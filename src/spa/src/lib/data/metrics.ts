export interface MetricConfig {
  key: string
  label: string
  scale: string
}

export const METRIC_CONFIGS: MetricConfig[] = [
  { key: 'rps',           label: 'RPS',          scale: 'rps'   },
  { key: 'avg_latency_ms',label: 'Avg Latency',   scale: 'ms'    },
  { key: 'p99_latency_ms',label: 'P99 Latency',   scale: 'ms'    },
  { key: 'cpu_pct',       label: 'CPU',           scale: 'pct'   },
  { key: 'memory_bytes',  label: 'Memory',        scale: 'bytes' },
  { key: 'bandwidth_bps', label: 'Bandwidth',     scale: 'bps'   },
  { key: 'input_bw_bps',  label: 'Input BW',      scale: 'bps'   },
  { key: 'reconnects',    label: 'Reconnects',    scale: 'count' },
  { key: 'status_2xx',    label: '2xx',           scale: 'count' },
  { key: 'status_3xx',    label: '3xx',           scale: 'count' },
  { key: 'status_4xx',    label: '4xx',           scale: 'count' },
  { key: 'status_5xx',    label: '5xx',           scale: 'count' },
]

export const METRIC_BY_KEY = Object.fromEntries(METRIC_CONFIGS.map(m => [m.key, m]))

export const SCALE_LABEL: Record<string, string> = {
  rps:   'req/s',
  ms:    'ms',
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
      return v >= 1000 ? `${(v / 1000).toFixed(2)}s` : `${v.toFixed(2)}ms`
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
