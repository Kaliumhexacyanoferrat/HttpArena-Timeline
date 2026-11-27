<script lang="ts">
  import { onMount } from 'svelte'
  import TestList from './lib/components/TestList.svelte'
  import FrameworkPicker from './lib/components/FrameworkPicker.svelte'
  import IndicatorPicker from './lib/components/IndicatorPicker.svelte'
  import MetricPicker from './lib/components/MetricPicker.svelte'
  import TimelineChart from './lib/components/TimelineChart.svelte'
  import TimeRangeFilter from './lib/components/TimeRangeFilter.svelte'
  import ResolutionPicker from './lib/components/ResolutionPicker.svelte'
  import TypeFilterBar from './lib/components/TypeFilterBar.svelte'
  import { loadIndex } from './lib/data/loader'
  import { CATEGORIES, profileOf, splitTest, formatConnections } from './lib/data/categories'
  import { passesTypeFilter } from './lib/data/classification'
  import { theme, toggleTheme } from './lib/stores/theme'
  import { selectedTest, selectedFrameworks, selectedIndicators, selectedRange, selectedResolution, typeFilter, selectedMetrics, metricsTouched, defaultMetricsForTest, syncUrl } from './lib/stores/selection'
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

  // Apply per-test default metrics while the user hasn't picked metrics themselves.
  // Runs on the initial test and on every test change, so e.g. selecting a latency
  // test switches to CPU/req + P99 + P99.9, and switching back to a throughput test
  // reverts to RPS.
  $: if (mounted && !$metricsTouched) {
    selectedMetrics.set(defaultMetricsForTest($selectedTest))
  }

  // Tests by category, for the phone layout's test picker (the sidebar is hidden there).
  $: testGroups = index
    ? CATEGORIES.map(c => ({ name: c.name, tests: c.tests.filter(t => index!.tests.includes(t)) })).filter(g => g.tests.length)
    : []

  const optionLabel = (t: string) => {
    const { connections } = splitTest(t)
    const label = profileOf(t)?.label
    return label && connections != null ? `${label} · ${formatConnections(connections)}` : t
  }

  $: profile = profileOf($selectedTest)
  $: connections = splitTest($selectedTest).connections

  // "HTTP/1.1 · 4,096 connections": what the board's name alone does not say.
  $: subtitle = (() => {
    const category = CATEGORIES.find(c => c.tests.includes($selectedTest))?.name
    return [category, connections != null && `${formatConnections(connections)} connections`].filter(Boolean).join(' · ')
  })()

  // Frameworks with results for the test, and how many of them the type filter shows.
  $: withResults = (() => {
    const infos = Object.values(index?.frameworks ?? {}).filter(f => f.tests.includes($selectedTest))
    return { total: infos.length, shown: infos.filter(f => passesTypeFilter(f, $typeFilter.types, $typeFilter.showTuned)).length }
  })()

  // Sync URL whenever any selection changes (after initial mount).
  $: if (mounted) {
    $selectedTest; $selectedFrameworks; $selectedIndicators; $selectedRange; $selectedResolution; $typeFilter; $selectedMetrics
    syncUrl()
  }
</script>

<header class="top">
  <a class="brand" href="https://www.http-arena.com/" aria-label="HttpArena home">
    <svg class="brand-ico" viewBox="0 0 28 28" aria-hidden="true"><rect class="bi-fr" x="1.75" y="1.75" width="24.5" height="24.5" rx="5"/><rect x="5.5" y="5.4" width="17" height="4.6" rx="1.2"/><rect x="5.5" y="11.7" width="17" height="4.6" rx="1.2"/><rect x="5.5" y="18" width="17" height="4.6" rx="1.2"/><circle class="bi-led" cx="19.3" cy="7.7" r="1.3"/><circle class="bi-led" cx="19.3" cy="14" r="1.3"/><circle class="bi-led a" cx="19.3" cy="20.3" r="1.3"/></svg>
    <span><b>Http</b>Arena</span>
  </a>

  <!-- The board's site navigation; the timeline is the page on screen. -->
  <nav class="top-nav" aria-label="Site">
    <a class="tn" href="https://www.http-arena.com/" title="The leaderboard: every entry racked by its composite score."><svg viewBox="0 0 24 24" aria-hidden="true"><rect x="4" y="3" width="16" height="18" rx="1.5"/><path d="M4 9h16M4 15h16"/></svg><span>Racks</span></a>
    <a class="tn" href="https://www.http-arena.com/frameworks/" title="A results page for every entry, grouped by language."><svg viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/></svg><span>Frameworks</span></a>
    <a class="tn" href="https://www.http-arena.com/compare/" title="Put any two entries side by side on the bench."><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 8h13"/><path d="m14 5 3 3-3 3"/><path d="M20 16H7"/><path d="m10 13-3 3 3 3"/></svg><span>Compare</span></a>
    <a class="tn" href="https://www.http-arena.com/docs/" title="The Knowledge Base: how the benchmark is run, scored and extended."><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/><path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/></svg><span>Knowledge Base</span></a>
    <a class="tn" href="./" aria-current="page" title="Every result over time."><svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/></svg><span>Timeline</span></a>
  </nav>

  <div class="top-links">
    <a class="icon-btn" href="https://github.com/Kaliumhexacyanoferrat/HttpArena-Timeline" target="_blank" rel="noopener" title="GitHub" aria-label="GitHub">
      <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M12 .297c-6.63 0-12 5.373-12 12 0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61C4.422 18.07 3.633 17.7 3.633 17.7c-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23A11.509 11.509 0 0112 5.803c1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222 0 1.606-.014 2.898-.014 3.293 0 .322.216.694.825.576C20.565 22.092 24 17.592 24 12.297c0-6.627-5.373-12-12-12"/></svg>
    </a>
    <a class="icon-btn" href="https://discord.gg/H84B5ZqDXR" target="_blank" rel="noopener" title="Discord" aria-label="Discord">
      <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M20.317 4.3698a19.7913 19.7913 0 00-4.8851-1.5152.0741.0741 0 00-.0785.0371c-.211.3753-.4447.8648-.6083 1.2495-1.8447-.2762-3.68-.2762-5.4868 0-.1636-.3933-.4058-.8742-.6177-1.2495a.077.077 0 00-.0785-.037 19.7363 19.7363 0 00-4.8852 1.515.0699.0699 0 00-.0321.0277C.5334 9.0458-.319 13.5799.0992 18.0578a.0824.0824 0 00.0312.0561c2.0528 1.5076 4.0413 2.4228 5.9929 3.0294a.0777.0777 0 00.0842-.0276c.4616-.6304.8731-1.2952 1.226-1.9942a.076.076 0 00-.0416-.1057c-.6528-.2476-1.2743-.5495-1.8722-.8923a.077.077 0 01-.0076-.1277c.1258-.0943.2517-.1923.3718-.2914a.0743.0743 0 01.0776-.0105c3.9278 1.7933 8.18 1.7933 12.0614 0a.0739.0739 0 01.0785.0095c.1202.099.246.1981.3728.2924a.077.077 0 01-.0066.1276 12.2986 12.2986 0 01-1.873.8914.0766.0766 0 00-.0407.1067c.3604.698.7719 1.3628 1.225 1.9932a.076.076 0 00.0842.0286c1.961-.6067 3.9495-1.5219 6.0023-3.0294a.077.077 0 00.0313-.0552c.5004-5.177-.8382-9.6739-3.5485-13.6604a.061.061 0 00-.0312-.0286zM8.02 15.3312c-1.1825 0-2.1569-1.0857-2.1569-2.419 0-1.3332.9555-2.4189 2.157-2.4189 1.2108 0 2.1757 1.0952 2.1568 2.419 0 1.3332-.9555 2.4189-2.1569 2.4189zm7.9748 0c-1.1825 0-2.1569-1.0857-2.1569-2.419 0-1.3332.9554-2.4189 2.1569-2.4189 1.2108 0 2.1757 1.0952 2.1568 2.419 0 1.3332-.946 2.4189-2.1568 2.4189Z"/></svg>
    </a>
    <button class="icon-btn" title="Toggle theme" aria-label="Toggle light / dark theme" on:click={toggleTheme}>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><circle cx="12" cy="12" r="8.5"/><path d="M12 3.5v17"/><path d="M12 3.5a8.5 8.5 0 0 1 0 17z" fill="currentColor" stroke="none"/></svg>
    </button>
  </div>
</header>

{#if error}
  <div class="error">Failed to load data: {error}</div>
{:else if !index}
  <div class="loading">Loading…</div>
{:else}
  <div class="layout">
    <aside>
      <TestList tests={index.tests} />
    </aside>

    <main>
      <!-- The console: the test's placard, and its asset plate. -->
      <section class="console">
        <div class="placard">
          <i class="screw" aria-hidden="true"></i><i class="screw" aria-hidden="true"></i><i class="screw" aria-hidden="true"></i><i class="screw" aria-hidden="true"></i>
          <div class="cat">{subtitle || 'Timeline'}</div>
          <div class="title">
            <h1>{profile?.label ?? ($selectedTest || 'Choose a test')}<span class="title-caret" aria-hidden="true"></span></h1>
            <!-- Phones: the title is the test picker, using the platform's own list. -->
            <select
              class="test-select"
              aria-label="Test"
              value={$selectedTest}
              on:change={e => selectedTest.set(e.currentTarget.value)}
            >
              {#each testGroups as group (group.name)}
                <optgroup label={group.name}>
                  {#each group.tests as t (t)}<option value={t}>{optionLabel(t)}</option>{/each}
                </optgroup>
              {/each}
            </select>
          </div>
          <p>
            {profile?.description ?? 'Benchmark results over time.'}
            {#if profile}<a href="https://www.http-arena.com/docs/{profile.doc}/" target="_blank" rel="noopener">About this test →</a>{/if}
          </p>
        </div>

        <div class="asset">
          <div class="asset-h">Test record</div>
          <div class="chip"><b>{$selectedTest}</b></div>
          {#if connections != null}<div class="chip"><b>{connections.toLocaleString('en-US')}</b> connections</div>{/if}
          <div class="chip" title="Frameworks with results for this test: in total, and of the types the filter shows">
            <b>{withResults.total}</b> frameworks · <b>{withResults.shown}</b> shown
          </div>
        </div>
      </section>

      <!-- The controls: what is shown, and how. -->
      <section class="tools">
        <div class="tools-row">
          <TypeFilterBar />
          <div class="view">
            <div class="metrics"><span class="cap">Metrics</span><MetricPicker /></div>
            <ResolutionPicker />
          </div>
        </div>
        <div class="tools-row series">
          <span class="cap">Series</span>
          <FrameworkPicker frameworks={index.frameworks} />
          <IndicatorPicker scopes={index.indicators ?? []} frameworks={index.frameworks} />
          <TimeRangeFilter />
        </div>
      </section>

      <section class="screen">
        <TimelineChart
          test={$selectedTest}
          frameworks={$selectedFrameworks}
          indicators={$selectedIndicators}
          scopes={index.indicators ?? []}
          metrics={$selectedMetrics}
          resolution={$selectedResolution}
          filter={$typeFilter}
          frameworkInfo={index.frameworks}
          theme={$theme}
        />
      </section>
    </main>
  </div>
{/if}

<style>
  /* ── the header: the top panel of the hall, a strip of brushed metal over a hazard stripe ── */
  .top {
    position: sticky;
    top: 0;
    z-index: 40;
    display: flex;
    align-items: center;
    gap: .9rem;
    height: var(--top-h);
    padding: 0 1.25rem;
    background: var(--header-bg);
    border-bottom: 1px solid var(--line);
    box-shadow: 0 1px 0 var(--edge-hi) inset, 0 8px 24px -14px rgba(0,0,0,.55);
    flex-shrink: 0;
  }
  .top::after {
    content: "";
    position: absolute;
    left: 0; right: 0; bottom: -4px;
    height: 3px;
    background: var(--hazard);
    opacity: .9;
    pointer-events: none;
  }

  .brand {
    display: flex;
    align-items: center;
    gap: .5rem;
    flex: none;
    font: 700 1.18rem/1 var(--label);
    letter-spacing: .01em;
    color: var(--text);
    text-decoration: none;
  }
  .brand:hover { opacity: .82; }
  .brand b { color: var(--accent); font-weight: 700; }

  /* the mark: a rack - its frame, three units and their status lights */
  .brand-ico { width: 26px; height: 26px; color: var(--text); fill: currentColor; flex: none; }
  .brand-ico :global(.bi-fr) { fill: none; stroke: currentColor; stroke-width: 1.6; opacity: .85; }
  .brand-ico :global(.bi-led) { fill: var(--led-g); }
  .brand-ico :global(.bi-led.a) { fill: var(--led-a); }

  .top-nav {
    display: flex;
    align-items: stretch;
    gap: .15rem;
    min-width: 0;
  }

  .tn {
    position: relative;
    display: flex;
    align-items: center;
    gap: .4rem;
    padding: .42rem .7rem;
    border: 1px solid transparent;
    border-radius: 4px;
    color: var(--text-2);
    font: 600 .9rem/1 var(--label);
    letter-spacing: .03em;
    text-transform: uppercase;
    text-decoration: none;
    white-space: nowrap;
  }
  .tn svg { width: 15px; height: 15px; flex: none; fill: none; stroke: currentColor; stroke-width: 2; stroke-linecap: round; stroke-linejoin: round; }
  .tn:hover { background: var(--panel-2); color: var(--text); border-color: var(--line); }
  .tn[aria-current="page"] { color: var(--text); background: var(--panel-2); border-color: var(--line); }
  .tn[aria-current="page"]::after {
    content: "";
    position: absolute;
    left: 8px; right: 8px; bottom: -1px;
    height: 2px;
    background: var(--led-g);
    box-shadow: 0 0 8px var(--led-g);
  }

  .top-links {
    display: flex;
    align-items: center;
    gap: .25rem;
    margin-left: auto;
    flex: none;
  }

  .icon-btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 34px;
    height: 34px;
    border-radius: 4px;
    border: 1px solid var(--line);
    background: var(--panel);
    color: var(--text-2);
    cursor: pointer;
    text-decoration: none;
    flex-shrink: 0;
  }
  .icon-btn:hover { color: var(--text); border-color: var(--accent); }
  .icon-btn svg { width: 17px; height: 17px; }

  /* ── layout: the rail of tests, and the page ── */
  .layout {
    display: grid;
    grid-template-columns: 248px minmax(0, 1fr);
    flex: 1;
    min-height: 0;
    overflow: hidden;
    /* clear of the hazard stripe */
    margin-top: 4px;
  }

  aside {
    overflow-y: auto;
    background: var(--patch);
    border-right: 1px solid var(--edge-lo);
    box-shadow: inset -1px 0 0 var(--edge-hi);
    scrollbar-width: thin;
    scrollbar-color: var(--line) transparent;
  }

  main {
    display: flex;
    flex-direction: column;
    gap: .8rem;
    min-width: 0;
    min-height: 0;
    padding: 1.1rem 1.25rem 1rem;
    overflow: hidden;
  }

  /* ── the console: the test's placard and its asset plate ── */
  .console {
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto;
    gap: 1rem;
    align-items: stretch;
    flex-shrink: 0;
  }

  .placard {
    position: relative;
    padding: .85rem 1.6rem .9rem;
    background: var(--face);
    border: 1px solid var(--edge-lo);
    border-radius: 4px;
    box-shadow: 0 1px 0 var(--edge-hi) inset, var(--shadow);
  }
  .placard > :global(.screw:nth-child(1)) { left: 7px; top: 7px; }
  .placard > :global(.screw:nth-child(2)) { right: 7px; top: 7px; }
  .placard > :global(.screw:nth-child(3)) { left: 7px; bottom: 7px; }
  .placard > :global(.screw:nth-child(4)) { right: 7px; bottom: 7px; }

  .cat {
    display: flex;
    align-items: center;
    gap: .5rem;
    font: 600 .76rem/1 var(--label);
    letter-spacing: .14em;
    text-transform: uppercase;
    color: var(--text-2);
  }
  .cat::before {
    content: "";
    width: 7px;
    height: 7px;
    border-radius: 50%;
    background: var(--led-g);
    box-shadow: 0 0 7px var(--led-g);
  }

  .title { position: relative; min-width: 0; }

  h1 {
    margin: .3rem 0 0;
    font: 700 clamp(1.4rem, 2.4vw, 2rem)/1.08 var(--label);
    letter-spacing: .005em;
    text-transform: uppercase;
    color: var(--text);
    overflow-wrap: anywhere;
  }

  .placard p {
    margin: .4rem 0 0;
    color: var(--text-2);
    font-size: .88rem;
    line-height: 1.55;
    max-width: 86ch;
  }
  .placard p a { color: var(--accent); font-weight: 600; text-decoration: none; white-space: nowrap; }
  .placard p a:hover { text-decoration: underline; }

  .asset {
    min-width: 240px;
    max-width: 340px;
    padding: .7rem .9rem;
    background: var(--plate);
    border: 1px solid var(--plate-line);
    border-radius: 4px;
    box-shadow: 0 1px 0 var(--edge-hi) inset;
    display: flex;
    flex-direction: column;
    justify-content: center;
    gap: .2rem;
  }
  .asset-h {
    margin-bottom: .2rem;
    font: 600 .7rem/1 var(--label);
    letter-spacing: .14em;
    text-transform: uppercase;
    color: var(--text-2);
  }
  .chip { font: 500 .76rem/1.4 var(--mono); color: var(--text-2); }
  .chip b { color: var(--text); font-weight: 600; }

  .test-select, .title-caret { display: none; }

  /* ── the controls ── */
  .tools {
    display: flex;
    flex-direction: column;
    gap: .6rem;
    padding: .65rem .75rem;
    background: var(--panel);
    border: 1px solid var(--line);
    flex-shrink: 0;
  }

  .tools-row {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: .6rem .9rem;
    min-width: 0;
  }

  .view {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: .6rem 1.1rem;
    margin-left: auto;
  }

  .metrics { display: flex; align-items: center; gap: .6rem; }

  .cap {
    font: 600 .8125rem/1 var(--sans);
    color: var(--text-2);
    white-space: nowrap;
  }

  .series { gap: .45rem; }
  .series .cap { margin-right: .15rem; }

  /* ── the screen: the chart ── */
  .screen {
    display: flex;
    flex-direction: column;
    flex: 1;
    min-height: 0;
    background: var(--panel);
    border: 1px solid var(--line);
    border-radius: 4px;
    box-shadow: var(--shadow);
  }

  /* A short window: the controls and the chart scroll together instead of squeezing the chart. */
  @media (max-height: 720px) and (min-width: 640px) {
    main { overflow-y: auto; }
    .screen { flex: none; height: max(360px, 62vh); }
  }

  @media (max-width: 1180px) {
    .tn span { display: none; }
    .tn { padding: .42rem .55rem; }
  }

  @media (max-width: 1023px) {
    .layout { grid-template-columns: 210px minmax(0, 1fr); }
    .asset { display: none; }
    .console { grid-template-columns: minmax(0, 1fr); }
  }

  @media (max-width: 639px) {
    .top { padding: 0 .75rem; gap: .5rem; }
    /* the timeline's own tab only */
    .tn:not([aria-current]) { display: none; }
    .tn span { display: inline; }
    .icon-btn { width: 32px; height: 32px; }

    .layout {
      grid-template-columns: minmax(0, 1fr);
      flex: none;
      overflow: visible;
    }
    main { padding: .9rem .75rem; overflow: visible; }
    .placard { padding: .75rem 1.1rem .8rem; }
    .view { margin-left: 0; }

    /* The rail becomes the title's native picker. */
    aside { display: none; }
    .test-select {
      display: block;
      position: absolute;
      inset: 0;
      width: 100%;
      opacity: 0;
      font-size: 16px; /* keeps iOS from zooming in on focus */
    }
    .title-caret {
      display: inline-block;
      width: 9px;
      height: 9px;
      margin: 0 0 6px 12px;
      vertical-align: middle;
      border-right: 2px solid var(--text-2);
      border-bottom: 2px solid var(--text-2);
      transform: rotate(45deg);
    }
    /* The select is invisible, so show its keyboard focus on the title. */
    .title:has(.test-select:focus-visible) h1 { outline: 2px solid var(--accent); outline-offset: 2px; }
  }

  .loading, .error {
    flex: 1;
    display: flex;
    align-items: center;
    justify-content: center;
    color: var(--text-2);
    font: 600 .9rem var(--label);
    letter-spacing: .1em;
    text-transform: uppercase;
  }
  .error { color: var(--led-r); }
</style>
