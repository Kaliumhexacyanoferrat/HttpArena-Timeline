<script lang="ts">
  import { onMount, onDestroy } from 'svelte'
  import { get } from 'svelte/store'
  import uPlot from 'uplot'
  import 'uplot/dist/uPlot.min.css'
  import { loadTimeline, loadIndicator } from '../data/loader'
  import { METRIC_BY_KEY, formatValue } from '../data/metrics'
  import { parseIndicator, indicatorLabel, type StatKey } from '../data/indicators'
  import { seriesColors } from '../stores/selection'
  import type { IndicatorScope, Metrics } from '../data/types'

  export let test: string
  export let frameworks: string[]
  export let indicators: string[] = []
  export let scopes: IndicatorScope[] = []
  export let metrics: string[]

  let container: HTMLDivElement
  let chart: uPlot | null = null
  let observer: ResizeObserver | null = null
  let status: 'idle' | 'loading' | 'empty' | 'ready' = 'idle'

  // Stale-check key to avoid updating the chart with results from a superseded request.
  let currentKey = ''

  function chartHeight() { return window.innerWidth < 640 ? 260 : 360 }

  /** A metric's value at one point, plus an optional legend note (who set a min/max, n). */
  type Reading = { v: number | null; note?: string }

  /** One line source: a framework, or one statistic of an indicator scope. */
  interface Source {
    colorKey: string
    label: string
    indicator: boolean
    points: Map<number, (metric: string) => Reading>
  }

  const toTs = (iso: string) => Math.floor(new Date(iso).getTime() / 1000)

  async function loadFrameworkSource(fw: string, t: string): Promise<Source | null> {
    const file = await loadTimeline(fw, t)
    if (!file) return null
    const points = new Map<number, (metric: string) => Reading>()
    for (const [iso, m] of file.data) {
      points.set(toTs(iso), mk => ({ v: (m[mk as keyof Metrics] as number | null) ?? null }))
    }
    return { colorKey: `fw:${fw}`, label: fw, indicator: false, points }
  }

  async function loadIndicatorSource(id: string, t: string): Promise<Source | null> {
    const ref = parseIndicator(id)
    if (!ref) return null
    const file = await loadIndicator(ref.scope, t)
    if (!file) return null

    const column = Object.fromEntries(file.metrics.map((k, i) => [k, i]))
    const stat: StatKey = ref.stat
    const points = new Map<number, (metric: string) => Reading>()

    for (const [iso, p] of file.data) {
      points.set(toTs(iso), mk => {
        const i = column[mk]
        if (i === undefined) return { v: null }
        const v = p[stat][i] ?? null
        if (stat === 'min' || stat === 'max') {
          const who = p[`${stat}_fw`][i]
          return { v, note: who != null ? file.frameworks[who] : undefined }
        }
        return { v, note: `n=${p.n}` }
      })
    }
    return { colorKey: `ind:${id}`, label: indicatorLabel(ref, scopes), indicator: true, points }
  }

  $: rebuild(test, frameworks, indicators, metrics)

  async function rebuild(t: string, fws: string[], inds: string[], mets: string[]) {
    if (!container) return

    const key = `${t}|${fws.join(',')}|${inds.join(',')}|${mets.join(',')}`
    currentKey = key

    if (!t || (fws.length === 0 && inds.length === 0) || mets.length === 0) {
      destroyChart()
      status = 'idle'
      return
    }

    status = 'loading'

    const loaded = await Promise.all([
      ...fws.map(fw => loadFrameworkSource(fw, t)),
      ...inds.map(id => loadIndicatorSource(id, t)),
    ])

    if (currentKey !== key) return

    const sources = loaded.filter((s): s is Source => s !== null)

    // Build aligned timestamps across every source.
    const tsSet = new Set<number>()
    for (const s of sources) for (const ts of s.points.keys()) tsSet.add(ts)

    if (tsSet.size === 0) {
      destroyChart()
      status = 'empty'
      return
    }

    const timestamps = Array.from(tsSet).sort((a, b) => a - b)
    const colors = get(seriesColors)

    // One series per (source, metric) pair; the second and later metrics are dashed.
    const series: {
      source: Source
      metricKey: string
      values: (number | null)[]
      notes: (string | undefined)[]
      asOf: number[] // per timestamp: index of the latest non-null value up to it, or -1
      dash?: number[]
    }[] = []

    for (const source of sources) {
      mets.forEach((mk, mi) => {
        const readings = timestamps.map(ts => source.points.get(ts)?.(mk))
        const values = readings.map(r => r?.v ?? null)
        let last = -1
        series.push({
          source,
          metricKey: mk,
          values,
          notes:  readings.map(r => r?.note),
          asOf:   values.map((v, i) => (v != null ? (last = i) : last)),
          dash:   mi > 0 ? [4, 4] : undefined,
        })
      })
    }

    // Build uPlot aligned data: [timestamps, ...valueArrays]
    const data: (number | null)[][] = [timestamps, ...series.map(s => s.values)]

    // Determine the set of unique scales for Y-axis construction.
    const activeScaleKeys = [...new Set(mets.map(mk => METRIC_BY_KEY[mk]?.scale ?? 'count'))]

    const { axes, scales } = buildAxesAndScales(activeScaleKeys)

    // Indicators are daily snapshots that hold until the next one, so draw them as steps;
    // this also sets them apart from the framework lines.
    const stepped = uPlot.paths.stepped!({ align: 1 })

    const opts: uPlot.Options = {
      width:  container.clientWidth || 800,
      height: chartHeight(),
      series: [
        { label: 'Time' },
        ...series.map(s => {
          const scale = METRIC_BY_KEY[s.metricKey]?.scale ?? 'count'
          return {
            label:  `${s.source.label} — ${METRIC_BY_KEY[s.metricKey]?.label ?? s.metricKey}`,
            stroke: colors[s.source.colorKey] ?? '#94a3b8',
            width:  s.source.indicator ? 1.5 : 2,
            dash:   s.dash,
            scale,
            spanGaps: true,
            paths:  s.source.indicator ? stepped : undefined,
            points: s.source.indicator ? { show: false } : undefined,
            // Series rarely share timestamps, so show each one's latest value as of the
            // cursor rather than a dash whenever it has no point exactly there.
            value:  (_u: uPlot, _v: number | null, _si: number, idx: number | null) => {
              const at = idx != null ? s.asOf[idx] : -1
              if (at < 0) return '–'
              const text = formatValue(scale, s.values[at]!)
              return s.notes[at] ? `${text} · ${s.notes[at]}` : text
            },
          }
        })
      ],
      axes,
      scales,
      cursor: { drag: { x: false, y: false } },
      legend: { show: true },
    }

    destroyChart()
    chart = new uPlot(opts, data as uPlot.AlignedData, container)
    status = 'ready'
  }

  function buildAxesAndScales(scaleKeys: string[]) {
    const GRID  = { stroke: '#1e293b', width: 1 }
    const TICKS = { stroke: '#334155', width: 1, size: 4 }

    const axes: uPlot.Axis[] = [
      {
        // x — time
        stroke: '#64748b',
        grid:   GRID,
        ticks:  TICKS,
      }
    ]

    const scales: Record<string, uPlot.Scale> = {
      x: { time: true },
    }

    scaleKeys.forEach((scale, i) => {
      const isLeft = i === 0
      axes.push({
        scale,
        label:    scale,
        side:     isLeft ? 3 : 1,
        stroke:   '#64748b',
        grid:     isLeft ? GRID : { show: false },
        ticks:    TICKS,
        gap:      i > 1 ? (i - 1) * 52 : 0,
        values:   (_u: uPlot, ticks: number[]) => ticks.map(v => formatValue(scale, v)),
        size:     52,
      })
      scales[scale] = { auto: true }
    })

    return { axes, scales }
  }

  function destroyChart() {
    if (chart) { chart.destroy(); chart = null }
  }

  onMount(() => {
    observer = new ResizeObserver(() => {
      if (chart && container) {
        chart.setSize({ width: container.clientWidth, height: chartHeight() })
      }
    })
    observer.observe(container)
    rebuild(test, frameworks, indicators, metrics)
  })

  onDestroy(() => {
    destroyChart()
    observer?.disconnect()
  })
</script>

<div class="chart-wrap">
  <div bind:this={container} class="chart-container"></div>
  {#if status === 'loading'}
    <div class="overlay">Loading…</div>
  {:else if status === 'idle'}
    <div class="overlay muted">Select a test and at least one framework or indicator.</div>
  {:else if status === 'empty'}
    <div class="overlay muted">No data for this combination.</div>
  {/if}
</div>

<style>
  .chart-wrap {
    position: relative;
    flex: 1;
    min-height: 0;
    padding: 12px 16px;
  }

  @media (max-width: 639px) {
    .chart-wrap { flex: none; }
  }

  .chart-container {
    width: 100%;
  }

  /* uPlot overrides for dark theme */
  :global(.uplot) {
    background: transparent !important;
    color: #94a3b8;
    font-family: inherit;
  }

  :global(.u-legend) {
    font-size: 12px !important;
    background: rgba(15, 23, 42, 0.85) !important;
    backdrop-filter: blur(6px);
    -webkit-backdrop-filter: blur(6px);
    border: 1px solid #334155;
    border-radius: 6px;
    padding: 4px 8px;
  }

  :global(.u-legend th) {
    font-weight: 400;
    color: #94a3b8;
  }

  :global(.u-legend td) {
    color: #e2e8f0;
  }

  .overlay {
    position: absolute;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 14px;
    color: #e2e8f0;
    background: rgba(15, 23, 42, 0.7);
    border-radius: 4px;
    pointer-events: none;
  }

  .overlay.muted {
    color: #64748b;
    background: transparent;
  }
</style>
