import { writable, derived, get } from 'svelte/store'
import { FRAMEWORK_COLORS } from '../data/metrics'
import { parseIndicator } from '../data/indicators'
import { parseRange, type TimeRange } from '../data/range'
import { parseResolution, type Resolution } from '../data/resolution'
import { DEFAULT_TYPES, DEFAULT_SHOW_TUNED, isDefaultFilter, normalizeTypes, type TypeFilter } from '../data/classification'

function parseHash(): { test: string; frameworks: string[]; indicators: string[]; range: TimeRange | null; resolution: Resolution; types: string[] | null; tuned: boolean | null; metrics: string[]; metricsExplicit: boolean } {
  try {
    const params = new URLSearchParams(window.location.hash.slice(1))
    return {
      test:       params.get('test') ?? '',
      frameworks: params.get('frameworks')?.split(',').filter(Boolean) ?? [],
      indicators: params.get('indicators')?.split(',').filter(id => parseIndicator(id) !== null) ?? [],
      range:      parseRange(params.get('from'), params.get('to')),
      resolution: parseResolution(params.get('res')),
      types:      normalizeTypes(params.get('type')?.split(',')),
      tuned:      params.has('tuned') ? params.get('tuned') !== '0' : null,
      metrics:    params.get('metrics')?.split(',').filter(Boolean) ?? ['rps'],
      // A metrics param in the URL means the user (or a shared link) picked metrics
      // explicitly — don't override those with per-test defaults.
      metricsExplicit: params.has('metrics'),
    }
  } catch {
    return { test: '', frameworks: [], indicators: [], range: null, resolution: 'raw', types: null, tuned: null, metrics: ['rps'], metricsExplicit: false }
  }
}

const initial = parseHash()

export const selectedTest       = writable(initial.test)
export const selectedFrameworks = writable<string[]>(initial.frameworks)
export const selectedMetrics    = writable<string[]>(initial.metrics)
// Indicator ids such as "median.all" or "max.rust" (see data/indicators.ts).
export const selectedIndicators = writable<string[]>(initial.indicators)
// Time filter, set by dragging across the chart; null shows the whole history.
export const selectedRange      = writable<TimeRange | null>(initial.range)
// One point per period (week, month, …), aggregated client-side; 'raw' shows every data point.
export const selectedResolution = writable<Resolution>(initial.resolution)

// HttpArena's type filter, global like on the board: it narrows the framework picker and
// decides which frameworks the indicators aggregate. As on the board, the last choice is
// remembered in this browser, and a link carries it (type=, tuned=) when it differs from
// the default, so shared indicator values reproduce.
export type { TypeFilter }

// v2: the first version stored its default on every page load, not just a user's choice.
const TYPE_FILTER_KEY = 'timeline-type-filter-v2'

function loadTypeFilter(): TypeFilter {
  let filter: TypeFilter = { types: DEFAULT_TYPES, showTuned: DEFAULT_SHOW_TUNED }
  try {
    const saved = JSON.parse(localStorage.getItem(TYPE_FILTER_KEY) ?? 'null')
    filter = {
      types: normalizeTypes(saved?.types) ?? filter.types,
      showTuned: typeof saved?.showTuned === 'boolean' ? saved.showTuned : filter.showTuned,
    }
  } catch { /* unreadable: keep the default */ }
  // A link wins over what this browser remembers.
  return { types: initial.types ?? filter.types, showTuned: initial.tuned ?? filter.showTuned }
}

export const typeFilter = writable<TypeFilter>(loadTypeFilter())
// Remember changes only, so a visitor who never touches the filter follows the default.
let storedInitial = false
typeFilter.subscribe(v => {
  if (!storedInitial) { storedInitial = true; return }
  try { localStorage.setItem(TYPE_FILTER_KEY, JSON.stringify(v)) } catch { /* private mode */ }
})

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
  const range     = get(selectedRange)
  const res       = get(selectedResolution)
  const filter    = get(typeFilter)
  const metrics   = get(selectedMetrics)

  const params = new URLSearchParams()
  if (test)       params.set('test', test)
  if (fws.length) params.set('frameworks', fws.join(','))
  if (inds.length) params.set('indicators', inds.join(','))
  if (range) { params.set('from', range.from); params.set('to', range.to) }
  if (res !== 'raw') params.set('res', res)
  // Same names as the board's hash. Written whenever the filter differs from the default:
  // indicators depend on it, so a link must say which one it was made with.
  if (!isDefaultFilter(filter)) {
    params.set('type', [...filter.types].sort().join(','))
    params.set('tuned', filter.showTuned ? '1' : '0')
  }
  const ms = metrics.join(',')
  // Persist metrics whenever they were explicitly chosen, or differ from the plain
  // rps default, so a shared link reproduces exactly what is on screen.
  if (ms && (get(metricsTouched) || ms !== 'rps')) params.set('metrics', ms)

  const hash = params.toString()
  history.replaceState(null, '', hash ? `#${hash}` : location.pathname + location.search)
}
