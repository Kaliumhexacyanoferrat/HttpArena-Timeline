<script lang="ts">
  import { onMount } from 'svelte'
  import TestList from './lib/components/TestList.svelte'
  import FrameworkPicker from './lib/components/FrameworkPicker.svelte'
  import MetricPicker from './lib/components/MetricPicker.svelte'
  import TimelineChart from './lib/components/TimelineChart.svelte'
  import { loadIndex } from './lib/data/loader'
  import { selectedTest, selectedFrameworks, selectedMetrics, syncUrl } from './lib/stores/selection'
  import type { DataIndex } from './lib/data/types'

  let index: DataIndex | null = null
  let error: string | null = null
  let mounted = false

  onMount(async () => {
    try {
      index = await loadIndex()
      if (!$selectedTest && index.tests.length > 0)
        selectedTest.set(index.tests.includes('baseline-4096') ? 'baseline-4096' : index.tests[0])
    } catch (e) {
      error = String(e)
    }
    mounted = true
  })

  // Sync URL whenever any selection changes (after initial mount).
  $: if (mounted) {
    $selectedTest; $selectedFrameworks; $selectedMetrics
    syncUrl()
  }
</script>

<header class="top">
  <a class="brand" href="https://www.http-arena.com" target="_blank" rel="noopener">
    <span class="brand-name"><b>Http</b>Arena</span><span class="brand-sub">Timeline</span>
  </a>
  <div class="top-links">
    <a class="icon-btn" href="https://www.http-arena.com/leaderboard" target="_blank" rel="noopener" title="Leaderboard" aria-label="Leaderboard">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><line x1="6" y1="20" x2="6" y2="14"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="18" y1="20" x2="18" y2="10"/></svg>
    </a>
    <a class="icon-btn" href="https://github.com/Kaliumhexacyanoferrat/HttpArena-Timeline" target="_blank" rel="noopener" title="GitHub" aria-label="GitHub">
      <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M12 .297c-6.63 0-12 5.373-12 12 0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61C4.422 18.07 3.633 17.7 3.633 17.7c-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23A11.509 11.509 0 0112 5.803c1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222 0 1.606-.014 2.898-.014 3.293 0 .322.216.694.825.576C20.565 22.092 24 17.592 24 12.297c0-6.627-5.373-12-12-12"/></svg>
    </a>
    <a class="icon-btn" href="https://discord.gg/H84B5ZqDXR" target="_blank" rel="noopener" title="Discord" aria-label="Discord">
      <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M20.317 4.3698a19.7913 19.7913 0 00-4.8851-1.5152.0741.0741 0 00-.0785.0371c-.211.3753-.4447.8648-.6083 1.2495-1.8447-.2762-3.68-.2762-5.4868 0-.1636-.3933-.4058-.8742-.6177-1.2495a.077.077 0 00-.0785-.037 19.7363 19.7363 0 00-4.8852 1.515.0699.0699 0 00-.0321.0277C.5334 9.0458-.319 13.5799.0992 18.0578a.0824.0824 0 00.0312.0561c2.0528 1.5076 4.0413 2.4228 5.9929 3.0294a.0777.0777 0 00.0842-.0276c.4616-.6304.8731-1.2952 1.226-1.9942a.076.076 0 00-.0416-.1057c-.6528-.2476-1.2743-.5495-1.8722-.8923a.077.077 0 01-.0076-.1277c.1258-.0943.2517-.1923.3718-.2914a.0743.0743 0 01.0776-.0105c3.9278 1.7933 8.18 1.7933 12.0614 0a.0739.0739 0 01.0785.0095c.1202.099.246.1981.3728.2924a.077.077 0 01-.0066.1276 12.2986 12.2986 0 01-1.873.8914.0766.0766 0 00-.0407.1067c.3604.698.7719 1.3628 1.225 1.9932a.076.076 0 00.0842.0286c1.961-.6067 3.9495-1.5219 6.0023-3.0294a.077.077 0 00.0313-.0552c.5004-5.177-.8382-9.6739-3.5485-13.6604a.061.061 0 00-.0312-.0286zM8.02 15.3312c-1.1825 0-2.1569-1.0857-2.1569-2.419 0-1.3332.9555-2.4189 2.157-2.4189 1.2108 0 2.1757 1.0952 2.1568 2.419 0 1.3332-.9555 2.4189-2.1569 2.4189zm7.9748 0c-1.1825 0-2.1569-1.0857-2.1569-2.419 0-1.3332.9554-2.4189 2.1569-2.4189 1.2108 0 2.1757 1.0952 2.1568 2.419 0 1.3332-.946 2.4189-2.1568 2.4189Z"/></svg>
    </a>
  </div>
</header>

{#if error}
  <div class="error">Failed to load data: {error}</div>
{:else if !index}
  <div class="loading">Loading…</div>
{:else}
  <div class="layout">
    <aside>
      <div class="aside-label">Tests</div>
      <TestList tests={index.tests} />
    </aside>

    <main>
      <FrameworkPicker frameworks={index.frameworks} />

      <TimelineChart
        test={$selectedTest}
        frameworks={$selectedFrameworks}
        metrics={$selectedMetrics}
      />

      <MetricPicker />
    </main>
  </div>
{/if}

<style>
  /* ── header ── */
  .top {
    position: sticky;
    top: 0;
    z-index: 40;
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 1.1rem;
    padding: .72rem 1.5rem;
    background: rgba(17,24,39,.7);
    backdrop-filter: blur(14px) saturate(1.6);
    -webkit-backdrop-filter: blur(14px) saturate(1.6);
    border-bottom: 1px solid var(--border);
    box-shadow: 0 6px 20px -12px rgba(20,22,40,.25);
    flex-shrink: 0;
  }

  .brand {
    display: flex;
    align-items: baseline;
    gap: .4rem;
    text-decoration: none;
    color: var(--text);
    white-space: nowrap;
  }
  .brand:hover { opacity: .75; }

  .brand-name {
    font-weight: 750;
    font-size: 1.06rem;
    letter-spacing: -.02em;
    color: var(--text);
  }
  .brand-name b { color: var(--accent); font-weight: 750; }

  .brand-sub {
    font-size: .85rem;
    font-weight: 500;
    color: var(--text-2);
  }

  .top-links {
    display: flex;
    align-items: center;
    gap: .25rem;
  }

  .icon-btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 34px;
    height: 34px;
    border-radius: 8px;
    border: 1px solid var(--border);
    background: var(--panel);
    color: var(--text-2);
    text-decoration: none;
    transition: color .12s ease, border-color .12s ease;
    flex-shrink: 0;
  }
  .icon-btn:hover { color: var(--text); border-color: var(--accent); }
  .icon-btn svg { width: 17px; height: 17px; }

  /* ── layout ── */
  .layout {
    display: grid;
    grid-template-columns: 264px 1fr;
    flex: 1;
    min-height: 0;
    overflow: hidden;
  }

  aside {
    border-right: 1px solid var(--border);
    overflow-y: auto;
    display: flex;
    flex-direction: column;
    background: transparent;
    scrollbar-width: thin;
    scrollbar-color: var(--border) transparent;
  }

  .aside-label {
    display: flex;
    align-items: center;
    gap: .35rem;
    margin: .1rem .55rem .45rem;
    padding: .9rem .55rem .1rem;
    font-size: .66rem;
    font-weight: 700;
    letter-spacing: .08em;
    text-transform: uppercase;
    color: var(--muted);
  }

  main {
    display: flex;
    flex-direction: column;
    min-height: 0;
    overflow: hidden;
  }

  @media (max-width: 639px) {
    .layout {
      grid-template-columns: 1fr;
      flex: none;
      overflow: visible;
    }
    aside {
      border-right: none;
      border-bottom: 1px solid var(--border);
      overflow: visible;
    }
    .aside-label { display: none; }
    main { overflow: visible; }
  }

  .loading, .error {
    flex: 1;
    display: flex;
    align-items: center;
    justify-content: center;
    color: var(--text-2);
  }
  .error { color: #ef4444; }
</style>
