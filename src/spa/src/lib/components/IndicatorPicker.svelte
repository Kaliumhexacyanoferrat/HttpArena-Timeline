<script lang="ts">
  import { selectedTest, selectedFrameworks, selectedIndicators, seriesColors } from '../stores/selection'
  import { STATS, STAT_BY_KEY, indicatorId, parseIndicator, indicatorLabel, type StatKey } from '../data/indicators'
  import type { FrameworkInfo, IndicatorScope } from '../data/types'

  export let scopes: IndicatorScope[]
  export let frameworks: Record<string, FrameworkInfo>

  interface Row { scope: IndicatorScope; count: number }

  let open = false
  let query = ''
  let wrapEl: HTMLDivElement
  let searchEl: HTMLInputElement

  // How many frameworks each scope aggregates for the selected test.
  $: counts = (() => {
    const byLanguage: Record<string, number> = {}
    let total = 0
    for (const info of Object.values(frameworks)) {
      if (!info.tests.includes($selectedTest)) continue
      total++
      byLanguage[info.language] = (byLanguage[info.language] ?? 0) + 1
    }
    return { total, byLanguage }
  })()

  function countOf(scope: IndicatorScope): number {
    return scope.language ? counts.byLanguage[scope.language] ?? 0 : counts.total
  }

  // Languages of the frameworks on the chart come first: the obvious thing to compare against.
  $: selectedLanguages = [...new Set($selectedFrameworks.map(fw => frameworks[fw]?.language).filter(Boolean))]

  $: sections = (() => {
    const q = query.trim().toLowerCase()
    const matches = (s: IndicatorScope) => !q || s.label.toLowerCase().includes(q) || s.key.includes(q)

    const rows: Row[] = scopes.map(scope => ({ scope, count: countOf(scope) }))
    const available = rows.filter(r => r.count > 0 && matches(r.scope))

    const overall = available.filter(r => !r.scope.language)
    const yours = available
      .filter(r => r.scope.language && selectedLanguages.includes(r.scope.language))
      .sort((a, b) => selectedLanguages.indexOf(a.scope.language!) - selectedLanguages.indexOf(b.scope.language!))
    const others = available
      .filter(r => r.scope.language && !selectedLanguages.includes(r.scope.language))
      .sort((a, b) => b.count - a.count || a.scope.label.localeCompare(b.scope.label))

    return {
      groups: [
        { title: 'Overall', rows: overall },
        { title: 'Languages on the chart', rows: yours },
        { title: yours.length ? 'Other languages' : 'Languages', rows: others },
      ].filter(g => g.rows.length > 0),
      hidden: rows.filter(r => r.count === 0 && matches(r.scope)).length,
    }
  })()

  $: refs = $selectedIndicators.map(parseIndicator).filter(r => r !== null)

  function toggle(stat: StatKey, scope: string) {
    const id = indicatorId(stat, scope)
    selectedIndicators.update(ids => ids.includes(id) ? ids.filter(i => i !== id) : [...ids, id])
  }

  function remove(id: string) {
    selectedIndicators.update(ids => ids.filter(i => i !== id))
  }

  function describe(stat: StatKey, scope: string): string {
    const s = scopes.find(x => x.key === scope)
    const who = s?.language ? `${s.language} frameworks` : 'frameworks'
    const extra = stat === 'min' || stat === 'max' ? ', naming the framework that set it' : ''
    return `${STAT_BY_KEY[stat].label} of all ${who} with results for the test, tracked daily${extra}`
  }

  async function toggleOpen() {
    open = !open
    if (open) {
      query = ''
      // Wait for the panel to render before focusing its search box.
      await Promise.resolve()
      searchEl?.focus()
    }
  }

  function onWindowPointer(e: MouseEvent) {
    if (open && wrapEl && !wrapEl.contains(e.target as Node)) open = false
  }

  function onKeydown(e: KeyboardEvent) {
    if (e.key === 'Escape') open = false
  }
</script>

<svelte:window on:mousedown={onWindowPointer} />

<div class="indicator-picker">
  {#each refs as ref (ref.id)}
    <span
      class="chip"
      style="--color: {$seriesColors[`ind:${ref.id}`]}"
      title={describe(ref.stat, ref.scope)}
    >
      <span class="glyph" aria-hidden="true">{STAT_BY_KEY[ref.stat].glyph}</span>
      {indicatorLabel(ref, scopes)}
      <button class="remove" on:click={() => remove(ref.id)} title="Remove" aria-label="Remove {indicatorLabel(ref, scopes)}">×</button>
    </span>
  {/each}

  <div class="add-wrap" bind:this={wrapEl}>
    <button
      class="add"
      class:active={open}
      on:click={toggleOpen}
      aria-expanded={open}
      aria-haspopup="dialog"
      disabled={scopes.length === 0}
      title={scopes.length === 0 ? 'No indicator data available' : 'Compare against the median, average, min or max of all frameworks or of one language'}
    >
      + indicator
    </button>

    {#if open}
      <div class="panel" role="dialog" aria-label="Choose indicators" on:keydown={onKeydown} tabindex="-1">
        <input
          bind:this={searchEl}
          bind:value={query}
          class="search"
          placeholder="Filter languages…"
          aria-label="Filter languages"
        />

        <div class="list">
          {#each sections.groups as group (group.title)}
            <div class="group-title">{group.title}</div>
            {#each group.rows as row (row.scope.key)}
              <div class="row">
                <div class="scope">
                  <span class="scope-name">{row.scope.label}</span>
                  <span class="count" title="{row.count} frameworks with results for {$selectedTest}">{row.count}</span>
                </div>
                <div class="stats">
                  {#each STATS as stat (stat.key)}
                    {@const id = indicatorId(stat.key, row.scope.key)}
                    {@const on = $selectedIndicators.includes(id)}
                    <button
                      class="stat"
                      class:on
                      style={on ? `--color: ${$seriesColors[`ind:${id}`]}` : ''}
                      aria-pressed={on}
                      title={describe(stat.key, row.scope.key)}
                      on:click={() => toggle(stat.key, row.scope.key)}
                    >{stat.short}</button>
                  {/each}
                </div>
              </div>
            {/each}
          {:else}
            <div class="empty">No language matches “{query}”.</div>
          {/each}
        </div>

        {#if sections.hidden > 0}
          <div class="footnote">
            {sections.hidden} {sections.hidden === 1 ? 'language has' : 'languages have'} no results for {$selectedTest}
          </div>
        {/if}
      </div>
    {/if}
  </div>
</div>

<style>
  /* Lays its chips and button out inline in the shared toolbar. */
  .indicator-picker { display: contents; }

  .chip {
    display: inline-flex;
    align-items: center;
    gap: 5px;
    padding: 3px 8px 3px 9px;
    border-radius: 999px;
    font-size: 13px;
    font-weight: 500;
    background: color-mix(in srgb, var(--color) 12%, transparent);
    color: var(--color);
    /* Dashed: a reference line, not a framework. */
    border: 1px dashed color-mix(in srgb, var(--color) 60%, transparent);
    white-space: nowrap;
  }

  .glyph {
    font-weight: 700;
    font-size: 12px;
    opacity: .85;
  }

  .remove {
    background: none;
    border: none;
    color: inherit;
    cursor: pointer;
    padding: 0 1px;
    font-size: 15px;
    line-height: 1;
    opacity: .7;
  }
  .remove:hover { opacity: 1; }

  .add-wrap { position: relative; }

  .add {
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 6px;
    color: var(--text-2);
    font-size: 13px;
    font-family: inherit;
    padding: 4px 10px;
    cursor: pointer;
    transition: border-color .12s ease, color .12s ease;
  }
  .add:hover:not(:disabled), .add.active { border-color: var(--accent); color: var(--text); }
  .add:disabled { opacity: .5; cursor: default; }

  .panel {
    position: absolute;
    top: calc(100% + 4px);
    left: 0;
    z-index: 100;
    width: 360px;
    display: flex;
    flex-direction: column;
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 8px;
    box-shadow: 0 12px 32px rgba(0, 0, 0, .45);
    outline: none;
  }

  .search {
    margin: 8px 8px 4px;
    background: var(--bg);
    border: 1px solid var(--border);
    border-radius: 6px;
    color: var(--text);
    font-size: 13px;
    font-family: inherit;
    padding: 5px 10px;
    outline: none;
  }
  .search:focus { border-color: var(--accent); }

  .list {
    max-height: min(380px, 60vh);
    overflow-y: auto;
    padding: 0 4px 6px;
    scrollbar-width: thin;
    scrollbar-color: var(--border) transparent;
  }

  .group-title {
    position: sticky;
    top: 0;
    z-index: 1;
    background: var(--surface);
    padding: 8px 8px 4px;
    font-size: .66rem;
    font-weight: 700;
    letter-spacing: .08em;
    text-transform: uppercase;
    color: var(--muted);
  }

  .row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 10px;
    padding: 3px 4px 3px 8px;
    border-radius: 6px;
  }
  .row:hover { background: var(--surface-hover); }

  .scope {
    display: flex;
    align-items: baseline;
    gap: 6px;
    min-width: 0;
  }

  .scope-name {
    font-size: 13px;
    color: var(--text);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .count {
    font-size: 11px;
    color: var(--muted);
    font-variant-numeric: tabular-nums;
  }

  .stats {
    display: flex;
    gap: 3px;
    flex-shrink: 0;
  }

  .stat {
    width: 42px;
    padding: 3px 0;
    border-radius: 5px;
    border: 1px solid var(--border);
    background: none;
    color: var(--text-2);
    font-size: 12px;
    font-family: inherit;
    cursor: pointer;
    transition: border-color .1s ease, color .1s ease, background .1s ease;
  }
  .stat:hover { border-color: var(--accent); color: var(--text); }
  .stat.on {
    border-color: var(--color);
    color: var(--color);
    background: color-mix(in srgb, var(--color) 15%, transparent);
    font-weight: 600;
  }

  .empty, .footnote {
    padding: 8px 12px;
    font-size: 12px;
    color: var(--muted);
  }
  .footnote { border-top: 1px solid var(--border); }

  @media (max-width: 639px) {
    /* Pin the panel to the viewport so it never runs off the right edge. */
    .panel {
      position: fixed;
      top: auto;
      left: 16px;
      right: 16px;
      width: auto;
      margin-top: 4px;
    }
  }
</style>
