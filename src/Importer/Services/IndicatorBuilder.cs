using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Importer.Models;

namespace Importer.Services;

/// <summary>
/// Computes field-wide indicators (median, average, min, max) per test — across all
/// frameworks and per language — and historizes them while the importer walks history.
///
/// The field mirrors what HttpArena showed at each point in time: every result file seen so
/// far contributes its latest row per (test, framework); a newer version of a file replaces
/// that file's rows, and deleting a file drops them. Rows are tagged with the file they came
/// from, so the July 2026 switch from per-test to per-framework files (old files deleted in
/// the same commit the new ones were added) never removes a row a newer file already set.
///
/// Results are merged one framework PR at a time, often dozens per day, so the field is
/// snapshotted once per UTC day (at the last data commit of that day) for every test touched
/// that day, rather than after each commit.
/// </summary>
public sealed class IndicatorBuilder(Func<string, string> languageOf)
{
    /// <summary>Folder under <c>data/</c>; reserved, no framework may use this name.</summary>
    public const string DirectoryName = "indicators";

    public const string AllScope = "all";

    // Aggregated metrics; keys match the per-framework files and the SPA's metric list.
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

    private readonly Dictionary<(string Scope, string Test), List<(DateTimeOffset Timestamp, Aggregate Value)>> _series = new();
    private readonly Dictionary<string, string> _languageScopes = new(); // scope slug -> language

    private readonly HashSet<string> _touched = new();
    private DateOnly? _day;
    private DateTimeOffset _dayTimestamp;

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

    /// <summary>Takes the final snapshot after the last commit.</summary>
    public void Complete() => Flush();

    public int SeriesCount => _series.Count;

    /// <summary>Scopes with data: "all" first, then languages by name.</summary>
    public IEnumerable<(string Key, string Label, string? Language)> Scopes()
    {
        var withData = _series.Keys.Select(k => k.Scope).ToHashSet();

        if (withData.Contains(AllScope))
            yield return (AllScope, "All frameworks", null);

        foreach (var (slug, language) in _languageScopes.OrderBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase))
        {
            if (withData.Contains(slug))
                yield return (slug, language, language);
        }
    }

    private void Flush()
    {
        foreach (var test in _touched)
            Snapshot(test);
        _touched.Clear();
    }

    private void Snapshot(string test)
    {
        if (!_field.TryGetValue(test, out var testField)) return;

        // rps <= 0 marks a failed run; it would pin every minimum to zero, so leave it out.
        var valid = testField
            .Where(kv => kv.Value.Entry.Rps > 0)
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => (Framework: kv.Key, kv.Value.Entry))
            .ToList();

        if (valid.Count == 0) return;

        Append(AllScope, test, valid);

        foreach (var group in valid.GroupBy(v => languageOf(v.Framework)))
        {
            var slug = LanguageSlug(group.Key);
            _languageScopes.TryAdd(slug, group.Key);
            Append(slug, test, group.ToList());
        }
    }

    private void Append(string scope, string test, List<(string Framework, MetricsEntry Entry)> rows)
    {
        var value = Aggregate.Compute(rows);
        var series = _series.TryGetValue((scope, test), out var s) ? s : (_series[(scope, test)] = []);

        // Same rule as the framework files: skip unchanged snapshots, but never leave a
        // gap of a week or more so a flat indicator still reaches into the present.
        if (series.Count > 0)
        {
            var (lastTs, last) = series[^1];
            if (last.SameAs(value) && (_dayTimestamp - lastTs).TotalDays < 7) return;
        }

        series.Add((_dayTimestamp, value));
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
        foreach (var ((scope, test), points) in _series)
        {
            var dir = Path.Combine(root, scope);
            Directory.CreateDirectory(dir);
            await WriteSeriesAsync(Path.Combine(dir, test + ".json"), points);
        }
    }

    /// <summary>
    /// Columnar to keep ~1,300 files small: each point holds one value per entry of
    /// <c>metrics</c>, and min/max attribution as indices into <c>frameworks</c> (listed in
    /// first-seen order so appending history never renumbers existing points). One point per
    /// line keeps the daily git diffs readable.
    /// </summary>
    private static async Task WriteSeriesAsync(string file, List<(DateTimeOffset Timestamp, Aggregate Value)> points)
    {
        var sorted = points.OrderBy(p => p.Timestamp).ToList();

        var frameworks = new List<string>();
        var frameworkIndex = new Dictionary<string, int>();
        int IndexOf(string fw)
        {
            if (frameworkIndex.TryGetValue(fw, out var i)) return i;
            frameworkIndex[fw] = frameworks.Count;
            frameworks.Add(fw);
            return frameworks.Count - 1;
        }
        foreach (var (_, value) in sorted)
        {
            foreach (var fw in value.MinFramework) if (fw is not null) IndexOf(fw);
            foreach (var fw in value.MaxFramework) if (fw is not null) IndexOf(fw);
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
        sb.Append(",\n  \"data\": [");

        for (var p = 0; p < sorted.Count; p++)
        {
            var (ts, v) = sorted[p];
            sb.Append(p == 0 ? "\n    " : ",\n    ");
            sb.Append(Json(w =>
            {
                w.WriteStartArray();
                w.WriteStringValue(ts.ToUniversalTime().ToString("o"));
                w.WriteStartObject();
                w.WriteNumber("n", v.Count);
                WriteValues(w, "median", v.Median);
                WriteValues(w, "avg", v.Average);
                WriteValues(w, "min", v.Min);
                WriteValues(w, "max", v.Max);
                WriteIndices(w, "min_fw", v.MinFramework, frameworkIndex);
                WriteIndices(w, "max_fw", v.MaxFramework, frameworkIndex);
                w.WriteEndObject();
                w.WriteEndArray();
            }));
        }

        sb.Append(sorted.Count > 0 ? "\n  ]\n}\n" : "]\n}\n");
        await File.WriteAllTextAsync(file, sb.ToString(), new UTF8Encoding(false));
    }

    private static string Json(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, CompactOptions))
            write(writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteValues(Utf8JsonWriter w, string name, double?[] values)
    {
        w.WriteStartArray(name);
        foreach (var v in values)
        {
            if (v.HasValue) w.WriteNumberValue(Math.Round(v.Value, 3));
            else w.WriteNullValue();
        }
        w.WriteEndArray();
    }

    private static void WriteIndices(Utf8JsonWriter w, string name, string?[] frameworks, Dictionary<string, int> index)
    {
        w.WriteStartArray(name);
        foreach (var fw in frameworks)
        {
            if (fw is not null) w.WriteNumberValue(index[fw]);
            else w.WriteNullValue();
        }
        w.WriteEndArray();
    }

    /// <summary>One snapshot of a scope: every statistic for every metric.</summary>
    private sealed class Aggregate
    {
        public int Count { get; private init; }
        public required double?[] Median { get; init; }
        public required double?[] Average { get; init; }
        public required double?[] Min { get; init; }
        public required double?[] Max { get; init; }
        public required string?[] MinFramework { get; init; }
        public required string?[] MaxFramework { get; init; }

        /// <param name="rows">Sorted by framework name, so ties resolve deterministically.</param>
        public static Aggregate Compute(List<(string Framework, MetricsEntry Entry)> rows)
        {
            var n = Metrics.Length;
            var result = new Aggregate
            {
                Count = rows.Count,
                Median = new double?[n], Average = new double?[n],
                Min = new double?[n], Max = new double?[n],
                MinFramework = new string?[n], MaxFramework = new string?[n],
            };

            for (var i = 0; i < n; i++)
            {
                var get = Metrics[i].Get;
                var values = new List<double>(rows.Count);

                foreach (var (fw, entry) in rows)
                {
                    if (get(entry) is not { } v || double.IsNaN(v)) continue;
                    values.Add(v);
                    if (result.Min[i] is null || v < result.Min[i]) { result.Min[i] = v; result.MinFramework[i] = fw; }
                    if (result.Max[i] is null || v > result.Max[i]) { result.Max[i] = v; result.MaxFramework[i] = fw; }
                }

                if (values.Count == 0) continue;

                // When every framework scores the same (e.g. all report zero 3xx), naming
                // one of them as min/max would be arbitrary.
                if (result.Min[i] == result.Max[i])
                    result.MinFramework[i] = result.MaxFramework[i] = null;

                values.Sort();
                var mid = values.Count / 2;
                result.Median[i] = values.Count % 2 == 1 ? values[mid] : (values[mid - 1] + values[mid]) / 2;
                result.Average[i] = values.Average();
            }

            return result;
        }

        public bool SameAs(Aggregate other) =>
            Count == other.Count
            && Median.SequenceEqual(other.Median)
            && Average.SequenceEqual(other.Average)
            && Min.SequenceEqual(other.Min)
            && Max.SequenceEqual(other.Max)
            && MinFramework.SequenceEqual(other.MinFramework)
            && MaxFramework.SequenceEqual(other.MaxFramework);
    }
}
