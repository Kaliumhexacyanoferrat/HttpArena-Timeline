using System.Globalization;

namespace Importer.Services;

public static class ValueParser
{
    private static readonly CultureInfo C = CultureInfo.InvariantCulture;

    public static double? ParseLatencyMs(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.EndsWith("ms") && double.TryParse(value[..^2], C, out var ms)) return ms;
        if (value.EndsWith("us") && double.TryParse(value[..^2], C, out var us)) return us / 1000.0;
        if (value.EndsWith("s")  && double.TryParse(value[..^1], C, out var s))  return s * 1000.0;
        return null;
    }

    public static double? ParseDurationMs(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.EndsWith("ms") && double.TryParse(value[..^2], C, out var ms)) return ms;
        if (value.EndsWith("s")  && double.TryParse(value[..^1], C, out var s))  return s * 1000.0;
        return null;
    }

    public static long? ParseMemoryBytes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.EndsWith("GiB") && double.TryParse(value[..^3], C, out var g)) return (long)(g * 1024 * 1024 * 1024);
        if (value.EndsWith("MiB") && double.TryParse(value[..^3], C, out var m)) return (long)(m * 1024 * 1024);
        if (value.EndsWith("KiB") && double.TryParse(value[..^3], C, out var k)) return (long)(k * 1024);
        return null;
    }

    // Returns bytes/sec, or null for values that are total bytes (no /s) or unrecognized.
    public static double? ParseBandwidthBytesPerSec(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value == "0") return 0.0;
        if (value.EndsWith("GB/s") && double.TryParse(value[..^4], C, out var gb)) return gb * 1e9;
        if (value.EndsWith("MB/s") && double.TryParse(value[..^4], C, out var mb)) return mb * 1e6;
        if (value.EndsWith("KB/s") && double.TryParse(value[..^4], C, out var kb)) return kb * 1e3;
        if (value.EndsWith("B/s")  && double.TryParse(value[..^3], C, out var b))  return b;
        return null;
    }

    public static double? ParseCpuPct(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.EndsWith('%') && double.TryParse(value[..^1], C, out var pct)) return pct;
        return null;
    }
}
