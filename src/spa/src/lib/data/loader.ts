import type { DataIndex, FieldLog, TimelineFile } from './types'

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

const fieldCache = new Map<string, Promise<FieldLog | null>>()

/** The test's field log, which every indicator of the test is computed from. */
export function loadFieldLog(test: string): Promise<FieldLog | null> {
  let pending = fieldCache.get(test)
  if (!pending) {
    // Every selected indicator of a test shares one file; cache the request itself so
    // they don't each fetch it.
    pending = fetch(`${base}data/indicators/${encodeURIComponent(test)}.json${v}`)
      .then(res => (res.ok ? (res.json() as Promise<FieldLog>) : null))
      .catch(() => null)
    fieldCache.set(test, pending)
  }
  return pending
}
