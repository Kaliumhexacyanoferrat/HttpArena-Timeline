using System.Text.Encodings.Web;
using System.Text.Json;
using Importer.Models;
using LibGit2Sharp;

namespace Importer.Services;

/// <summary>
/// Rebuilds the timeline data from scratch on every run. The set of framework/test
/// combinations to keep is read from the current main of HttpArena (see
/// <see cref="ActiveConfig"/>): only combos that are active *now* are emitted, so
/// disabled frameworks and retired tests are pruned even though history still contains them.
/// There is intentionally no incremental mode — the active set can change in ways that only
/// a full rebuild reflects correctly (a framework being disabled must retroactively drop its
/// whole directory, not just stop appending).
/// </summary>
public class TimelineImporter(string repoPath, string outputPath, string startingCommit)
{
    private readonly Dictionary<string, Dictionary<string, List<(DateTimeOffset Timestamp, MetricsEntry Entry)>>> _data = new();
    private readonly Dictionary<string, string> _frameworkLanguages = new();
    private ActiveConfig _active = null!;

    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NewLine = "\n"
    };

    public async Task RunAsync()
    {
        Console.WriteLine("Opening repository...");
        using var repo = new Repository(repoPath);

        var mainBranch = repo.Branches["main"]
            ?? throw new InvalidOperationException("Branch 'main' not found.");
        var mainTip = mainBranch.Tip;

        Console.WriteLine("Reading active framework/test set from main...");
        _active = ActiveConfig.Load(mainTip);
        Console.WriteLine($"  {_active.FrameworkCount} active frameworks, {_active.TestKeys.Count} active test keys.");

        Console.WriteLine("Walking git history...");
        WalkHistory(repo, mainTip);

        Console.WriteLine($"Writing output to {outputPath}...");
        await WriteOutputAsync();

        Console.WriteLine("Done.");
    }

    private void WalkHistory(Repository repo, Commit mainTip)
    {
        var snapshotCommit = repo.Lookup<Commit>(startingCommit)
            ?? throw new InvalidOperationException($"Starting commit '{startingCommit}' not found.");

        var commits = WalkFirstParent(mainTip, stopAt: snapshotCommit.Parents.FirstOrDefault()?.Sha);
        Console.WriteLine($"  Found {commits.Count} commits to process.");

        // Snapshot the starting commit's full tree so tests that existed before the start
        // commit but haven't changed since are still captured.
        SnapshotCommit(snapshotCommit);

        var processed = 0;
        foreach (var commit in commits)
        {
            ProcessCommit(repo, commit);
            if (++processed % 100 == 0)
                Console.WriteLine($"  {processed}/{commits.Count}...");
        }
        Console.WriteLine($"  Processed {processed} commits.");
    }

    private static List<Commit> WalkFirstParent(Commit tip, string? stopAt)
    {
        var commits = new List<Commit>();
        var current = tip;
        while (current != null)
        {
            if (current.Sha == stopAt) break;
            commits.Add(current);
            current = current.Parents.FirstOrDefault();
        }
        commits.Reverse();
        return commits;
    }

    private void SnapshotCommit(Commit commit)
    {
        var timestamp = commit.Author.When;
        if (commit.Tree["site/data"]?.Target is not Tree dataTree) return;

        foreach (var entry in dataTree)
        {
            if (entry.TargetType != TreeEntryTargetType.Blob) continue;
            var path = $"site/data/{entry.Name}";
            if (!IsRelevantDataFile(path)) continue;
            if (entry.Target is not Blob blob) continue;
            ProcessDataFile(path, blob.GetContentText(), timestamp);
        }

        // The results/ layout lives one directory deeper (site/data/results/<fw>.json).
        if (commit.Tree["site/data/results"]?.Target is not Tree resultsTree) return;
        foreach (var entry in resultsTree)
        {
            if (entry.TargetType != TreeEntryTargetType.Blob) continue;
            var path = $"site/data/results/{entry.Name}";
            if (!IsRelevantDataFile(path)) continue;
            if (entry.Target is not Blob blob) continue;
            ProcessDataFile(path, blob.GetContentText(), timestamp);
        }
    }

    private void ProcessCommit(Repository repo, Commit commit)
    {
        var parent = commit.Parents.FirstOrDefault();
        var changes = repo.Diff.Compare<TreeChanges>(parent?.Tree, commit.Tree);

        var changedFiles = changes.Where(c => IsRelevantDataFile(c.Path)).Select(c => c.Path).ToList();
        if (changedFiles.Count == 0) return;

        var timestamp = commit.Author.When;
        foreach (var filePath in changedFiles)
        {
            var blob = commit.Tree[filePath]?.Target as Blob;
            if (blob is null) continue;
            ProcessDataFile(filePath, blob.GetContentText(), timestamp);
        }
    }

    // Since commit 5698fa8 ("Store results one file per framework"), HttpArena stores results
    // as site/data/results/<slug>.json, one file per framework, holding { framework, results:
    // { "<test>-<conns>": {...single row...} } } instead of the old flat site/data/<test>.json
    // arrays holding every framework's row for that test. Both layouts are parsed here so
    // history predating the change can still be reproduced.
    private static bool IsResultsLayoutFile(string path) =>
        path.Contains("site/data/results/", StringComparison.OrdinalIgnoreCase);

    private void ProcessDataFile(string filePath, string content, DateTimeOffset timestamp)
    {
        var isResultsLayout = IsResultsLayoutFile(filePath);
        var testFile = Path.GetFileNameWithoutExtension(filePath);
        if (!isResultsLayout && !_active.TestKeys.Contains(testFile)) return;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"  Warning: skipping {filePath}: {ex.Message}");
            return;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (isResultsLayout)
            {
                if (root.ValueKind != JsonValueKind.Object) return;
                if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Object) return;

                foreach (var prop in results.EnumerateObject())
                {
                    if (!_active.TestKeys.Contains(prop.Name)) continue;
                    if (prop.Value.ValueKind != JsonValueKind.Object) continue;
                    AddEntry(prop.Value, prop.Name, timestamp);
                }
            }
            else
            {
                if (root.ValueKind != JsonValueKind.Array) return;
                foreach (var entry in root.EnumerateArray())
                    AddEntry(entry, testFile, timestamp);
            }
        }
    }

    private void AddEntry(JsonElement entry, string testFile, DateTimeOffset timestamp)
    {
        if (!entry.TryGetProperty("framework", out var fwProp)) return;
        var framework = fwProp.GetString()?.ToLowerInvariant();
        if (string.IsNullOrEmpty(framework)) return;

        // Only keep combos that are active on the current main.
        if (!_active.IsActive(framework, testFile)) return;

        if (!_frameworkLanguages.ContainsKey(framework))
        {
            var lang = _active.LanguageFor(framework)
                ?? (entry.TryGetProperty("language", out var lp) && lp.ValueKind == JsonValueKind.String
                    ? lp.GetString() ?? "Unknown" : "Unknown");
            _frameworkLanguages[framework] = lang;
        }

        var metrics = ParseMetrics(entry);
        var frameworkData = _data.TryGetValue(framework, out var fd) ? fd : (_data[framework] = new());
        var testData = frameworkData.TryGetValue(testFile, out var td) ? td : (frameworkData[testFile] = []);
        testData.Add((timestamp, metrics));
    }

    private static MetricsEntry ParseMetrics(JsonElement e)
    {
        string? S(string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        long    L(string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;
        int     I(string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

        return new MetricsEntry(
            Rps:                  L("rps"),
            AvgLatencyMs:         ValueParser.ParseLatencyMs(S("avg_latency")),
            P99LatencyMs:         ValueParser.ParseLatencyMs(S("p99_latency")),
            CpuPct:               ValueParser.ParseCpuPct(S("cpu")),
            MemoryBytes:          ValueParser.ParseMemoryBytes(S("memory")),
            Connections:          I("connections"),
            Threads:              I("threads"),
            DurationMs:           ValueParser.ParseDurationMs(S("duration")),
            Pipeline:             I("pipeline"),
            BandwidthBytesPerSec: ValueParser.ParseBandwidthBytesPerSec(S("bandwidth")),
            InputBwBytesPerSec:   ValueParser.ParseBandwidthBytesPerSec(S("input_bw")),
            Reconnects:           I("reconnects"),
            Status2xx:            I("status_2xx"),
            Status3xx:            I("status_3xx"),
            Status4xx:            I("status_4xx"),
            Status5xx:            I("status_5xx")
        );
    }

    private static bool IsRelevantDataFile(string path)
    {
        if (!path.StartsWith("site/data/", StringComparison.OrdinalIgnoreCase)) return false;
        if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return false;
        var name = Path.GetFileName(path);
        if (name is "current.json" or "frameworks.json" or "langcolors.json") return false;
        if (path.Contains("/rounds/")) return false;
        return true;
    }

    private async Task WriteOutputAsync()
    {
        CleanFrameworkDirectories();

        foreach (var (framework, testFiles) in _data)
        {
            var frameworkDir = Path.Combine(outputPath, SanitizeName(framework));
            Directory.CreateDirectory(frameworkDir);

            foreach (var (testFile, points) in testFiles)
            {
                var outputFile = Path.Combine(frameworkDir, testFile + ".json");
                await WriteTimelineFileAsync(outputFile, points);
            }
        }

        var totalFiles = _data.Values.Sum(d => d.Count);
        Console.WriteLine($"  Wrote {totalFiles} files for {_data.Count} frameworks.");

        await WriteIndexAsync();
    }

    private async Task WriteIndexAsync()
    {
        var index = new Dictionary<string, (string Language, SortedSet<string> Tests)>();
        foreach (var (fw, tests) in _data)
        {
            var lang = _frameworkLanguages.GetValueOrDefault(fw, "Unknown");
            index[fw] = (lang, new SortedSet<string>(tests.Keys));
        }

        var allTests = index.Values.SelectMany(v => v.Tests).Distinct().OrderBy(t => t).ToList();

        var indexPath = Path.Combine(outputPath, "index.json");
        await using var stream = File.Create(indexPath);
        await using var writer = new Utf8JsonWriter(stream, WriterOptions);

        writer.WriteStartObject();
        writer.WriteStartObject("frameworks");
        foreach (var (fw, (lang, tests)) in index.OrderBy(kv => kv.Key))
        {
            writer.WriteStartObject(fw);
            writer.WriteString("language", lang);
            writer.WriteStartArray("tests");
            foreach (var t in tests) writer.WriteStringValue(t);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
        writer.WriteStartArray("tests");
        foreach (var t in allTests) writer.WriteStringValue(t);
        writer.WriteEndArray();
        writer.WriteEndObject();

        await writer.FlushAsync();
        Console.WriteLine($"  Wrote index.json ({index.Count} frameworks, {allTests.Count} tests).");
    }

    private void CleanFrameworkDirectories()
    {
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
            return;
        }
        foreach (var dir in Directory.EnumerateDirectories(outputPath))
            Directory.Delete(dir, true);
    }

    private static async Task WriteTimelineFileAsync(
        string outputFile,
        List<(DateTimeOffset Timestamp, MetricsEntry Entry)> points)
    {
        var sorted = points.OrderBy(p => p.Timestamp).ToList();

        // Collapse runs of identical rps, but never drop more than a week of gap so the
        // series still shows movement over time even while a value is flat.
        var filtered = new List<(DateTimeOffset Timestamp, MetricsEntry Entry)>();
        (DateTimeOffset Timestamp, long Rps)? prev = null;
        foreach (var (ts, entry) in sorted)
        {
            if (prev is null || prev.Value.Rps != entry.Rps || (ts - prev.Value.Timestamp).TotalDays >= 7)
            {
                filtered.Add((ts, entry));
                prev = (ts, entry.Rps);
            }
        }

        await using var stream = File.Create(outputFile);
        await using var writer = new Utf8JsonWriter(stream, WriterOptions);

        writer.WriteStartObject();
        writer.WriteStartArray("data");
        foreach (var (ts, entry) in filtered)
        {
            writer.WriteStartArray();
            writer.WriteStringValue(ts.ToUniversalTime().ToString("o"));
            WriteMetrics(writer, entry);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();

        await writer.FlushAsync();
    }

    private static void WriteMetrics(Utf8JsonWriter w, MetricsEntry m)
    {
        w.WriteStartObject();
        w.WriteNumber("rps",            m.Rps);
        WriteNullable(w, "avg_latency_ms",    m.AvgLatencyMs);
        WriteNullable(w, "p99_latency_ms",    m.P99LatencyMs);
        WriteNullable(w, "cpu_pct",           m.CpuPct);
        WriteNullable(w, "memory_bytes",      m.MemoryBytes);
        w.WriteNumber("connections",    m.Connections);
        w.WriteNumber("threads",        m.Threads);
        WriteNullable(w, "duration_ms",       m.DurationMs);
        w.WriteNumber("pipeline",       m.Pipeline);
        WriteNullable(w, "bandwidth_bps",     m.BandwidthBytesPerSec);
        WriteNullable(w, "input_bw_bps",      m.InputBwBytesPerSec);
        w.WriteNumber("reconnects",     m.Reconnects);
        w.WriteNumber("status_2xx",     m.Status2xx);
        w.WriteNumber("status_3xx",     m.Status3xx);
        w.WriteNumber("status_4xx",     m.Status4xx);
        w.WriteNumber("status_5xx",     m.Status5xx);
        w.WriteEndObject();
    }

    private static void WriteNullable(Utf8JsonWriter w, string name, double? v)
    {
        if (v.HasValue) w.WriteNumber(name, v.Value);
        else w.WriteNull(name);
    }

    private static void WriteNullable(Utf8JsonWriter w, string name, long? v)
    {
        if (v.HasValue) w.WriteNumber(name, v.Value);
        else w.WriteNull(name);
    }

    // Framework names must map to a single directory regardless of casing seen in the source
    // data, or a case-insensitive filesystem (Windows) and a case-sensitive one (git/Linux CI)
    // disagree about whether "Fletch" and "fletch" are the same path, corrupting the git tree.
    private static string SanitizeName(string name) =>
        string.Concat(name.Select(c => Array.IndexOf(InvalidChars, c) >= 0 ? '_' : c)).ToLowerInvariant();
}
