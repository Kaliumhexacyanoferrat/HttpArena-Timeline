<script lang="ts">
  import { selectedTest } from '../stores/selection'
  import { CATEGORIES } from '../data/categories'

  export let tests: string[]

  $: testSet = new Set(tests)

  $: groups = (() => {
    const result: { name: string; tests: string[]; color: string; bg: string }[] = []
    const seen = new Set<string>()

    for (const cat of CATEGORIES) {
      const matching = cat.tests.filter(t => testSet.has(t)).sort()
      if (matching.length > 0) {
        result.push({ name: cat.name, tests: matching, color: cat.color, bg: cat.bg })
        matching.forEach(t => seen.add(t))
      }
    }

    const uncategorized = tests.filter(t => !seen.has(t))
    if (uncategorized.length > 0) {
      result.push({ name: 'Other', tests: uncategorized, color: 'var(--text-muted)', bg: 'transparent' })
    }

    return result
  })()

  let expanded = new Set<string>()

  $: if ($selectedTest) {
    for (const group of groups) {
      if (group.tests.includes($selectedTest)) {
        expanded = new Set([...expanded, group.name])
        break
      }
    }
  }

  function toggle(name: string) {
    const next = new Set(expanded)
    if (next.has(name)) next.delete(name)
    else next.add(name)
    expanded = next
  }
</script>

<nav class="test-list">
  {#each groups as group (group.name)}
    <div class="group">
      <button
        class="group-header"
        style="--cat-color:{group.color};--cat-bg:{group.bg}"
        on:click={() => toggle(group.name)}
      >
        <span class="chevron" class:open={expanded.has(group.name)}>›</span>
        {group.name}
      </button>

      {#if expanded.has(group.name)}
        <div class="group-tests">
          {#each group.tests as test (test)}
            <button
              class="test-btn"
              class:active={$selectedTest === test}
              on:click={() => selectedTest.set(test)}
            >
              {test}
            </button>
          {/each}
        </div>
      {/if}
    </div>
  {/each}
</nav>

<style>
  .test-list {
    display: flex;
    flex-direction: column;
    padding: 6px 0;
  }

  .group {
    display: flex;
    flex-direction: column;
  }

  .group-header {
    display: flex;
    align-items: center;
    gap: 6px;
    width: 100%;
    padding: 7px 12px;
    border: none;
    background: var(--cat-bg);
    color: var(--cat-color);
    font-size: 11px;
    font-weight: 700;
    font-family: inherit;
    text-transform: uppercase;
    letter-spacing: 0.07em;
    cursor: pointer;
    text-align: left;
    border-top: 1px solid var(--border);
    transition: filter 0.1s;
  }

  .group:first-child .group-header {
    border-top: none;
  }

  .group-header:hover {
    filter: brightness(1.15);
  }

  .chevron {
    font-size: 14px;
    line-height: 1;
    opacity: 0.7;
    transform: rotate(0deg);
    transition: transform 0.15s;
    display: inline-block;
  }

  .chevron.open {
    transform: rotate(90deg);
  }

  .group-tests {
    display: flex;
    flex-direction: column;
    gap: 1px;
    padding: 2px 8px 6px;
  }

  .test-btn {
    text-align: left;
    padding: 6px 10px;
    border-radius: 4px;
    border: none;
    background: none;
    color: var(--text-muted);
    cursor: pointer;
    font-size: 13px;
    font-family: inherit;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    transition: background 0.1s, color 0.1s;
  }

  .test-btn:hover {
    background: var(--surface-hover);
    color: var(--text);
  }

  .test-btn.active {
    background: rgba(255,255,255,0.08);
    color: #fff;
    font-weight: 500;
  }

  @media (max-width: 639px) {
    .test-list {
      flex-direction: row;
      overflow-x: auto;
      padding: 6px 8px;
      scrollbar-width: none;
      gap: 4px;
      align-items: center;
    }

    .test-list::-webkit-scrollbar { display: none; }

    .group { flex-direction: row; flex-shrink: 0; }

    .group-header { display: none; }

    .group-tests {
      flex-direction: row;
      padding: 0;
      gap: 4px;
    }

    .test-btn {
      flex-shrink: 0;
      overflow: visible;
      text-overflow: unset;
    }
  }
</style>
