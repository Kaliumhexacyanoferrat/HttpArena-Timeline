import type { DataIndex, IndicatorFile, TimelineFile } from './types'

const base = import.meta.env.BASE_URL
const v = `?v=${__BUILD_TIME__}`

let indexCache: DataIndex | null = null
const timelineCache = new Map<string, TimelineFile | null>()

export async function loadIndex(): Promise<DataIndex> {
  if (indexCache) return indexCache
  const res = await fetch(`${base}data/index.json`, { cache: 'no-cache' })
  if (!res.ok) throw new Error(`Failed to load index: ${res.status}`)
  indexCache = await res.json() as DataIndex
  return indexCache
}

export async function loadTimeline(framework: string, test: string): Promise<TimelineFile | null> {
  const key = `${framework}/${test}`
  if (timelineCache.has(key)) return timelineCache.get(key)!

  const res = await fetch(`${base}data/${encodeURIComponent(framework)}/${encodeURIComponent(test)}.json${v}`)
  const data = res.ok ? (await res.json() as TimelineFile) : null
  timelineCache.set(key, data)
  return data
}

const indicatorCache = new Map<string, Promise<IndicatorFile | null>>()

export function loadIndicator(scope: string, test: string): Promise<IndicatorFile | null> {
  const key = `${scope}/${test}`
  let pending = indicatorCache.get(key)
  if (!pending) {
    // Several selected stats of one scope share a single file; cache the request itself
    // so they don't each fetch it.
    pending = fetch(`${base}data/indicators/${encodeURIComponent(scope)}/${encodeURIComponent(test)}.json${v}`)
      .then(res => (res.ok ? (res.json() as Promise<IndicatorFile>) : null))
      .catch(() => null)
    indicatorCache.set(key, pending)
  }
  return pending
}
