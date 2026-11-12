export interface Metrics {
  rps: number
  avg_latency_ms: number | null
  p99_latency_ms: number | null
  p99_9_latency_ms: number | null
  cpu_pct: number | null
  cpu_per_req_us: number | null
  memory_bytes: number | null
  connections: number
  threads: number
  duration_ms: number | null
  pipeline: number
  bandwidth_bps: number | null
  input_bw_bps: number | null
  reconnects: number
  status_2xx: number
  status_3xx: number
  status_4xx: number
  status_5xx: number
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
