import { writable, get } from 'svelte/store'

function parseHash(): { test: string; frameworks: string[]; metrics: string[] } {
  try {
    const params = new URLSearchParams(window.location.hash.slice(1))
    return {
      test:       params.get('test') ?? '',
      frameworks: params.get('frameworks')?.split(',').filter(Boolean) ?? [],
      metrics:    params.get('metrics')?.split(',').filter(Boolean) ?? ['rps'],
    }
  } catch {
    return { test: '', frameworks: [], metrics: ['rps'] }
  }
}

const initial = parseHash()

export const selectedTest       = writable(initial.test)
export const selectedFrameworks = writable<string[]>(initial.frameworks)
export const selectedMetrics    = writable<string[]>(initial.metrics)

export function syncUrl(): void {
  const test      = get(selectedTest)
  const fws       = get(selectedFrameworks)
  const metrics   = get(selectedMetrics)

  const params = new URLSearchParams()
  if (test)       params.set('test', test)
  if (fws.length) params.set('frameworks', fws.join(','))
  const ms = metrics.join(',')
  if (ms && ms !== 'rps') params.set('metrics', ms)

  const hash = params.toString()
  history.replaceState(null, '', hash ? `#${hash}` : location.pathname + location.search)
}
