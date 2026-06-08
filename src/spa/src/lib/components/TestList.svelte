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
  let autoExpandedForTest: string | null = null

  $: if ($selectedTest && $selectedTest !== autoExpandedForTest) {
    autoExpandedForTest = $selectedTest
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
    <div class="nav-grp" class:open={expanded.has(group.name)}>
      <button class="nav-grp-h" on:click={() => toggle(group.name)}>
        <span class="grp-label">
          <span class="cat-dot" style="background:{group.color}" aria-hidden="true"></span>
          {group.name}
        </span>
        <span class="caret" aria-hidden="true">▸</span>
      </button>

      <div class="nav-grp-body">
        <div class="nav-grp-inner">
          {#each group.tests as test (test)}
            <button
              class="nav-item"
              class:active={$selectedTest === test}
              on:click={() => selectedTest.set(test)}
            >
              {test}
            </button>
          {/each}
        </div>
      </div>
    </div>
  {/each}
</nav>

<style>
  .test-list {
    display: flex;
    flex-direction: column;
    padding: .4rem .7rem 3rem;
  }

  .nav-grp {
    display: flex;
    flex-direction: column;
  }

  /* Category header — nav-grp-h style */
  .nav-grp-h {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: .4rem;
    width: 100%;
    padding: .44rem .55rem;
    border: none;
    border-radius: 7px;
    background: none;
    color: var(--text);
    font-size: .86rem;
    font-weight: 650;
    font-family: inherit;
    cursor: pointer;
    text-align: left;
    user-select: none;
    transition: background .12s ease, color .12s ease;
    -webkit-tap-highlight-color: transparent;
  }
  .nav-grp-h:hover { background: var(--panel-2); }

  .grp-label {
    display: flex;
    align-items: center;
    gap: .5rem;
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .cat-dot {
    width: 7px;
    height: 7px;
    border-radius: 50%;
    flex-shrink: 0;
  }

  /* Rotating caret — matches .caret in the new leaderboard */
  .caret {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: .85rem;
    font-size: .62rem;
    color: var(--muted);
    transition: transform .22s cubic-bezier(.4,0,.2,1);
    flex-shrink: 0;
  }
  .nav-grp.open > .nav-grp-h .caret { transform: rotate(90deg); }

  /* Accordion body with tree line — border-left pattern from new leaderboard */
  .nav-grp-body {
    margin-left: .8rem;
    border-left: 1px solid var(--border);
    padding-left: .5rem;
    display: grid;
    grid-template-rows: 0fr;
    transition: grid-template-rows .22s cubic-bezier(.4,0,.2,1);
  }
  .nav-grp.open > .nav-grp-body { grid-template-rows: 1fr; }

  .nav-grp-inner {
    overflow: hidden;
    min-height: 0;
    display: flex;
    flex-direction: column;
    gap: 1px;
    opacity: 0;
    transition: opacity .16s ease .04s;
  }
  .nav-grp.open > .nav-grp-body > .nav-grp-inner { opacity: 1; }

  /* Test item — nav-item style */
  .nav-item {
    display: block;
    width: 100%;
    text-align: left;
    padding: .4rem .6rem;
    border: none;
    border-radius: 7px;
    background: none;
    color: var(--text-2);
    cursor: pointer;
    font-size: .85rem;
    font-weight: 500;
    font-family: inherit;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    transition: background .12s ease, color .12s ease, box-shadow .12s ease, transform .12s ease;
    -webkit-tap-highlight-color: transparent;
  }
  .nav-item:hover {
    background: var(--panel-2);
    color: var(--text);
    transform: translateX(2px);
  }
  .nav-item.active {
    background: var(--accent-weak);
    color: var(--accent);
    font-weight: 600;
    box-shadow: inset 2px 0 0 var(--accent);
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
    .nav-grp { flex-direction: row; flex-shrink: 0; }
    .nav-grp-h { display: none; }
    .nav-grp-body {
      display: flex;
      flex-direction: row;
      border-left: none;
      padding-left: 0;
      margin-left: 0;
      grid-template-rows: none;
    }
    .nav-grp-inner {
      display: flex;
      flex-direction: row;
      opacity: 1;
      gap: 4px;
    }
    .nav-item {
      flex-shrink: 0;
      overflow: visible;
      text-overflow: unset;
      transform: none !important;
    }
  }
</style>
