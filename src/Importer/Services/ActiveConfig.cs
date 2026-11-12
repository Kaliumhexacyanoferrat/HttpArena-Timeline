using System.Text.Json;
using System.Text.RegularExpressions;
using Importer.Models;
using LibGit2Sharp;

namespace Importer.Services;

/// <summary>
/// The set of framework/test combinations that are actively benchmarked on the current
/// main of HttpArena. This is the source of truth for what the timeline shows: history is
/// only kept for combos that are active *now*, so disabled frameworks and retired tests
/// (which can still linger in the results/*.json files) never reach the output.
///
/// It is derived from two authoritative files at main's tip:
///   * <c>scripts/lib/profiles.sh</c> — maps a base test name to its concurrency level(s),
///     e.g. echo-ws -> 512,4096,16384, yielding the concrete "&lt;test&gt;-&lt;conns&gt;" keys.
///   * <c>frameworks/&lt;dir&gt;/meta.json</c> — declares, per framework, whether it is
///     <c>enabled</c> and which base tests it runs. Several variant folders can share one
///     <c>display_name</c> (actix, actix-h2c, actix-websocket -> "actix"); their test lists
///     are unioned, matching how results merge them under a single framework name.
///
/// It also carries each framework's classification (type tier and standard/tuned mode),
/// resolved exactly like HttpArena's <c>scripts/rebuild_site_data.py</c> builds the
/// leaderboard's <c>frameworks.json</c>: every meta.json counts (enabled or not), entries are
/// grouped by <c>display_name</c>, and the group's primary entry — the folder named like the
/// display name, else the first folder alphabetically — defines type and mode.
/// </summary>
public sealed partial class ActiveConfig
{
    // framework key (trimmed, lower-cased display_name) -> active "<test>-<conns>" keys
    private readonly Dictionary<string, HashSet<string>> _combos;

    // framework key -> language (from meta.json)
    public IReadOnlyDictionary<string, string> Languages { get; }

    // union of every active test key across all frameworks
    public IReadOnlySet<string> TestKeys { get; }

    // framework key -> classification of the display name's primary meta.json
    private readonly Dictionary<string, Classification> _classifications;

    public int FrameworkCount => _combos.Count;

    private ActiveConfig(
        Dictionary<string, HashSet<string>> combos,
        Dictionary<string, string> languages,
        HashSet<string> testKeys,
        Dictionary<string, Classification> classifications)
    {
        _combos = combos;
        Languages = languages;
        TestKeys = testKeys;
        _classifications = classifications;
    }

    private static string Key(string framework) => framework.Trim().ToLowerInvariant();

    /// <summary>True if this framework currently runs this concrete test key.</summary>
    public bool IsActive(string framework, string testKey) =>
        _combos.TryGetValue(Key(framework), out var set) && set.Contains(testKey);

    public string? LanguageFor(string framework) =>
        Languages.TryGetValue(Key(framework), out var lang) ? lang : null;

    /// <summary>The framework's type and mode; HttpArena's defaults if it has no meta.json.</summary>
    public Classification ClassificationOf(string framework) =>
        _classifications.TryGetValue(Key(framework), out var c) ? c : Classification.Default;

    public static ActiveConfig Load(Commit mainTip)
    {
        var profiles = LoadProfiles(mainTip);

        var combos = new Dictionary<string, HashSet<string>>();
        var languages = new Dictionary<string, string>();
        var testKeys = new HashSet<string>();
        var candidates = new List<(string DisplayName, string Dir, Classification Classification)>();

        if (mainTip.Tree["frameworks"]?.Target is Tree frameworksTree)
        {
            foreach (var dirEntry in frameworksTree)
            {
                if (dirEntry.Target is not Tree fwTree) continue;
                if (fwTree["meta.json"]?.Target is not Blob metaBlob) continue;

                LoadMeta(metaBlob.GetContentText(), dirEntry.Name, profiles, combos, languages, testKeys, candidates);
            }
        }

        return new ActiveConfig(combos, languages, testKeys, ResolveClassifications(candidates));
    }

    /// <summary>
    /// One classification per display name, picked like rebuild_site_data.py does: the entry
    /// whose folder equals the display name, else the first folder in ordinal order.
    /// </summary>
    private static Dictionary<string, Classification> ResolveClassifications(
        List<(string DisplayName, string Dir, Classification Classification)> candidates)
    {
        var result = new Dictionary<string, Classification>();

        foreach (var group in candidates.GroupBy(c => c.DisplayName, StringComparer.Ordinal))
        {
            var sorted = group.OrderBy(c => c.Dir, StringComparer.Ordinal).ToList();
            var primary = sorted.FirstOrDefault(c => c.Dir == group.Key);
            if (primary.Dir is null) primary = sorted[0];

            result.TryAdd(Key(group.Key), primary.Classification);
        }

        return result;
    }

    private static void LoadMeta(
        string content,
        string dirName,
        IReadOnlyDictionary<string, List<string>> profiles,
        Dictionary<string, HashSet<string>> combos,
        Dictionary<string, string> languages,
        HashSet<string> testKeys,
        List<(string DisplayName, string Dir, Classification Classification)> candidates)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(content); }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"  Warning: skipping frameworks/{dirName}/meta.json: {ex.Message}");
            return;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;

            var displayName = root.TryGetProperty("display_name", out var dn) && dn.ValueKind == JsonValueKind.String
                ? dn.GetString() : null;
            if (string.IsNullOrWhiteSpace(displayName)) displayName = dirName;

            // Disabled entries still take part in picking the display name's classification.
            candidates.Add((displayName!, dirName, ReadClassification(root)));

            if (!root.TryGetProperty("enabled", out var enabled) || enabled.ValueKind != JsonValueKind.True)
                return;

            var key = Key(displayName!);

            if (root.TryGetProperty("language", out var lp) && lp.ValueKind == JsonValueKind.String
                && lp.GetString() is { Length: > 0 } lang)
                languages[key] = lang;

            if (!root.TryGetProperty("tests", out var tests) || tests.ValueKind != JsonValueKind.Array)
                return;

            var set = combos.TryGetValue(key, out var s) ? s : (combos[key] = new(StringComparer.OrdinalIgnoreCase));

            foreach (var t in tests.EnumerateArray())
            {
                if (t.ValueKind != JsonValueKind.String) continue;
                var baseTest = t.GetString();
                if (string.IsNullOrEmpty(baseTest)) continue;

                if (!profiles.TryGetValue(baseTest, out var conns))
                {
                    Console.Error.WriteLine($"  Warning: {dirName}: test '{baseTest}' has no profile in profiles.sh; skipping.");
                    continue;
                }

                foreach (var conn in conns)
                {
                    var testKey = $"{baseTest}-{conn}";
                    set.Add(testKey);
                    testKeys.Add(testKey);
                }
            }
        }
    }

    /// <summary>Type and mode with the leaderboard's fallbacks (emerging, standard).</summary>
    private static Classification ReadClassification(JsonElement root)
    {
        static string? Str(JsonElement root, string name) =>
            root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s
                ? s : null;

        return new Classification(
            Str(root, "type") ?? Classification.DefaultType,
            Str(root, "mode") ?? Classification.DefaultMode);
    }

    /// <summary>
    /// Parse scripts/lib/profiles.sh into base-test -> concurrency list. Each PROFILES entry
    /// looks like <c>[baseline]="1|0|0-31,64-95|4096|"</c> where field 4 (0-indexed 3) is the
    /// comma-separated connection counts, one concrete run per value.
    /// </summary>
    private static Dictionary<string, List<string>> LoadProfiles(Commit mainTip)
    {
        if (mainTip.Tree["scripts/lib/profiles.sh"]?.Target is not Blob blob)
            throw new InvalidOperationException("scripts/lib/profiles.sh not found on main; cannot determine active tests.");

        var text = blob.GetContentText();
        var profiles = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in ProfileLineRegex().Matches(text))
        {
            var name = m.Groups["name"].Value;
            var spec = m.Groups["spec"].Value;
            var parts = spec.Split('|');
            if (parts.Length < 4) continue;

            var conns = parts[3]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            if (conns.Count == 0) continue;

            profiles[name] = conns;
        }

        if (profiles.Count == 0)
            throw new InvalidOperationException("No profiles parsed from profiles.sh; format may have changed.");

        return profiles;
    }

    [GeneratedRegex("\\[(?<name>[a-z0-9-]+)\\]=\"(?<spec>[^\"]*)\"")]
    private static partial Regex ProfileLineRegex();
}
