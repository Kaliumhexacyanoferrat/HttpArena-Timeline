using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Importer.Models;
using LibGit2Sharp;

namespace Importer.Services;

public class TimelineImporter(string repoPath, string outputPath, string startingCommit)
{
    private readonly Dictionary<string, Dictionary<string, List<(DateTimeOffset Timestamp, MetricsEntry Entry)>>> _newData = new();
    private bool _isIncremental;

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
        if (!WalkHistory(repo)) return;

        Console.WriteLine($"Writing output to {outputPath}...");
        await WriteOutputAsync();

        SaveLastCommit(repo.Head.Tip.Sha);
        Console.WriteLine("Done.");
    }

    private bool WalkHistory(Repository repo)
    {
        var lastCommit = LoadLastCommit();
        _isIncremental = lastCommit != null;

        CommitFilter filter;
        if (_isIncremental)
        {
            var lastProcessed = repo.Lookup<Commit>(lastCommit!);
            if (lastProcessed?.Id == repo.Head.Tip.Id)
            {
                Console.WriteLine("  Already up-to-date.");
                return false;
            }
            filter = new CommitFilter
            {
                IncludeReachableFrom = repo.Head.Tip,
                ExcludeReachableFrom = lastProcessed,
                SortBy = CommitSortStrategies.Time | CommitSortStrategies.Reverse
            };
            Console.WriteLine("  Incremental update.");
        }
        else
        {
            var startCommit = repo.Lookup<Commit>(startingCommit)
                ?? throw new InvalidOperationException($"Starting commit '{startingCommit}' not found.");
            filter = new CommitFilter
            {
                IncludeReachableFrom = repo.Head.Tip,
                ExcludeReachableFrom = startCommit.Parents.Any() ? startCommit.Parents : null,
                SortBy = CommitSortStrategies.Time | CommitSortStrategies.Reverse
            };
            Console.WriteLine("  Full import.");
        }

        var commits = repo.Commits.QueryBy(filter).ToList();
        Console.WriteLine($"  Found {commits.Count} commits to process.");

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
        var allExisting = existing.Select(p  => (p.Timestamp,  Write: (Action<Utf8JsonWriter>)(w => p.Entry.WriteTo(w))));
        var allNew      = newPoints.Select(p => (p.Timestamp,  Write: (Action<Utf8JsonWriter>)(w => WriteMetrics(w, p.Entry))));
        var sorted      = allExisting.Concat(allNew).OrderBy(p => p.Timestamp).ToList();

        await using var stream = File.Create(outputFile);
        await using var writer = new Utf8JsonWriter(stream, WriterOptions);

        writer.WriteStartObject();
        writer.WriteStartArray("data");
        foreach (var (ts, write) in sorted)
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
