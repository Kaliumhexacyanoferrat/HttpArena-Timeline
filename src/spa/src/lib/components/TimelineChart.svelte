<script lang="ts">
  import { onMount, onDestroy } from 'svelte'
  import uPlot from 'uplot'
  import 'uplot/dist/uPlot.min.css'
  import { loadTimeline } from '../data/loader'
  import { METRIC_BY_KEY, FRAMEWORK_COLORS, formatValue } from '../data/metrics'
  import type { Metrics } from '../data/types'

  export let test: string
  export let frameworks: string[]
  export let metrics: string[]

  let container: HTMLDivElement
  let chart: uPlot | null = null
  let observer: ResizeObserver | null = null
  let status: 'idle' | 'loading' | 'empty' | 'ready' = 'idle'

  // Stale-check key to avoid updating the chart with results from a superseded request.
  let currentKey = ''

  function chartHeight() { return window.innerWidth < 640 ? 260 : 360 }

  $: rebuild(test, frameworks, metrics)

  async function rebuild(t: string, fws: string[], mets: string[]) {
    if (!container) return

    const key = `${t}|${fws.join(',')}|${mets.join(',')}`
    currentKey = key

    if (!t || fws.length === 0 || mets.length === 0) {
      destroyChart()
      status = 'idle'
      return
    }

    status = 'loading'

    const results = await Promise.all(
      fws.map(fw => loadTimeline(fw, t).then(data => ({ fw, data })))
    )

    if (currentKey !== key) return

    // Build aligned timestamp + value arrays.
    const tsSet = new Set<number>()
    const fwPoints = new Map<string, Map<number, Metrics>>()

    for (const { fw, data } of results) {
      if (!data) continue
      const map = new Map<number, Metrics>()
      for (const [iso, m] of data.data) {
        const ts = Math.floor(new Date(iso).getTime() / 1000)
        tsSet.add(ts)
        map.set(ts, m)
      }
      fwPoints.set(fw, map)
    }

    if (tsSet.size === 0) {
      destroyChart()
      status = 'empty'
      return
    }

    const timestamps = Array.from(tsSet).sort((a, b) => a - b)

    // One series descriptor per (framework, metric) pair.
    const series: { fw: string; metricKey: string; color: string; dash?: number[] }[] = []

    for (const fw of fws) {
      if (!fwPoints.has(fw)) continue
      const color = FRAMEWORK_COLORS[fws.indexOf(fw) % FRAMEWORK_COLORS.length]
      mets.forEach((mk, mi) => {
        series.push({ fw, metricKey: mk, color, dash: mi > 0 ? [4, 4] : undefined })
      })
    }

    // Build uPlot aligned data: [timestamps, ...valueArrays]
    const data: (number | null | undefined)[][] = [timestamps]
    for (const { fw, metricKey } of series) {
      const map = fwPoints.get(fw)!
      data.push(timestamps.map(ts => {
        const m = map?.get(ts)
        return m ? (m[metricKey as keyof Metrics] as number | null) : null
      }))
    }

    // Determine the set of unique scales for Y-axis construction.
    const activeScaleKeys = [...new Set(mets.map(mk => METRIC_BY_KEY[mk]?.scale ?? 'count'))]

    const { axes, scales } = buildAxesAndScales(activeScaleKeys)

    const opts: uPlot.Options = {
      width:  container.clientWidth || 800,
      height: chartHeight(),
      series: [
        { label: 'Time' },
        ...series.map(s => ({
          label:  `${s.fw} — ${METRIC_BY_KEY[s.metricKey]?.label ?? s.metricKey}`,
          stroke: s.color,
          width:  2,
          dash:   s.dash,
          scale:  METRIC_BY_KEY[s.metricKey]?.scale ?? 'count',
          spanGaps: true,
        }))
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
    rebuild(test, frameworks, metrics)
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
    <div class="overlay muted">Select a test and at least one framework.</div>
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
