import type { IndicatorScope } from './types'

export type StatKey = 'median' | 'avg' | 'min' | 'max'

export interface StatConfig {
  key: StatKey
  label: string
  short: string
  glyph: string
}

export const STATS: StatConfig[] = [
  { key: 'median', label: 'Median',  short: 'Med', glyph: 'x̃' },
  { key: 'avg',    label: 'Average', short: 'Avg', glyph: 'x̄' },
  { key: 'min',    label: 'Min',     short: 'Min', glyph: '↓' },
  { key: 'max',    label: 'Max',     short: 'Max', glyph: '↑' },
]

export const STAT_BY_KEY = Object.fromEntries(STATS.map(s => [s.key, s])) as Record<StatKey, StatConfig>

/** A selected indicator: one statistic of one scope, written as "median.rust" in URLs. */
export interface IndicatorRef {
  id: string
  stat: StatKey
  scope: string
}

export function indicatorId(stat: StatKey, scope: string): string {
  return `${stat}.${scope}`
}

export function parseIndicator(id: string): IndicatorRef | null {
  const dot = id.indexOf('.')
  if (dot < 0) return null
  const stat = id.slice(0, dot) as StatKey
  const scope = id.slice(dot + 1)
  if (!STAT_BY_KEY[stat] || !scope) return null
  return { id, stat, scope }
}

export function scopeLabel(scope: string, scopes: IndicatorScope[]): string {
  if (scope === 'all') return 'All'
  return scopes.find(s => s.key === scope)?.label ?? scope
}

export function indicatorLabel(ref: IndicatorRef, scopes: IndicatorScope[]): string {
  return `${STAT_BY_KEY[ref.stat].label} · ${scopeLabel(ref.scope, scopes)}`
}
