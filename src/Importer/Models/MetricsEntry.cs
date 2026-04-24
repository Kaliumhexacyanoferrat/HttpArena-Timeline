namespace Importer.Models;

public record MetricsEntry(
    long Rps,
    double? AvgLatencyMs,
    double? P99LatencyMs,
    double? CpuPct,
    long? MemoryBytes,
    int Connections,
    int Threads,
    double? DurationMs,
    int Pipeline,
    double? BandwidthBytesPerSec,
    double? InputBwBytesPerSec,
    int Reconnects,
    int Status2xx,
    int Status3xx,
    int Status4xx,
    int Status5xx
);
