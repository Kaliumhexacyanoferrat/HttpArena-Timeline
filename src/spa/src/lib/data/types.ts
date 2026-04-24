export interface Metrics {
  rps: number
  avg_latency_ms: number | null
  p99_latency_ms: number | null
  cpu_pct: number | null
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
}

export interface DataIndex {
  frameworks: Record<string, FrameworkInfo>
  tests: string[]
}
