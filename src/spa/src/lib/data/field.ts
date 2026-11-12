import type { FieldLog, FrameworkInfo, IndicatorFile, IndicatorPoint } from './types'
import { passesTypeFilter, type TypeFilter } from './classification'
import { loadFieldLog } from './loader'

const WEEK_MS = 7 * 86400 * 1000

type Values = (number | null)[]

/**
 * Computes an indicator series (median, average, min and max of every metric, with who set
 * the min/max) from a test's field log, for the frameworks `include` accepts.
 *
 * This is the aggregation the importer used to precompute, moved here so it can follow the
 * type filter. Keep the semantics in step with the importer's documentation:
 *   * the log is replayed day by day; each day yields one point for the field as it stood;
 *   * rows with rps <= 0 (failed runs) are left out, and days without any row yield no point;
 *   * frameworks are taken in ordinal name order, so ties for min/max go to the first name,
 *     and min/max name nobody when every framework has the same value;
 *   * an unchanged point is skipped unless a week or more has passed since the last one, so a
 *     flat indicator still reaches into the present.
 */
export function computeIndicator(log: FieldLog, include: (framework: string) => boolean): IndicatorFile {
  const rpsIndex = log.metrics.indexOf('rps')
  const field = new Map<number, Values>()
  const data: [string, IndicatorPoint][] = []
  let last: { ts: number; point: IndicatorPoint } | null = null

  // Which framework indices take part never changes during one replay; decide once.
  const included = log.frameworks.map(fw => include(fw))
  const byName = (a: number, b: number) => {
    const x = log.frameworks[a], y = log.frameworks[b]
    return x < y ? -1 : x > y ? 1 : 0
  }

  for (const [iso, set, removed] of log.days) {
    for (const row of set) field.set(row[0] as number, row.slice(1))
    for (const i of removed) field.delete(i)

    const members = [...field.keys()]
      .filter(i => included[i] && (field.get(i)![rpsIndex] ?? 0) > 0)
      .sort(byName)
    if (members.length === 0) continue

    const point = aggregate(members, field, log.metrics.length)
    const ts = Date.parse(iso)
    if (last && samePoint(last.point, point) && ts - last.ts < WEEK_MS) continue

    data.push([iso, point])
    last = { ts, point }
  }

  return { metrics: log.metrics, frameworks: log.frameworks, data }
}

const computed = new Map<string, IndicatorFile | null>()

/**
 * The indicator series of one scope (all frameworks, or one language when `language` is set)
 * under a type filter, computed once per test, filter and scope.
 */
export async function loadIndicator(
  test: string,
  scope: string,
  language: string | undefined,
  filter: TypeFilter,
  frameworks: Record<string, FrameworkInfo>,
): Promise<IndicatorFile | null> {
  const key = `${test}|${scope}|${[...filter.types].sort().join(',')}|${filter.showTuned}`
  if (computed.has(key)) return computed.get(key)!

  const log = await loadFieldLog(test)
  const result = log
    ? computeIndicator(log, fw => {
        const info = frameworks[fw]
        return passesTypeFilter(info, filter.types, filter.showTuned) && (!language || info?.language === language)
      })
    : null
  computed.set(key, result)
  return result
}

function aggregate(members: number[], field: Map<number, Values>, metricCount: number): IndicatorPoint {
  const point: IndicatorPoint = {
    n: members.length,
    median: [], avg: [], min: [], max: [], min_fw: [], max_fw: [],
  }

  for (let m = 0; m < metricCount; m++) {
    const values: number[] = []
    let min: number | null = null, max: number | null = null
    let minFw: number | null = null, maxFw: number | null = null

    for (const i of members) {
      const v = field.get(i)![m]
      if (v == null || Number.isNaN(v)) continue
      values.push(v)
      if (min === null || v < min) { min = v; minFw = i }
      if (max === null || v > max) { max = v; maxFw = i }
    }

    if (values.length === 0) {
      point.median.push(null); point.avg.push(null); point.min.push(null); point.max.push(null)
      point.min_fw.push(null); point.max_fw.push(null)
      continue
    }

    // When every framework scores the same (e.g. all report zero 3xx), naming one of them
    // as min/max would be arbitrary.
    if (min === max) minFw = maxFw = null

    values.sort((a, b) => a - b)
    const sum = values.reduce((a, b) => a + b, 0)
    const mid = values.length >> 1
    point.median.push(values.length % 2 === 1 ? values[mid] : (values[mid - 1] + values[mid]) / 2)
    point.avg.push(sum / values.length)
    point.min.push(min)
    point.max.push(max)
    point.min_fw.push(minFw)
    point.max_fw.push(maxFw)
  }

  return point
}

const sameArray = <T>(a: T[], b: T[]) => a.length === b.length && a.every((x, i) => x === b[i])

function samePoint(a: IndicatorPoint, b: IndicatorPoint): boolean {
  return a.n === b.n
    && sameArray(a.median, b.median) && sameArray(a.avg, b.avg)
    && sameArray(a.min, b.min) && sameArray(a.max, b.max)
    && sameArray(a.min_fw, b.min_fw) && sameArray(a.max_fw, b.max_fw)
}
