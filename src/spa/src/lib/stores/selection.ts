import { writable, derived, get } from 'svelte/store'
import { FRAMEWORK_COLORS } from '../data/metrics'
import { parseIndicator } from '../data/indicators'

function parseHash(): { test: string; frameworks: string[]; indicators: string[]; metrics: string[]; metricsExplicit: boolean } {
  try {
    const params = new URLSearchParams(window.location.hash.slice(1))
    return {
      test:       params.get('test') ?? '',
      frameworks: params.get('frameworks')?.split(',').filter(Boolean) ?? [],
      indicators: params.get('indicators')?.split(',').filter(id => parseIndicator(id) !== null) ?? [],
      metrics:    params.get('metrics')?.split(',').filter(Boolean) ?? ['rps'],
      // A metrics param in the URL means the user (or a shared link) picked metrics
      // explicitly — don't override those with per-test defaults.
      metricsExplicit: params.has('metrics'),
    }
  } catch {
    return { test: '', frameworks: [], indicators: [], metrics: ['rps'], metricsExplicit: false }
  }
}

const initial = parseHash()

export const selectedTest       = writable(initial.test)
export const selectedFrameworks = writable<string[]>(initial.frameworks)
export const selectedMetrics    = writable<string[]>(initial.metrics)
// Indicator ids such as "median.all" or "max.rust" (see data/indicators.ts).
export const selectedIndicators = writable<string[]>(initial.indicators)

// Frameworks and indicators draw from one palette, frameworks first, so a chip and its
// line always share a color. Keys are "fw:<name>" and "ind:<id>".
export const seriesColors = derived(
  [selectedFrameworks, selectedIndicators],
  ([$fws, $inds]) => {
    const keys = [...$fws.map(f => `fw:${f}`), ...$inds.map(i => `ind:${i}`)]
    return Object.fromEntries(keys.map((k, i) => [k, FRAMEWORK_COLORS[i % FRAMEWORK_COLORS.length]])) as Record<string, string>
  }
)

// True once the user has manually toggled a metric (or arrived via a link that
// pins metrics). While false, switching tests applies that test's default metrics.
export const metricsTouched     = writable(initial.metricsExplicit)

export function markMetricsTouched(): void {
  metricsTouched.set(true)
}

// Some tests are not about throughput. For fixed-rate / bandwidth-saturation tests
// (8gbit, latency) the score is driven by CPU cost per request and tail latency, so
// default to those metrics rather than RPS.
const TEST_METRIC_DEFAULTS: { match: (test: string) => boolean; metrics: string[] }[] = [
  {
    match: t => t.startsWith('8gbit') || t.startsWith('latency'),
    metrics: ['cpu_per_req_us', 'p99_latency_ms', 'p99_9_latency_ms'],
  },
]

export function defaultMetricsForTest(test: string): string[] {
  for (const d of TEST_METRIC_DEFAULTS) {
    if (d.match(test)) return d.metrics
  }
  return ['rps']
}

export function syncUrl(): void {
  const test      = get(selectedTest)
  const fws       = get(selectedFrameworks)
  const inds      = get(selectedIndicators)
  const metrics   = get(selectedMetrics)

  const params = new URLSearchParams()
  if (test)       params.set('test', test)
  if (fws.length) params.set('frameworks', fws.join(','))
  if (inds.length) params.set('indicators', inds.join(','))
  const ms = metrics.join(',')
  // Persist metrics whenever they were explicitly chosen, or differ from the plain
  // rps default, so a shared link reproduces exactly what is on screen.
  if (ms && (get(metricsTouched) || ms !== 'rps')) params.set('metrics', ms)

  const hash = params.toString()
  history.replaceState(null, '', hash ? `#${hash}` : location.pathname + location.search)
}
