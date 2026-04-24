import type { DataIndex, TimelineFile } from './types'

const base = import.meta.env.BASE_URL

let indexCache: DataIndex | null = null
const timelineCache = new Map<string, TimelineFile | null>()

export async function loadIndex(): Promise<DataIndex> {
  if (indexCache) return indexCache
  const res = await fetch(`${base}data/index.json`)
  if (!res.ok) throw new Error(`Failed to load index: ${res.status}`)
  indexCache = await res.json() as DataIndex
  return indexCache
}

export async function loadTimeline(framework: string, test: string): Promise<TimelineFile | null> {
  const key = `${framework}/${test}`
  if (timelineCache.has(key)) return timelineCache.get(key)!

  const res = await fetch(`${base}data/${encodeURIComponent(framework)}/${encodeURIComponent(test)}.json`)
  const data = res.ok ? (await res.json() as TimelineFile) : null
  timelineCache.set(key, data)
  return data
}
