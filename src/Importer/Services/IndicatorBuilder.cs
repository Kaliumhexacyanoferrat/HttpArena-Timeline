using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Importer.Models;

namespace Importer.Services;

/// <summary>
/// Records, per test, how the field of results changed day by day, so the SPA can compute
/// indicators (median, average, min, max — overall or per language) for whatever HttpArena
/// type filter the user selects. Aggregating in the browser keeps one small file per test
/// instead of one precomputed series per filter combination, scope and test.
///
/// The field mirrors what HttpArena showed at each point in time: every result file seen so
/// far contributes its latest row per (test, framework); a newer version of a file replaces
/// that file's rows, and deleting a file drops them. Rows are tagged with the file they came
/// from, so the July 2026 switch from per-test to per-framework files (old files deleted in
/// the same commit the new ones were added) never removes a row a newer file already set.
///
/// Results are merged one framework PR at a time, often dozens per day, so the field is
/// recorded once per UTC day (at the last data commit of that day) for every test touched
/// that day, rather than after each commit. Each day lists only what changed since the
/// previous one; a touched day without net changes is still recorded so the SPA can keep a
/// flat indicator reaching into the present.
///
/// Classification and failed runs are deliberately left to the SPA: every active row is
/// recorded, including rps &lt;= 0, since a failed run replaces the framework's previous row.
/// </summary>
public sealed class IndicatorBuilder(Func<string, string> languageOf)
{
    /// <summary>Folder under <c>data/</c>; reserved, no framework may use this name.</summary>
    public const string DirectoryName = "indicators";

    public const string AllScope = "all";

    // Recorded metrics; keys match the per-framework files and the SPA's metric list.
    private static readonly (string Key, Func<MetricsEntry, double?> Get)[] Metrics =
    [
        ("rps",              m => m.Rps),
        ("avg_latency_ms",   m => m.AvgLatencyMs),
        ("p99_latency_ms",   m => m.P99LatencyMs),
        ("p99_9_latency_ms", m => m.P999LatencyMs),
        ("cpu_pct",          m => m.CpuPct),
        ("cpu_per_req_us",   m => m.CpuPerReqUs),
        ("memory_bytes",     m => m.MemoryBytes),
        ("bandwidth_bps",    m => m.BandwidthBytesPerSec),
        ("input_bw_bps",     m => m.InputBwBytesPerSec),
        ("reconnects",       m => m.Reconnects),
        ("status_2xx",       m => m.Status2xx),
        ("status_3xx",       m => m.Status3xx),
        ("status_4xx",       m => m.Status4xx),
        ("status_5xx",       m => m.Status5xx),
    ];

    private static readonly JsonWriterOptions CompactOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // test -> framework -> latest row and the file it came from
    private readonly Dictionary<string, Dictionary<string, (MetricsEntry Entry, string Source)>> _field = new();

    // file path -> the (test, framework) rows it last contributed
    private readonly Dictionary<string, List<(string Test, string Framework)>> _contributions = new();

    // test -> the field as last recorded (framework -> metric values)
    private readonly Dictionary<string, Dictionary<string, double?[]>> _recorded = new();

    // test -> recorded days
    private readonly Dictionary<string, List<Day>> _logs = new();

    private readonly HashSet<string> _touched = new();
    private DateOnly? _day;
    private DateTimeOffset _dayTimestamp;

    private sealed record Day(DateTimeOffset Timestamp, List<(string Framework, double?[] Values)> Set, List<string> Removed);

    /// <summary>Starts processing a commit that changes result data.</summary>
    public void BeginCommit(DateTimeOffset timestamp)
    {
        var day = DateOnly.FromDateTime(timestamp.UtcDateTime);
        if (_day != day)
        {
            Flush();
            _day = day;
            _dayTimestamp = timestamp;
        }
        else if (timestamp > _dayTimestamp)
        {
            _dayTimestamp = timestamp;
        }
    }

    /// <summary>Replaces every row a result file contributes with its current content.</summary>
    public void SetFile(string path, IReadOnlyList<(string Test, string Framework, MetricsEntry Entry)> rows)
    {
        RemoveFile(path);

        var keys = new List<(string, string)>(rows.Count);
        foreach (var (test, framework, entry) in rows)
        {
            var testField = _field.TryGetValue(test, out var tf) ? tf : (_field[test] = new());
            testField[framework] = (entry, path);
            keys.Add((test, framework));
            _touched.Add(test);
        }
        _contributions[path] = keys;
    }

    /// <summary>Drops the rows of a deleted result file (unless a newer file has replaced them).</summary>
    public void RemoveFile(string path)
    {
        if (!_contributions.Remove(path, out var old)) return;

        foreach (var (test, framework) in old)
        {
            if (_field.TryGetValue(test, out var testField)
                && testField.TryGetValue(framework, out var row)
                && row.Source == path)
            {
                testField.Remove(framework);
                _touched.Add(test);
            }
        }
    }

    /// <summary>Records the final day after the last commit.</summary>
    public void Complete() => Flush();

    public int SeriesCount => _logs.Count;

    /// <summary>
    /// Scopes the SPA can aggregate over: "all" first, then every language with rows in some
    /// test, by name. Which of them have data for a test and filter is up to the SPA.
    /// </summary>
    public IEnumerable<(string Key, string Label, string? Language)> Scopes()
    {
        if (_logs.Count == 0) yield break;

        yield return (AllScope, "All", null);

        var languages = _logs.Values
            .SelectMany(days => days.SelectMany(d => d.Set.Select(s => s.Framework)))
            .Select(languageOf)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(l => l, StringComparer.OrdinalIgnoreCase);

        foreach (var language in languages)
            yield return (LanguageSlug(language), language, language);
    }

    private void Flush()
    {
        foreach (var test in _touched)
            Record(test);
        _touched.Clear();
    }

    private void Record(string test)
    {
        var current = _field.TryGetValue(test, out var tf) ? tf : new();
        var recorded = _recorded.TryGetValue(test, out var r) ? r : (_recorded[test] = new());

        var set = new List<(string, double?[])>();
        foreach (var (framework, (entry, _)) in current.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var values = Values(entry);
            if (recorded.TryGetValue(framework, out var before) && before.SequenceEqual(values)) continue;
            recorded[framework] = values;
            set.Add((framework, values));
        }

        var removed = recorded.Keys.Where(fw => !current.ContainsKey(fw)).OrderBy(fw => fw, StringComparer.Ordinal).ToList();
        foreach (var fw in removed) recorded.Remove(fw);

        var log = _logs.TryGetValue(test, out var l) ? l : (_logs[test] = []);
        if (log.Count == 0 && set.Count == 0) return; // nothing recorded yet, nothing to say

        log.Add(new Day(_dayTimestamp, set, removed));
    }

    private static double?[] Values(MetricsEntry entry)
    {
        var values = new double?[Metrics.Length];
        for (var i = 0; i < Metrics.Length; i++)
        {
            var v = Metrics[i].Get(entry);
            values[i] = v is { } d && !double.IsNaN(d) ? d : null;
        }
        return values;
    }

    /// <summary>
    /// Turns a language name into a folder/URL-safe key: C# -> csharp, C++ -> cpp, F# -> fsharp.
    /// </summary>
    public static string LanguageSlug(string language)
    {
        var sb = new StringBuilder();
        foreach (var c in language.Trim().ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(c);
            else if (c == '#') sb.Append("sharp");
            else if (c == '+') sb.Append('p');
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        // Never collide with the "all" scope or produce an empty name.
        return slug is "" or AllScope ? $"lang-{slug}" : slug;
    }

    public async Task WriteAsync(string outputPath)
    {
        var root = Path.Combine(outputPath, DirectoryName);
        Directory.CreateDirectory(root);
        foreach (var (test, days) in _logs)
            await WriteLogAsync(Path.Combine(root, test + ".json"), days);
    }

    /// <summary>
    /// <c>{"metrics": [...], "frameworks": [...], "days": [[ts, set, removed], ...]}</c> where
    /// <c>set</c> holds <c>[frameworkIndex, value per metric...]</c> rows and <c>removed</c>
    /// framework indices. Frameworks are listed in first-seen order so appending history never
    /// renumbers existing days; one day per line keeps the daily git diffs readable.
    /// </summary>
    private static async Task WriteLogAsync(string file, List<Day> days)
    {
        var frameworks = new List<string>();
        var frameworkIndex = new Dictionary<string, int>();
        foreach (var day in days)
        {
            foreach (var (fw, _) in day.Set)
            {
                if (frameworkIndex.ContainsKey(fw)) continue;
                frameworkIndex[fw] = frameworks.Count;
                frameworks.Add(fw);
            }
        }

        var sb = new StringBuilder();
        sb.Append("{\n  \"metrics\": ").Append(Json(w =>
        {
            w.WriteStartArray();
            foreach (var (key, _) in Metrics) w.WriteStringValue(key);
            w.WriteEndArray();
        }));
        sb.Append(",\n  \"frameworks\": ").Append(Json(w =>
        {
            w.WriteStartArray();
            foreach (var fw in frameworks) w.WriteStringValue(fw);
            w.WriteEndArray();
        }));
        sb.Append(",\n  \"days\": [");

        for (var d = 0; d < days.Count; d++)
        {
            var day = days[d];
            sb.Append(d == 0 ? "\n    " : ",\n    ");
            sb.Append(Json(w =>
            {
                w.WriteStartArray();
                w.WriteStringValue(day.Timestamp.ToUniversalTime().ToString("o"));
                w.WriteStartArray();
                foreach (var (fw, values) in day.Set)
                {
                    w.WriteStartArray();
                    w.WriteNumberValue(frameworkIndex[fw]);
                    foreach (var v in values)
                    {
                        if (v.HasValue) w.WriteNumberValue(v.Value);
                        else w.WriteNullValue();
                    }
                    w.WriteEndArray();
                }
                w.WriteEndArray();
                w.WriteStartArray();
                foreach (var fw in day.Removed) w.WriteNumberValue(frameworkIndex[fw]);
                w.WriteEndArray();
                w.WriteEndArray();
            }));
        }

        sb.Append(days.Count > 0 ? "\n  ]\n}\n" : "]\n}\n");
        await File.WriteAllTextAsync(file, sb.ToString(), new UTF8Encoding(false));
    }

    private static string Json(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, CompactOptions))
            write(writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
