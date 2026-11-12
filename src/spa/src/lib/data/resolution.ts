/** Time resolution of the chart: every data point, or one point per UTC period. */
export type Resolution = 'raw' | 'day' | 'week' | 'month' | 'quarter' | 'year'

export interface ResolutionConfig {
  key: Resolution
  label: string
  title: string
}

export const RESOLUTIONS: ResolutionConfig[] = [
  { key: 'raw',     label: 'All points', title: 'Every measurement' },
  { key: 'day',     label: 'Daily',     title: 'One point per day' },
  { key: 'week',    label: 'Weekly',    title: 'One point per week (Monday to Sunday)' },
  { key: 'month',   label: 'Monthly',   title: 'One point per month' },
  { key: 'quarter', label: 'Quarterly', title: 'One point per quarter' },
  { key: 'year',    label: 'Yearly',    title: 'One point per year' },
]

export function parseResolution(value: string | null): Resolution {
  return RESOLUTIONS.find(r => r.key === value)?.key ?? 'raw'
}

const utc = (y: number, m: number, d: number) => Date.UTC(y, m, d) / 1000

/** Start (unix seconds) of the UTC period containing ts. */
export function periodStart(ts: number, res: Resolution): number {
  const d = new Date(ts * 1000)
  const y = d.getUTCFullYear(), m = d.getUTCMonth(), day = d.getUTCDate()
  switch (res) {
    case 'raw':     return ts
    case 'day':     return utc(y, m, day)
    case 'week':    return utc(y, m, day - (d.getUTCDay() + 6) % 7)
    case 'month':   return utc(y, m, 1)
    case 'quarter': return utc(y, m - m % 3, 1)
    case 'year':    return utc(y, 0, 1)
  }
}

/** Start of the period after the one starting at start. */
export function nextPeriod(start: number, res: Resolution): number {
  const d = new Date(start * 1000)
  const y = d.getUTCFullYear(), m = d.getUTCMonth(), day = d.getUTCDate()
  switch (res) {
    case 'raw':     return start
    case 'day':     return utc(y, m, day + 1)
    case 'week':    return utc(y, m, day + 7)
    case 'month':   return utc(y, m + 1, 1)
    case 'quarter': return utc(y, m + 3, 1)
    case 'year':    return utc(y + 1, 0, 1)
  }
}

/** Legend label of the period starting at start, e.g. "Sep 2026", "Q3 2026", "Week of Sep 14, 2026". */
export function formatPeriod(start: number, res: Resolution): string {
  const d = new Date(start * 1000)
  const fmt = (o: Intl.DateTimeFormatOptions) => d.toLocaleDateString('en-US', { ...o, timeZone: 'UTC' })
  switch (res) {
    case 'raw':
    case 'day':     return fmt({ year: 'numeric', month: 'short', day: 'numeric' })
    case 'week':    return `Week of ${fmt({ year: 'numeric', month: 'short', day: 'numeric' })}`
    case 'month':   return fmt({ year: 'numeric', month: 'short' })
    case 'quarter': return `Q${Math.floor(d.getUTCMonth() / 3) + 1} ${d.getUTCFullYear()}`
    case 'year':    return String(d.getUTCFullYear())
  }
}
