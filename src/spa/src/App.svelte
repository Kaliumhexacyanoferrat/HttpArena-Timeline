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
  <span class="logo">⚡ HTTP Arena Timeline</span>
  <a href="https://github.com/Kaliumhexacyanoferrat/HttpArena-Timeline" target="_blank" rel="noopener">GitHub</a>
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
    padding: 0 16px;
    height: 48px;
    border-bottom: 1px solid var(--border);
    flex-shrink: 0;
  }

  .logo {
    font-weight: 600;
    font-size: 15px;
    letter-spacing: -0.01em;
  }

  header a {
    color: var(--text-muted);
    font-size: 13px;
    text-decoration: none;
  }
  header a:hover { color: var(--text); }

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
