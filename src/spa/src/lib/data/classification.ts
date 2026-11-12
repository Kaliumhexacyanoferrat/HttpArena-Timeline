import type { FrameworkInfo } from './types'

/**
 * HttpArena's framework classification, mirrored from its leaderboard
 * (site/leaderboard/index.html) so the timeline filters exactly like the board does.
 */
export type FrameworkType = 'flagship' | 'emerging' | 'experimental' | 'engine' | 'infrastructure'

export interface TypeConfig {
  key: FrameworkType
  label: string
  /** Swatch next to a framework name. */
  color: string
  /** Text and background of an active filter button (the board's dark theme). */
  text: string
  background: string
  tip: string
}

export const TYPES: TypeConfig[] = [
  { key: 'flagship',       label: 'Flagship',     color: '#2e9e6a', text: '#5cc596', background: 'rgba(46,158,106,.24)',
    tip: 'Proven in production: real applications are built and operated on it at scale, and it is actively maintained.' },
  { key: 'emerging',       label: 'Emerging',     color: '#3b73c4', text: '#9bbdec', background: 'rgba(59,115,196,.30)',
    tip: 'A genuine, working server that is not yet production-proven: young, niche, or without real deployments to point to.' },
  { key: 'experimental',   label: 'Experimental', color: '#d98e2b', text: '#e8b066', background: 'rgba(217,142,43,.28)',
    tip: 'Very new, unproven work. Hidden until selected.' },
  { key: 'engine',         label: 'Engine',       color: '#cb5f51', text: '#e89384', background: 'rgba(203,95,81,.24)',
    tip: 'An HTTP implementation applications are not written against: raw sockets, low-level I/O, or a WSGI/ASGI-style host. Shown on its own.' },
  { key: 'infrastructure', label: 'Infra',        color: '#2f93a8', text: '#6cc3d2', background: 'rgba(47,147,168,.24)',
    tip: 'Reverse proxies and static-file servers run without an application framework layer. Shown on its own.' },
]

export const TYPE_BY_KEY = Object.fromEntries(TYPES.map(t => [t.key, t])) as Record<FrameworkType, TypeConfig>

export const TUNED = {
  text: '#d9bd6b',
  background: 'rgba(200,154,60,.26)',
  tip: 'Tuned: non-default configuration and hand optimizations.',
}

/** Flagship, emerging and experimental combine; engine and infrastructure are each selected alone. */
export const EXCLUSIVE_TYPES: string[] = ['engine', 'infrastructure']

/**
 * Default view: flagship + emerging without tuned entries. Note that the board's code shows
 * tuned entries by default (showTuned: true); the timeline deliberately starts without them
 * so its indicators compare against standard configurations.
 */
export const DEFAULT_TYPES: string[] = ['flagship', 'emerging']
export const DEFAULT_SHOW_TUNED = false

/** The type filter: which tiers are listed and aggregated, and whether tuned entries count. */
export interface TypeFilter { types: string[]; showTuned: boolean }

export function isDefaultFilter(f: TypeFilter): boolean {
  return f.showTuned === DEFAULT_SHOW_TUNED && [...f.types].sort().join(',') === [...DEFAULT_TYPES].sort().join(',')
}

/** Sanitizes a list of type keys: known ones only, an exclusive type alone, never empty. */
export function normalizeTypes(types: unknown): string[] | null {
  if (!Array.isArray(types)) return null
  const known = TYPES.map(t => t.key as string)
  const valid = types.filter((t): t is string => typeof t === 'string' && known.includes(t))
  const exclusive = valid.find(t => EXCLUSIVE_TYPES.includes(t))
  if (exclusive) return [exclusive]
  return valid.length ? [...new Set(valid)] : null
}

/** Type of a framework; the board reads a missing type as emerging. */
export function typeOf(info: FrameworkInfo | undefined): string {
  return info?.type || 'emerging'
}

export function typeConfig(info: FrameworkInfo | undefined): TypeConfig | undefined {
  return TYPE_BY_KEY[typeOf(info) as FrameworkType]
}

/** The board's toggleType: exclusive types replace the selection, the others combine, never empty. */
export function toggleType(types: string[], t: string): string[] {
  if (EXCLUSIVE_TYPES.includes(t) || types.some(x => EXCLUSIVE_TYPES.includes(x))) return [t]
  if (types.includes(t)) return types.length > 1 ? types.filter(x => x !== t) : types
  return [...types, t]
}

/** Whether a framework is listed under the given type filter (the board's row filter). */
export function passesTypeFilter(info: FrameworkInfo | undefined, types: string[], showTuned: boolean): boolean {
  if (!types.includes(typeOf(info))) return false
  return showTuned || !info?.tuned
}

/** "Flagship + Emerging, excluding tuned" */
export function describeFilter(f: TypeFilter): string {
  const types = TYPES.filter(t => f.types.includes(t.key)).map(t => t.label).join(' + ')
  return f.showTuned ? `${types}, including tuned` : `${types}, excluding tuned`
}

export function classificationLabel(info: FrameworkInfo | undefined): string {
  const t = typeConfig(info)?.label ?? typeOf(info)
  return info?.tuned ? `${t} · tuned` : t
}
