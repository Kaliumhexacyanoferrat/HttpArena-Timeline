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

<header>
  <a class="logo" href="https://www.http-arena.com" target="_blank" rel="noopener">
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="22" height="22" aria-hidden="true">
      <path d="M16 1 L19 21 L16 23 L13 21 Z" fill="currentColor"/>
      <line x1="16" y1="4" x2="16" y2="19" stroke="currentColor" stroke-width="0.8" opacity="0.3"/>
      <path d="M7 22.5 Q7 21 9 21 L23 21 Q25 21 25 22.5 Q25 24 23 24 L9 24 Q7 24 7 22.5 Z" fill="currentColor"/>
      <rect x="14" y="24" width="4" height="4" rx="0.5" fill="currentColor"/>
      <line x1="14" y1="25.5" x2="18" y2="25.5" stroke="currentColor" stroke-width="0.5" opacity="0.3"/>
      <line x1="14" y1="27" x2="18" y2="27" stroke="currentColor" stroke-width="0.5" opacity="0.3"/>
      <ellipse cx="16" cy="30" rx="2.5" ry="1.8" fill="currentColor"/>
    </svg>
    <span class="logo-text">HTTP Arena <span class="logo-sub">Timeline</span></span>
  </a>
  <nav class="header-nav">
    <a href="https://www.http-arena.com" target="_blank" rel="noopener">http-arena.com</a>
    <a href="https://github.com/Kaliumhexacyanoferrat/HttpArena-Timeline" target="_blank" rel="noopener">GitHub</a>
  </nav>
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
  header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0 20px;
    height: 52px;
    border-bottom: 1px solid var(--border);
    flex-shrink: 0;
    background: rgba(15, 23, 42, 0.6);
    backdrop-filter: blur(8px);
    -webkit-backdrop-filter: blur(8px);
  }

  .logo {
    display: flex;
    align-items: center;
    gap: 9px;
    color: var(--text);
    text-decoration: none;
  }

  .logo-text {
    font-weight: 600;
    font-size: 15px;
    letter-spacing: -0.02em;
  }

  .logo-sub {
    font-weight: 400;
    color: var(--text-muted);
  }

  .header-nav {
    display: flex;
    align-items: center;
    gap: 16px;
  }

  .header-nav a {
    color: var(--text-muted);
    font-size: 13px;
    text-decoration: none;
    transition: color 0.15s;
  }
  .header-nav a:hover { color: var(--text); }

  .layout {
    display: grid;
    grid-template-columns: 220px 1fr;
    flex: 1;
    min-height: 0;
    overflow: hidden;
  }

  aside {
    border-right: 1px solid var(--border);
    overflow-y: auto;
    display: flex;
    flex-direction: column;
  }

  .aside-label {
    padding: 10px 18px 4px;
    font-size: 11px;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.06em;
    color: var(--text-muted);
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
    color: var(--text-muted);
  }

  .error { color: #ef4444; }
</style>
