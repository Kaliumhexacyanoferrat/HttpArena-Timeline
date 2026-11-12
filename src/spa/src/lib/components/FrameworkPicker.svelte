<script lang="ts">
  import { selectedTest, selectedFrameworks, seriesColors, typeFilter } from '../stores/selection'
  import { typeConfig, passesTypeFilter, classificationLabel, TUNED } from '../data/classification'
  import PickerPanel from './PickerPanel.svelte'
  import Chip from './Chip.svelte'
  import type { FrameworkInfo } from '../data/types'

  export let frameworks: Record<string, FrameworkInfo>

  interface Row { name: string; info: FrameworkInfo; hasData: boolean }

  let open = false
  let query = ''
  // Frameworks without results for the selected test are hidden unless asked for.
  let showAll = false

  $: hasData = (fw: string) => !$selectedTest || (frameworks[fw]?.tests.includes($selectedTest) ?? false)

  $: view = (() => {
    const q = query.trim().toLowerCase()
    const matches = (fw: string, language: string) =>
      !q || fw.toLowerCase().includes(q) || language.toLowerCase().includes(q)

    const byLanguage = new Map<string, Row[]>()
    let hidden = 0
    let hiddenByType = 0

    for (const name of Object.keys(frameworks).sort((a, b) => a.localeCompare(b))) {
      const info = frameworks[name]
      const language = info.language || 'Other'
      if (!matches(name, language)) continue
      const row = { name, info, hasData: hasData(name) }
      // Frameworks on the chart stay listed so they can be removed here too.
      const selected = $selectedFrameworks.includes(name)
      if (!selected && !passesTypeFilter(info, $typeFilter.types, $typeFilter.showTuned)) {
        // Only count what the type filter alone keeps out of the list.
        if (row.hasData || showAll) hiddenByType++
        continue
      }
      if (!row.hasData && !showAll && !selected) { hidden++; continue }
      if (!byLanguage.has(language)) byLanguage.set(language, [])
      byLanguage.get(language)!.push(row)
    }

    // Biggest ecosystems first; that is where most lookups go.
    const groups = [...byLanguage.entries()]
      .map(([language, rows]) => ({ language, rows }))
      .sort((a, b) => b.rows.length - a.rows.length || a.language.localeCompare(b.language))

    return { groups, hidden, hiddenByType }
  })()

  function toggle(fw: string) {
    selectedFrameworks.update(fws => fws.includes(fw) ? fws.filter(f => f !== fw) : [...fws, fw])
  }

  function remove(fw: string) {
    selectedFrameworks.update(fws => fws.filter(f => f !== fw))
  }

  // Enter adds the first match not on the chart yet, then clears the search for the next one.
  function onSearchKeydown(e: KeyboardEvent) {
    if (e.key !== 'Enter') return
    const first = view.groups.flatMap(g => g.rows).find(r => !$selectedFrameworks.includes(r.name))
    if (first) {
      toggle(first.name)
      query = ''
    }
  }
</script>

<div class="framework-picker">
  {#each $selectedFrameworks as fw (fw)}
    <Chip
      color={$seriesColors[`fw:${fw}`]}
      title="{frameworks[fw]?.language} · {classificationLabel(frameworks[fw])}"
      removeLabel="Remove {fw}"
      on:remove={() => remove(fw)}
    >{fw}</Chip>
  {/each}

  <PickerPanel
    label="+ Framework"
    dialogLabel="Choose frameworks"
    placeholder="Filter frameworks or languages…"
    title="Add frameworks to the chart"
    bind:open
    bind:query
    on:keydown={onSearchKeydown}
  >
    {#each view.groups as group (group.language)}
      <div class="group-title">
        <span>{group.language}</span>
        <span class="group-count">{group.rows.length}</span>
      </div>
      {#each group.rows as row (row.name)}
        {@const on = $selectedFrameworks.includes(row.name)}
        <button
          class="row fw"
          class:on
          class:no-data={!row.hasData}
          style={on ? `--color: ${$seriesColors[`fw:${row.name}`]}` : ''}
          aria-pressed={on}
          on:click={() => toggle(row.name)}
        >
          <span class="check" aria-hidden="true">{on ? '✓' : ''}</span>
          <span
            class="swatch"
            style="background: {typeConfig(row.info)?.color ?? 'var(--muted)'}"
            title={classificationLabel(row.info)}
          ></span>
          <span class="fw-name">{row.name}</span>
          {#if row.info.tuned}
            <span class="tuned" style="--on-text: {TUNED.text}; --on-bg: {TUNED.background}" title={TUNED.tip}>tuned</span>
          {/if}
          {#if !row.hasData}
            <span class="tag">no results</span>
          {/if}
        </button>
      {/each}
    {:else}
      <div class="empty">
        {query ? `No framework matches “${query}”` : 'No frameworks'}{view.hiddenByType ? ' in the selected types' : ''}.
      </div>
    {/each}

    <svelte:fragment slot="footer">
      {#if view.hiddenByType > 0}
        <div class="footnote">
          {view.hiddenByType} {view.hiddenByType === 1 ? (query ? 'match is' : 'framework is') : (query ? 'matches are' : 'frameworks are')} hidden by the type filter
        </div>
      {/if}
      {#if showAll && $selectedTest}
        <div class="footnote">
          <button class="link" on:click={() => (showAll = false)}>Hide frameworks without results for {$selectedTest}</button>
        </div>
      {:else if view.hidden > 0}
        <div class="footnote">
          {view.hidden} {view.hidden === 1 ? 'framework has' : 'frameworks have'} no results for {$selectedTest} ·
          <button class="link" on:click={() => (showAll = true)}>show</button>
        </div>
      {/if}
    </svelte:fragment>
  </PickerPanel>
</div>

<style>
  /* Lays its chips and button out inline in the shared toolbar. */
  .framework-picker { display: contents; }

  .group-count {
    font-weight: 500;
    letter-spacing: 0;
    font-variant-numeric: tabular-nums;
  }

  .fw {
    width: 100%;
    justify-content: flex-start;
    gap: 8px;
    padding-top: 4px;
    padding-bottom: 4px;
    border: none;
    background: none;
    color: var(--text);
    font-size: 13px;
    font-family: inherit;
    text-align: left;
    cursor: pointer;
  }
  .fw.no-data .fw-name { color: var(--muted); }

  .check {
    flex-shrink: 0;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 15px;
    height: 15px;
    border-radius: 4px;
    border: 1px solid var(--border);
    font-size: 11px;
    font-weight: 700;
    line-height: 1;
  }
  .fw:hover .check { border-color: var(--accent); }
  .fw.on .check {
    border-color: var(--color);
    background: color-mix(in srgb, var(--color) 20%, transparent);
    color: var(--color);
  }
  .fw.on .fw-name { color: var(--color); font-weight: 600; }

  .fw-name {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .swatch {
    flex-shrink: 0;
    width: 8px;
    height: 8px;
    border-radius: 2px;
  }

  .tuned {
    flex-shrink: 0;
    padding: 0 5px;
    border-radius: 4px;
    font-size: 10px;
    font-weight: 650;
    letter-spacing: .02em;
    color: var(--on-text);
    background: var(--on-bg);
  }

  .tag {
    font-size: 11px;
    color: var(--muted);
    white-space: nowrap;
  }

  .link {
    background: none;
    border: none;
    padding: 0;
    color: var(--accent);
    font: inherit;
    cursor: pointer;
  }
  .link:hover { text-decoration: underline; }
</style>
