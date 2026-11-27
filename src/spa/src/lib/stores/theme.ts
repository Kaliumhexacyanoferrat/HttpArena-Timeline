import { writable } from 'svelte/store'

export type Theme = 'light' | 'dark'

// The board's key, so the choice reads the same way on both sites.
const KEY = 'lb-theme'

const media = window.matchMedia('(prefers-color-scheme: dark)')

function saved(): Theme | null {
  try {
    const v = localStorage.getItem(KEY)
    return v === 'light' || v === 'dark' ? v : null
  } catch { return null }
}

function apply(t: Theme) {
  document.documentElement.setAttribute('data-theme', t)
}

/** The theme on screen. index.html applies it before the first paint; this keeps it current. */
export const theme = writable<Theme>(saved() ?? (media.matches ? 'dark' : 'light'))
theme.subscribe(apply)

// Without a choice of their own, the page follows the system as it changes.
media.addEventListener('change', e => {
  if (!saved()) theme.set(e.matches ? 'dark' : 'light')
})

export function toggleTheme(): void {
  theme.update(t => {
    const next: Theme = t === 'dark' ? 'light' : 'dark'
    try { localStorage.setItem(KEY, next) } catch { /* private mode */ }
    return next
  })
}
