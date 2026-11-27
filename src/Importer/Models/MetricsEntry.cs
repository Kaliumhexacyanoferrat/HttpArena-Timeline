namespace Importer.Models;

public record MetricsEntry(
    long Rps,
    double? AvgLatencyMs,
    double? P50LatencyMs,
    double? P99LatencyMs,
    double? P999LatencyMs,
    double? CpuPct,
    double? CpuPerReqUs,
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
    int Status5xx,
    // Fixed-rate profiles only (8gbit, latency, connections); null elsewhere.
    double? TargetRate,
    double? RateRatio,
    long? ConnectionsOpen,
    long? WindowConnectErrors
);
