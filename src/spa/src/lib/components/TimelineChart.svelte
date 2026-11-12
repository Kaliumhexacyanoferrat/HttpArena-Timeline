<script lang="ts">
  import { onMount, onDestroy } from 'svelte'
  import { get } from 'svelte/store'
  import uPlot from 'uplot'
  import 'uplot/dist/uPlot.min.css'
  import { loadTimeline } from '../data/loader'
  import { loadIndicator } from '../data/field'
  import type { TypeFilter } from '../data/classification'
  import { METRIC_BY_KEY, SCALE_LABEL, formatValue } from '../data/metrics'
  import { parseIndicator, indicatorLabel, type StatKey } from '../data/indicators'
  import { seriesColors, selectedRange } from '../stores/selection'
  import { rangeBounds, rangeBetween, type TimeRange } from '../data/range'
  import { periodStart, nextPeriod, formatPeriod, type Resolution } from '../data/resolution'
  import type { FrameworkInfo, IndicatorScope, Metrics } from '../data/types'

  export let test: string
  export let frameworks: string[]
  export let indicators: string[] = []
  export let scopes: IndicatorScope[] = []
  export let metrics: string[]
  export let resolution: Resolution = 'raw'
  /** The type filter the indicators aggregate over, and the classification to apply it to. */
  export let filter: TypeFilter
  export let frameworkInfo: Record<string, FrameworkInfo> = {}

  let container: HTMLDivElement
  let chart: uPlot | null = null
  let observer: ResizeObserver | null = null
  let status: 'idle' | 'loading' | 'empty' | 'ready' = 'idle'
  // Timestamps of the current chart, to reset the x axis and spot an empty time filter.
  let timestamps: number[] = []
  // Right edge of the full view: the last point, or today when showing periods so the
  // current period gets its width instead of collapsing onto the edge.
  let extentEnd = 0
  let rangeEmpty = false

  // Stale-check key to avoid updating the chart with results from a superseded request.
  let currentKey = ''

  // The chart fills whatever height the layout gives its container (fixed on phones).
  function chartHeight() { return Math.max(240, container?.clientHeight || 360) }

  /** One line of the hover card. */
  interface HoverRow { color: string; label: string; value: string; note?: string; dashed: boolean; indicator: boolean; raw: number }

  // The hover card replaces uPlot's legend: the chips above already name and colour every
  // series, so values only need to appear where the reader is looking.
  let hover: { x: number; y: number; title: string; rows: HoverRow[] } | null = null
  let wrapEl: HTMLDivElement
  let cardWidth = 0
  let cardHeight = 0

  // Per plotted series, what the hover card needs (index matches uPlot's series index - 1).
  let hoverSeries: {
    color: string
    label: string
    scale: string
    dashed: boolean
    indicator: boolean
    values: (number | null)[]
    notes: (string | undefined)[]
    asOf: number[]
  }[] = []
  let hoverTitle: (ts: number) => string = ts => String(ts)
  let hoverByPeriod = false

  function updateHover(u: uPlot) {
    const left = u.cursor.left ?? -1
    const top = u.cursor.top ?? -1
    if (u.cursor.idx == null || left < 0 || !wrapEl) { hover = null; return }
    // uPlot's cursor.idx is the nearest point; with periods, use the period under the cursor.
    const idx = hoverByPeriod ? periodIndex(u.posToVal(left, 'x'), u.cursor.idx) : u.cursor.idx

    const rows: HoverRow[] = []
    for (const s of hoverSeries) {
      // Series rarely share timestamps: show each one's latest value as of the cursor.
      const at = s.asOf[idx]
      if (at < 0) continue
      rows.push({
        color: s.color, label: s.label, dashed: s.dashed, indicator: s.indicator,
        value: formatValue(s.scale, s.values[at]!), note: s.notes[at], raw: s.values[at]!,
      })
    }
    if (rows.length === 0) { hover = null; return }

    // Highest first, so the card reads like the chart from top to bottom.
    rows.sort((a, b) => Number(a.dashed) - Number(b.dashed) || b.raw - a.raw)

    const over = u.over.getBoundingClientRect()
    const wrap = wrapEl.getBoundingClientRect()
    hover = {
      x: over.left - wrap.left + left,
      y: over.top - wrap.top + top,
      title: hoverTitle(timestamps[idx]),
      rows,
    }
  }

  // Keep the card inside the chart: right of the cursor, or left of it near the right edge.
  $: cardLeft = hover ? (hover.x + 16 + cardWidth > (wrapEl?.clientWidth ?? 0) ? hover.x - 16 - cardWidth : hover.x + 16) : 0
  $: cardTop = hover ? Math.max(0, Math.min(hover.y - cardHeight / 2, (wrapEl?.clientHeight ?? 0) - cardHeight)) : 0

  /** A metric's value at one point, plus an optional legend note (who set a min/max, n). */
  type Reading = { v: number | null; note?: string }

  type ReadingFn = (metric: string) => Reading

  /** One line source: a framework, or one statistic of an indicator scope. */
  interface Source {
    colorKey: string
    label: string
    /**
     * Indicators are states that hold until the next snapshot (stepped lines, time-weighted
     * when aggregated); framework points are individual measurements.
     */
    indicator: boolean
    /** How values combine into one point per period. */
    aggregate: 'mean' | 'min' | 'max'
    points: Map<number, ReadingFn>
  }

  const toTs = (iso: string) => Math.floor(new Date(iso).getTime() / 1000)
  const fmtUtc = (d: Date) => `${d.toISOString().slice(0, 16).replace('T', ' ')} UTC`

  async function loadFrameworkSource(fw: string, t: string): Promise<Source | null> {
    const file = await loadTimeline(fw, t)
    if (!file) return null
    const points = new Map<number, ReadingFn>()
    for (const [iso, m] of file.data) {
      points.set(toTs(iso), mk => ({ v: (m[mk as keyof Metrics] as number | null) ?? null }))
    }
    return { colorKey: `fw:${fw}`, label: fw, indicator: false, aggregate: 'mean', points }
  }

  async function loadIndicatorSource(id: string, t: string, f: TypeFilter): Promise<Source | null> {
    const ref = parseIndicator(id)
    if (!ref) return null
    const scope = scopes.find(s => s.key === ref.scope)
    if (!scope) return null
    const file = await loadIndicator(t, scope.key, scope.language, f, frameworkInfo)
    if (!file) return null

    const column = Object.fromEntries(file.metrics.map((k, i) => [k, i]))
    const stat: StatKey = ref.stat
    const points = new Map<number, ReadingFn>()

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
    const aggregate = stat === 'min' || stat === 'max' ? stat : 'mean'
    return { colorKey: `ind:${id}`, label: indicatorLabel(ref, scopes), indicator: true, aggregate, points }
  }

  /**
   * Reduces a source to one point per period, placed at the period start. Framework runs in a
   * period are averaged. Indicator values are weighted by how long they held within the period
   * (including the value carried in from before it); min/max take the extreme held, with who set it.
   */
  function bucket(src: Source, res: Resolution): Source {
    if (res === 'raw' || src.points.size === 0) return src
    const tss = [...src.points.keys()].sort((a, b) => a - b)
    const points = new Map<number, ReadingFn>()

    if (!src.indicator) {
      const groups = new Map<number, number[]>()
      for (const ts of tss) {
        const p = periodStart(ts, res)
        const group = groups.get(p)
        if (group) group.push(ts)
        else groups.set(p, [ts])
      }
      for (const [p, members] of groups) {
        points.set(p, mk => {
          const vs = members.map(ts => src.points.get(ts)!(mk).v).filter((v): v is number => v != null)
          if (vs.length === 0) return { v: null }
          return { v: vs.reduce((a, b) => a + b, 0) / vs.length, note: vs.length > 1 ? `avg of ${vs.length} runs` : undefined }
        })
      }
      return { ...src, points }
    }

    // The latest snapshot still holds today.
    const end = Math.max(Date.now() / 1000, tss[tss.length - 1] + 1)
    let i = 0 // first snapshot at or after the period start
    let lastPeriod: number | null = null
    for (let p = periodStart(tss[0], res); p < end; p = nextPeriod(p, res)) {
      const pEnd = Math.min(nextPeriod(p, res), end)
      while (i < tss.length && tss[i] < p) i++
      const segments: { ts: number; w: number }[] = []
      for (let j = Math.max(0, i - 1); j < tss.length && tss[j] < pEnd; j++) {
        const w = Math.min(tss[j + 1] ?? end, pEnd) - Math.max(tss[j], p)
        if (w > 0) segments.push({ ts: tss[j], w })
      }
      if (segments.length === 0) continue
      points.set(p, mk => combineHeld(segments.map(seg => ({ r: src.points.get(seg.ts)!(mk), w: seg.w })), src.aggregate))
      lastPeriod = p
    }
    // Repeat the current period's value at its end (today) so its step spans its width.
    if (lastPeriod != null && Math.floor(end) > lastPeriod) points.set(Math.floor(end), points.get(lastPeriod)!)
    return { ...src, points }
  }

  function combineHeld(parts: { r: Reading; w: number }[], aggregate: Source['aggregate']): Reading {
    const valid = parts.filter(x => x.r.v != null)
    if (valid.length === 0) return { v: null }
    if (aggregate === 'mean') {
      const weight = valid.reduce((a, x) => a + x.w, 0)
      // n of the latest snapshot: the field as the period ends.
      return { v: valid.reduce((a, x) => a + x.r.v! * x.w, 0) / weight, note: valid[valid.length - 1].r.note }
    }
    let best = valid[0]
    for (const x of valid) {
      if (aggregate === 'min' ? x.r.v! < best.r.v! : x.r.v! > best.r.v!) best = x
    }
    return best.r
  }

  $: rebuild(test, frameworks, indicators, metrics, resolution, filter)

  async function rebuild(t: string, fws: string[], inds: string[], mets: string[], res: Resolution, f: TypeFilter) {
    if (!container) return

    const key = `${t}|${fws.join(',')}|${inds.join(',')}|${mets.join(',')}|${res}|${f.types.join(',')}|${f.showTuned}`
    currentKey = key

    if (!t || (fws.length === 0 && inds.length === 0) || mets.length === 0) {
      destroyChart()
      timestamps = []
      status = 'idle'
      return
    }

    status = 'loading'

    const loaded = await Promise.all([
      ...fws.map(fw => loadFrameworkSource(fw, t)),
      ...inds.map(id => loadIndicatorSource(id, t, f)),
    ])

    if (currentKey !== key) return

    const sources = loaded.filter((s): s is Source => s !== null).map(s => bucket(s, res))
    hover = null

    // Build aligned timestamps across every source.
    const tsSet = new Set<number>()
    for (const s of sources) for (const ts of s.points.keys()) tsSet.add(ts)

    if (tsSet.size === 0) {
      destroyChart()
      timestamps = []
      status = 'empty'
      return
    }

    timestamps = Array.from(tsSet).sort((a, b) => a - b)
    const last = timestamps[timestamps.length - 1]
    extentEnd = res === 'raw' ? last : Math.max(last, Math.floor(Date.now() / 1000))
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

    hoverSeries = series.map(s => ({
      color:     colors[s.source.colorKey] ?? '#94a3b8',
      label:     mets.length > 1 ? `${s.source.label} · ${METRIC_BY_KEY[s.metricKey]?.label ?? s.metricKey}` : s.source.label,
      scale:     METRIC_BY_KEY[s.metricKey]?.scale ?? 'count',
      dashed:    s.dash != null,
      indicator: s.source.indicator,
      values:    s.values,
      notes:     s.notes,
      asOf:      s.asOf,
    }))
    hoverByPeriod = res !== 'raw'
    hoverTitle = ts => res === 'raw' ? fmtUtc(new Date(ts * 1000)) : formatPeriod(periodStart(ts, res), res)

    // Indicators are daily snapshots that hold until the next one, so draw them as steps;
    // this also sets them apart from the framework lines.
    const stepped = uPlot.paths.stepped!({ align: 1 })

    const opts: uPlot.Options = {
      width:  container.clientWidth || 800,
      height: chartHeight(),
      // UTC throughout, matching the time filter and the period boundaries.
      tzDate: ts => uPlot.tzDate(new Date(ts * 1000), 'Etc/UTC'),
      series: [
        { label: 'Time' },
        ...series.map(s => ({
          label:  `${s.source.label} — ${METRIC_BY_KEY[s.metricKey]?.label ?? s.metricKey}`,
          stroke: colors[s.source.colorKey] ?? '#94a3b8',
          width:  s.source.indicator ? 1.5 : 2,
          dash:   s.dash,
          scale:  METRIC_BY_KEY[s.metricKey]?.scale ?? 'count',
          spanGaps: true,
          paths:  s.source.indicator ? stepped : undefined,
          points: s.source.indicator ? { show: false } : undefined,
        }))
      ],
      axes,
      scales,
      // Dragging selects a time range; the selection becomes the time filter (applied by
      // applyRange) instead of uPlot's own zoom, so it shows in the toolbar and the URL.
      cursor: {
        // Vertical guide only; the hover card carries the values.
        y: false,
        drag: { x: true, y: false, setScale: false },
        // With periods, hovering anywhere in one shows that period, not the nearest period start.
        dataIdx: res === 'raw' ? undefined : (_u, _si, closest, xValue) => periodIndex(xValue, closest),
      },
      select: { show: true, left: 0, top: 0, width: 0, height: 0 },
      legend: { show: false },
      hooks: {
        setCursor: [updateHover],
        setSelect: [u => {
          const { left, width } = u.select
          if (width >= 4) {
            selectedRange.set(rangeBetween(u.posToVal(left, 'x'), u.posToVal(left + width, 'x')))
          }
          u.setSelect({ left: 0, top: 0, width: 0, height: 0 }, false)
        }],
      },
    }

    destroyChart()
    chart = new uPlot(opts, data as uPlot.AlignedData, container)
    chart.over.addEventListener('dblclick', () => {
      selectedRange.set(null)
      // uPlot resets the axis to the data on double-click; restore our full view.
      applyRange(null)
    })
    status = 'ready'
    applyRange(get(selectedRange))
  }

  /** Index of the last point at or before x (the period x falls in). */
  function periodIndex(x: number, fallback: number): number {
    let lo = 0, hi = timestamps.length - 1, found = -1
    while (lo <= hi) {
      const mid = (lo + hi) >> 1
      if (timestamps[mid] <= x) { found = mid; lo = mid + 1 } else hi = mid - 1
    }
    return found < 0 ? fallback : found
  }

  $: applyRange($selectedRange)

  function applyRange(r: TimeRange | null) {
    if (!chart || timestamps.length === 0) { rangeEmpty = false; return }
    const [min, max] = r ? rangeBounds(r) : [timestamps[0], extentEnd]
    rangeEmpty = r != null && !timestamps.some(ts => ts >= min && ts <= max)
    chart.setScale('x', { min, max })
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
        label:    SCALE_LABEL[scale] ?? scale,
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
    rebuild(test, frameworks, indicators, metrics, resolution, filter)
  })

  onDestroy(() => {
    destroyChart()
    observer?.disconnect()
  })
</script>

<div class="chart-wrap" bind:this={wrapEl}>
  <div bind:this={container} class="chart-container"></div>

  {#if hover && status === 'ready'}
    <div class="card" style="left: {cardLeft}px; top: {cardTop}px" bind:clientWidth={cardWidth} bind:clientHeight={cardHeight}>
      <div class="card-title">{hover.title}</div>
      {#each hover.rows as row}
        <div class="card-row">
          <svg viewBox="0 0 14 8" aria-hidden="true" style="stroke: {row.color}">
            {#if row.indicator}<path d="M1 7 H5 V1 H9 V4 H13" stroke-dasharray={row.dashed ? '2 2' : undefined} />
            {:else}<path d="M1 4 H13" stroke-dasharray={row.dashed ? '3 2' : undefined} />{/if}
          </svg>
          <span class="card-label">{row.label}</span>
          <span class="card-value">{row.value}</span>
          {#if row.note}<span class="card-note">{row.note}</span>{/if}
        </div>
      {/each}
    </div>
  {/if}

  {#if status === 'loading'}
    <div class="overlay">Loading…</div>
  {:else if status === 'idle'}
    <div class="overlay muted">Add a framework or an indicator to see its history.</div>
  {:else if status === 'empty'}
    <div class="overlay muted">No results for this selection yet.</div>
  {:else if rangeEmpty}
    <div class="overlay muted">No results in the selected time range. Double-click the chart to show everything.</div>
  {/if}
</div>

{#if status === 'ready'}
  <div class="foot">
    <span>Times in UTC</span>
    <span class="drag-hint">Drag across the chart to zoom into a time range{#if $selectedRange}, double-click to show everything{/if}</span>
  </div>
{/if}

<style>
  .chart-wrap {
    position: relative;
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
    padding: 8px 16px 0;
  }

  .chart-container {
    flex: 1;
    min-height: 0;
    width: 100%;
    overflow: hidden;
  }

  @media (max-width: 639px) {
    .chart-wrap { flex: none; }
    .chart-container { flex: none; height: clamp(260px, 52vh, 440px); }
  }

  .foot {
    display: flex;
    justify-content: space-between;
    gap: 12px;
    padding: 2px 24px 12px;
    font-size: 12px;
    color: var(--muted);
  }

  @media (hover: none) {
    .drag-hint { display: none; }
  }

  .card {
    position: absolute;
    z-index: 10;
    min-width: 180px;
    max-width: 340px;
    padding: 8px 10px;
    border-radius: 8px;
    background: rgba(17, 24, 39, .94);
    border: 1px solid var(--border);
    box-shadow: 0 8px 24px rgba(0, 0, 0, .4);
    font-size: 12px;
    pointer-events: none;
  }

  .card-title {
    margin-bottom: 4px;
    color: var(--text-2);
    font-variant-numeric: tabular-nums;
  }

  .card-row {
    display: flex;
    align-items: center;
    gap: 6px;
    line-height: 1.7;
    white-space: nowrap;
  }

  .card-row svg {
    width: 14px;
    height: 8px;
    flex-shrink: 0;
    fill: none;
    stroke-width: 2;
    stroke-linecap: round;
    stroke-linejoin: round;
  }

  .card-label {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    color: var(--text-2);
  }

  .card-value {
    margin-left: 12px;
    color: var(--text);
    font-weight: 600;
    font-variant-numeric: tabular-nums;
  }

  .card-note { color: var(--muted); }

  /* uPlot overrides for dark theme */
  :global(.uplot) {
    background: transparent !important;
    color: #94a3b8;
    font-family: inherit;
  }

  :global(.u-select) {
    background: color-mix(in srgb, var(--accent) 18%, transparent) !important;
    border-left: 1px solid var(--accent);
    border-right: 1px solid var(--accent);
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
