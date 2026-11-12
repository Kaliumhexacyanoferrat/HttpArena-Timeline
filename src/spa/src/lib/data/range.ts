/** A time filter: whole UTC days, both ends inclusive, written as YYYY-MM-DD (also in URLs). */
export interface TimeRange {
  from: string
  to: string
}

const DAY = 86400
const DATE = /^\d{4}-\d{2}-\d{2}$/

export function parseRange(from: string | null, to: string | null): TimeRange | null {
  if (!from || !to || !DATE.test(from) || !DATE.test(to)) return null
  if (Number.isNaN(Date.parse(from)) || Number.isNaN(Date.parse(to))) return null
  return from <= to ? { from, to } : { from: to, to: from }
}

/** UTC day of a unix timestamp (seconds). */
export function dayOf(ts: number): string {
  return new Date(ts * 1000).toISOString().slice(0, 10)
}

/** The range as unix seconds: start of the first day to the end of the last. */
export function rangeBounds(r: TimeRange): [number, number] {
  return [Date.parse(`${r.from}T00:00:00Z`) / 1000, Date.parse(`${r.to}T00:00:00Z`) / 1000 + DAY]
}

/** The range spanned by two timestamps, in either order. */
export function rangeBetween(a: number, b: number): TimeRange {
  return { from: dayOf(Math.min(a, b)), to: dayOf(Math.max(a, b)) }
}

/** "Jun 3 – Aug 15, 2026", "Dec 3, 2025 – Feb 1, 2026" or "Jun 3, 2026". */
export function formatRange(r: TimeRange): string {
  const fmt = (d: string, year: boolean) => new Date(`${d}T00:00:00Z`).toLocaleDateString('en-US', {
    month: 'short', day: 'numeric', year: year ? 'numeric' : undefined, timeZone: 'UTC',
  })
  if (r.from === r.to) return fmt(r.from, true)
  const sameYear = r.from.slice(0, 4) === r.to.slice(0, 4)
  return `${fmt(r.from, !sameYear)} – ${fmt(r.to, true)}`
}
