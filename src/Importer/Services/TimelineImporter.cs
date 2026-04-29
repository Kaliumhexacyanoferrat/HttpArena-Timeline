using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Importer.Models;
using LibGit2Sharp;

namespace Importer.Services;

public class TimelineImporter(string repoPath, string outputPath, string startingCommit)
{
    private readonly Dictionary<string, Dictionary<string, List<(DateTimeOffset Timestamp, MetricsEntry Entry)>>> _newData = new();
    private readonly Dictionary<string, string> _frameworkLanguages = new();
    private bool _isIncremental;
    private string? _mainTipSha;

    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task RunAsync()
    {
        Console.WriteLine("Opening repository...");
        using var repo = new Repository(repoPath);

        Console.WriteLine("Walking git history...");
        var hasNewCommits = WalkHistory(repo);

        if (hasNewCommits)
        {
            Console.WriteLine($"Writing output to {outputPath}...");
            await WriteOutputAsync();
            SaveLastCommit(_mainTipSha!);
        }
        else if (!File.Exists(Path.Combine(outputPath, "index.json")))
        {
            // No new commits but index is missing — generate it from existing data on disk.
            Console.WriteLine("Generating index.json from existing data...");
            await WriteIndexFromDiskAsync();
        }

        Console.WriteLine("Done.");
    }

    private bool WalkHistory(Repository repo)
    {
        var mainBranch = repo.Branches["main"]
            ?? throw new InvalidOperationException("Branch 'main' not found.");
        var mainTip = mainBranch.Tip;
        _mainTipSha = mainTip.Sha;

        var lastCommit = LoadLastCommit();
        _isIncremental = lastCommit != null;

        List<Commit> commits;
        Commit? snapshotCommit = null;

        if (_isIncremental)
        {
            var lastProcessed = repo.Lookup<Commit>(lastCommit!);
            if (lastProcessed?.Id == mainTip.Id)
            {
                Console.WriteLine("  Already up-to-date.");
                return false;
            }
            commits = WalkFirstParent(mainTip, stopAt: lastProcessed?.Sha);
            Console.WriteLine("  Incremental update.");
        }
        else
        {
            snapshotCommit = repo.Lookup<Commit>(startingCommit)
                ?? throw new InvalidOperationException($"Starting commit '{startingCommit}' not found.");
            commits = WalkFirstParent(mainTip, stopAt: snapshotCommit.Parents.FirstOrDefault()?.Sha);
            Console.WriteLine("  Full import.");
        }

        Console.WriteLine($"  Found {commits.Count} commits to process.");

        // For a full (non-incremental) import, snapshot the starting commit's full tree
        // so tests that existed before the start commit but haven't changed since are captured.
        if (snapshotCommit is not null)
            SnapshotCommit(snapshotCommit);

        var processed = 0;
        foreach (var commit in commits)
        {
            ProcessCommit(repo, commit);
            if (++processed % 100 == 0)
                Console.WriteLine($"  {processed}/{commits.Count}...");
        }
        Console.WriteLine($"  Processed {processed} commits.");
        return true;
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

    private void ProcessDataFile(string filePath, string content, DateTimeOffset timestamp)
    {
        var testFile = Path.GetFileNameWithoutExtension(filePath);

        JsonElement[] entries;
        try
        {
            entries = JsonSerializer.Deserialize<JsonElement[]>(content) ?? [];
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"  Warning: skipping {filePath}: {ex.Message}");
            return;
        }

        foreach (var entry in entries)
        {
            if (!entry.TryGetProperty("framework", out var fwProp)) continue;
            var framework = fwProp.GetString();
            if (string.IsNullOrEmpty(framework)) continue;

            if (!_frameworkLanguages.ContainsKey(framework))
            {
                var lang = entry.TryGetProperty("language", out var lp) && lp.ValueKind == JsonValueKind.String
                    ? lp.GetString() ?? "Unknown" : "Unknown";
                _frameworkLanguages[framework] = lang;
            }

            var metrics = ParseMetrics(entry);
            var frameworkData = _newData.TryGetValue(framework, out var fd) ? fd : (_newData[framework] = new());
            var testData = frameworkData.TryGetValue(testFile, out var td) ? td : (frameworkData[testFile] = []);
            testData.Add((timestamp, metrics));
        }
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
        if (!_isIncremental)
            CleanFrameworkDirectories();

        foreach (var (framework, testFiles) in _newData)
        {
            var frameworkDir = Path.Combine(outputPath, SanitizeName(framework));
            Directory.CreateDirectory(frameworkDir);

            foreach (var (testFile, newPoints) in testFiles)
            {
                var outputFile = Path.Combine(frameworkDir, testFile + ".json");

                var existing = _isIncremental && File.Exists(outputFile)
                    ? LoadExistingPoints(outputFile)
                    : [];

                await WriteTimelineFileAsync(outputFile, existing, newPoints);
            }
        }

        var totalFiles = _newData.Values.Sum(d => d.Count);
        Console.WriteLine($"  Wrote {totalFiles} files for {_newData.Count} frameworks.");

        await WriteIndexAsync();
    }

    private async Task WriteIndexAsync()
    {
        // For incremental: merge new data into existing index. For full rebuild: start fresh.
        var index = _isIncremental ? LoadExistingIndex() : new Dictionary<string, (string Language, SortedSet<string> Tests)>();

        foreach (var (fw, tests) in _newData)
        {
            var lang = _frameworkLanguages.GetValueOrDefault(fw, "Unknown");
            if (index.TryGetValue(fw, out var existing))
                foreach (var t in tests.Keys) existing.Tests.Add(t);
            else
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

    private async Task WriteIndexFromDiskAsync()
    {
        // Build index by scanning the data directory (used when index.json is missing but data exists).
        if (!Directory.Exists(outputPath)) return;

        var index = new Dictionary<string, (string Language, SortedSet<string> Tests)>();
        foreach (var fwDir in Directory.EnumerateDirectories(outputPath))
        {
            var fw = Path.GetFileName(fwDir);
            var tests = new SortedSet<string>(
                Directory.EnumerateFiles(fwDir, "*.json")
                         .Select(f => Path.GetFileNameWithoutExtension(f)!));

            // Try to get language from first data file
            var lang = "Unknown";
            var firstFile = Directory.EnumerateFiles(fwDir, "*.json").FirstOrDefault();
            if (firstFile != null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllBytes(firstFile));
                    var firstPoint = doc.RootElement.GetProperty("data").EnumerateArray().FirstOrDefault();
                    if (firstPoint.ValueKind != JsonValueKind.Undefined
                        && firstPoint[1].TryGetProperty("language", out var lp))
                        lang = lp.GetString() ?? "Unknown";
                }
                catch { /* ignore */ }
            }

            index[fw] = (lang, tests);
        }

        _isIncremental = false; // Write fresh index
        // Temporarily populate _newData so WriteIndexAsync can use it, but skip file writing
        // Instead, write the index directly here
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

    private Dictionary<string, (string Language, SortedSet<string> Tests)> LoadExistingIndex()
    {
        var result = new Dictionary<string, (string, SortedSet<string>)>();
        var indexPath = Path.Combine(outputPath, "index.json");
        if (!File.Exists(indexPath)) return result;

        using var doc = JsonDocument.Parse(File.ReadAllBytes(indexPath));
        if (!doc.RootElement.TryGetProperty("frameworks", out var fws)) return result;

        foreach (var fw in fws.EnumerateObject())
        {
            var lang = fw.Value.TryGetProperty("language", out var lp) ? lp.GetString() ?? "Unknown" : "Unknown";
            var tests = new SortedSet<string>();
            if (fw.Value.TryGetProperty("tests", out var tp))
                foreach (var t in tp.EnumerateArray())
                    if (t.GetString() is { } s) tests.Add(s);
            result[fw.Name] = (lang, tests);
        }

        return result;
    }

    private void CleanFrameworkDirectories()
    {
        if (!Directory.Exists(outputPath)) return;
        foreach (var dir in Directory.EnumerateDirectories(outputPath))
            Directory.Delete(dir, true);
    }

    private static List<(DateTimeOffset Timestamp, JsonElement Entry)> LoadExistingPoints(string filePath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(filePath));
        return doc.RootElement.GetProperty("data")
            .EnumerateArray()
            .Select(pair =>
            {
                var ts = DateTimeOffset.Parse(pair[0].GetString()!, CultureInfo.InvariantCulture);
                return (ts, pair[1].Clone());
            })
            .ToList();
    }

    private static async Task WriteTimelineFileAsync(
        string outputFile,
        List<(DateTimeOffset Timestamp, JsonElement Entry)> existing,
        List<(DateTimeOffset Timestamp, MetricsEntry Entry)> newPoints)
    {
        var allExisting = existing.Select(p  => (p.Timestamp, Rps: GetRps(p.Entry),  Write: (Action<Utf8JsonWriter>)(w => p.Entry.WriteTo(w))));
        var allNew      = newPoints.Select(p => (p.Timestamp, Rps: p.Entry.Rps,      Write: (Action<Utf8JsonWriter>)(w => WriteMetrics(w, p.Entry))));
        var sorted      = allExisting.Concat(allNew).OrderBy(p => p.Timestamp).ToList();

        var filtered = new List<(DateTimeOffset Timestamp, Action<Utf8JsonWriter> Write)>();
        (DateTimeOffset Timestamp, long Rps)? prev = null;
        foreach (var (ts, rps, write) in sorted)
        {
            if (prev is null || prev.Value.Rps != rps || (ts - prev.Value.Timestamp).TotalDays >= 7)
            {
                filtered.Add((ts, write));
                prev = (ts, rps);
            }
        }

        await using var stream = File.Create(outputFile);
        await using var writer = new Utf8JsonWriter(stream, WriterOptions);

        writer.WriteStartObject();
        writer.WriteStartArray("data");
        foreach (var (ts, write) in filtered)
        {
            writer.WriteStartArray();
            writer.WriteStringValue(ts.ToUniversalTime().ToString("o"));
            write(writer);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();

        await writer.FlushAsync();
    }

    private static long GetRps(JsonElement entry) =>
        entry.TryGetProperty("rps", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;

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

    private string? LoadLastCommit()
    {
        var statePath = Path.Combine(outputPath, "state.json");
        if (!File.Exists(statePath)) return null;
        using var doc = JsonDocument.Parse(File.ReadAllBytes(statePath));
        return doc.RootElement.TryGetProperty("lastCommit", out var v) ? v.GetString() : null;
    }

    private void SaveLastCommit(string sha)
    {
        Directory.CreateDirectory(outputPath);
        File.WriteAllText(Path.Combine(outputPath, "state.json"),
            JsonSerializer.Serialize(new { lastCommit = sha }));
    }

    private static string SanitizeName(string name) =>
        string.Concat(name.Select(c => Array.IndexOf(InvalidChars, c) >= 0 ? '_' : c));
}
