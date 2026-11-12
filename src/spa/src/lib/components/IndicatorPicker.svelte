<script lang="ts">
  import { selectedTest, selectedFrameworks, selectedIndicators, seriesColors, typeFilter } from '../stores/selection'
  import { STATS, STAT_BY_KEY, indicatorId, parseIndicator, indicatorLabel, scopeLabel, type StatKey } from '../data/indicators'
  import { passesTypeFilter, describeFilter } from '../data/classification'
  import PickerPanel from './PickerPanel.svelte'
  import Chip from './Chip.svelte'
  import type { FrameworkInfo, IndicatorScope } from '../data/types'

  export let scopes: IndicatorScope[]
  export let frameworks: Record<string, FrameworkInfo>

  interface Row { scope: IndicatorScope; count: number }

  let open = false
  let query = ''

  // How many frameworks each scope aggregates for the selected test under the type filter.
  $: counts = (() => {
    const byLanguage: Record<string, number> = {}
    let total = 0
    for (const info of Object.values(frameworks)) {
      if (!info.tests.includes($selectedTest) || !passesTypeFilter(info, $typeFilter.types, $typeFilter.showTuned)) continue
      total++
      byLanguage[info.language] = (byLanguage[info.language] ?? 0) + 1
    }
    return { total, byLanguage }
  })()

  // Languages of the frameworks on the chart come first: the obvious thing to compare against.
  $: selectedLanguages = [...new Set($selectedFrameworks.map(fw => frameworks[fw]?.language).filter(Boolean))]

  $: sections = (() => {
    const q = query.trim().toLowerCase()
    const matches = (s: IndicatorScope) => !q || s.label.toLowerCase().includes(q) || s.key.includes(q)

    // Reads `counts` here (not via a helper) so Svelte recomputes on test and filter changes.
    const rows: Row[] = scopes.map(scope => ({
      scope,
      count: scope.language ? counts.byLanguage[scope.language] ?? 0 : counts.total,
    }))
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

  $: fieldText = describeFilter($typeFilter)

  function describe(stat: StatKey, scope: string): string {
    const s = scopes.find(x => x.key === scope)
    const who = s?.language ? `${s.language} frameworks` : 'frameworks'
    const extra = stat === 'min' || stat === 'max' ? ', naming the framework that set it' : ''
    return `${STAT_BY_KEY[stat].label} of ${who} with results for the test (${fieldText}), tracked daily${extra}`
  }
</script>

<div class="indicator-picker">
  {#each refs as ref (ref.id)}
    <Chip
      color={$seriesColors[`ind:${ref.id}`]}
      kind="step"
      title={describe(ref.stat, ref.scope)}
      removeLabel="Remove {indicatorLabel(ref, scopes)}"
      on:remove={() => remove(ref.id)}
    >
      {STAT_BY_KEY[ref.stat].label}<span class="chip-scope">{scopeLabel(ref.scope, scopes)}</span>
    </Chip>
  {/each}

  <PickerPanel
    label="+ Indicator"
    dialogLabel="Choose indicators"
    placeholder="Filter languages…"
    disabled={scopes.length === 0}
    title={scopes.length === 0 ? 'No indicator data available' : 'Compare against the median, average, min or max of all frameworks or of one language'}
    bind:open
    bind:query
  >
    <svelte:fragment slot="header">
      <div class="field" title="Change it with the type filter at the top. Classification is today's, applied to the whole history.">
        Aggregates {fieldText}
      </div>
    </svelte:fragment>

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

    <svelte:fragment slot="footer">
      {#if sections.hidden > 0}
        <div class="footnote">
          {sections.hidden} {sections.hidden === 1 ? 'language has' : 'languages have'} no results for {$selectedTest}
        </div>
      {/if}
    </svelte:fragment>
  </PickerPanel>
</div>

<style>
  /* Lays its chips and button out inline in the shared toolbar. */
  .indicator-picker { display: contents; }

  .chip-scope { color: var(--text-2); }

  .field {
    margin: 2px 12px 4px;
    font-size: 12px;
    color: var(--muted);
  }

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
</style>
