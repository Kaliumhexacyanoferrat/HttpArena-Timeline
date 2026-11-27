/** A measurement. The importer leaves out fields without a value, so most are optional. */
export interface Metrics {
  rps: number
  avg_latency_ms?: number
  /** Reported by the held-connection profiles (connections-*) only. */
  p50_latency_ms?: number
  p99_latency_ms?: number
  /** Fixed-rate profiles only, like cpu_per_req_us. */
  p99_9_latency_ms?: number
  cpu_pct?: number
  cpu_per_req_us?: number
  memory_bytes?: number
  connections: number
  threads: number
  duration_ms?: number
  pipeline: number
  bandwidth_bps?: number
  input_bw_bps?: number
  reconnects: number
  status_2xx: number
  status_3xx: number
  status_4xx: number
  status_5xx: number
  // Fixed-rate profiles only.
  target_rate?: number
  rate_ratio?: number
  connections_open?: number
  window_connect_errors?: number
}

export interface TimelineFile {
  data: [string, Metrics][]
}

export interface FrameworkInfo {
  language: string
  tests: string[]
  /** HttpArena tier: flagship, emerging, experimental, engine or infrastructure. */
  type?: string
  /** Set for entries in HttpArena's "tuned" mode (non-default configuration). */
  tuned?: boolean
}


/** An aggregation scope: every framework ("all") or the frameworks of one language. */
export interface IndicatorScope {
  key: string
  label: string
  language?: string
}

export interface DataIndex {
  frameworks: Record<string, FrameworkInfo>
  tests: string[]
  indicators?: IndicatorScope[]
}

/**
 * data/indicators/<test>.json: how the test's field of results changed, one entry per UTC
 * day: [timestamp, rows set that day as [frameworkIndex, value per metric...], frameworks
 * removed that day]. Indicators are aggregated from it in the browser (see data/field.ts).
 */
export interface FieldLog {
  metrics: string[]
  frameworks: string[]
  days: [string, (number | null)[][], number[]][]
}

/** One daily aggregate of a scope; each array holds one value per entry of `metrics`. */
export interface IndicatorPoint {
  n: number
  median: (number | null)[]
  avg: (number | null)[]
  min: (number | null)[]
  max: (number | null)[]
  /** Indices into `frameworks`: who set the min/max (null when everyone tied). */
  min_fw: (number | null)[]
  max_fw: (number | null)[]
}

export interface IndicatorFile {
  metrics: string[]
  frameworks: string[]
  data: [string, IndicatorPoint][]
}
