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
    // Held-connection profiles (connections-1m, -1m-tls, -5m) only; null elsewhere and not written then.
    double? TargetRate,
    double? RateRatio,
    long? ConnectionsOpen,
    long? WindowConnectErrors
);
