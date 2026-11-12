export interface MetricConfig {
  key: string
  label: string
  scale: string
  group: string
}

export const METRIC_GROUPS = ['Throughput', 'Latency', 'Resources', 'Responses']

export const METRIC_CONFIGS: MetricConfig[] = [
  { key: 'rps',              label: 'RPS',             scale: 'rps',   group: 'Throughput' },
  { key: 'bandwidth_bps',    label: 'Bandwidth',       scale: 'bps',   group: 'Throughput' },
  { key: 'input_bw_bps',     label: 'Input bandwidth', scale: 'bps',   group: 'Throughput' },
  { key: 'avg_latency_ms',   label: 'Avg latency',     scale: 'ms',    group: 'Latency' },
  { key: 'p99_latency_ms',   label: 'P99 latency',     scale: 'ms',    group: 'Latency' },
  { key: 'p99_9_latency_ms', label: 'P99.9 latency',   scale: 'ms',    group: 'Latency' },
  { key: 'cpu_pct',          label: 'CPU',             scale: 'pct',   group: 'Resources' },
  { key: 'cpu_per_req_us',   label: 'CPU per request', scale: 'us',    group: 'Resources' },
  { key: 'memory_bytes',     label: 'Memory',          scale: 'bytes', group: 'Resources' },
  { key: 'status_2xx',       label: '2xx responses',   scale: 'count', group: 'Responses' },
  { key: 'status_3xx',       label: '3xx responses',   scale: 'count', group: 'Responses' },
  { key: 'status_4xx',       label: '4xx responses',   scale: 'count', group: 'Responses' },
  { key: 'status_5xx',       label: '5xx responses',   scale: 'count', group: 'Responses' },
  { key: 'reconnects',       label: 'Reconnects',      scale: 'count', group: 'Responses' },
]

export const METRIC_BY_KEY = Object.fromEntries(METRIC_CONFIGS.map(m => [m.key, m]))

export const SCALE_LABEL: Record<string, string> = {
  rps:   'req/s',
  ms:    'ms',
  us:    'µs',
  pct:   '%',
  bytes: 'bytes',
  bps:   'bytes/s',
  count: 'count',
}

export function formatValue(scale: string, v: number): string {
  switch (scale) {
    case 'rps':
      return v >= 1e6 ? `${(v / 1e6).toFixed(1)}M` : v >= 1e3 ? `${(v / 1e3).toFixed(0)}K` : `${Math.round(v)}`
    case 'ms':
      return v >= 1000 ? `${trim(v / 1000)}s` : `${trim(v)}ms`
    case 'us':
      return v >= 1000 ? `${trim(v / 1000)}ms` : `${trim(v)}µs`
    case 'pct':
      return `${v.toFixed(1)}%`
    case 'bytes':
      if (v >= 1073741824) return `${(v / 1073741824).toFixed(1)} GiB`
      if (v >= 1048576)    return `${(v / 1048576).toFixed(0)} MiB`
      if (v >= 1024)       return `${(v / 1024).toFixed(0)} KiB`
      return `${v} B`
    case 'bps':
      if (v >= 1e9) return `${(v / 1e9).toFixed(1)} GB/s`
      if (v >= 1e6) return `${(v / 1e6).toFixed(0)} MB/s`
      if (v >= 1e3) return `${(v / 1e3).toFixed(0)} KB/s`
      return `${v} B/s`
    case 'count':
      return v >= 1e6 ? `${(v / 1e6).toFixed(1)}M` : v >= 1e3 ? `${(v / 1e3).toFixed(0)}K` : `${Math.round(v)}`
    default:
      return String(v)
  }
}

export const FRAMEWORK_COLORS = [
  '#3b82f6', // blue
  '#ef4444', // red
  '#10b981', // emerald
  '#f59e0b', // amber
  '#8b5cf6', // violet
  '#06b6d4', // cyan
  '#f97316', // orange
  '#84cc16', // lime
  '#ec4899', // pink
  '#14b8a6', // teal
  '#a855f7', // purple
  '#eab308', // yellow
]

/** Three significant digits at most, without trailing zeros: 0.041, 4.12, 41.2, 412. */
function trim(v: number): string {
  const digits = v >= 100 ? 0 : v >= 10 ? 1 : v >= 1 ? 2 : 3
  return String(Number(v.toFixed(digits)))
}
