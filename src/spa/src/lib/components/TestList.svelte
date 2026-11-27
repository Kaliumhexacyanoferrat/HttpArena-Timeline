<script lang="ts">
  import { selectedTest } from '../stores/selection'
  import { CATEGORIES, profileOf, splitTest, formatConnections } from '../data/categories'

  export let tests: string[]

  $: testSet = new Set(tests)

  // In the board's order, which categories.ts keeps.
  $: groups = CATEGORIES
    .map(c => ({ name: c.name, short: c.short, tests: c.tests.filter(t => testSet.has(t)) }))
    .filter(g => g.tests.length > 0)

  let expanded = new Set<string>()
  let autoExpandedForTest: string | null = null

  $: if ($selectedTest && $selectedTest !== autoExpandedForTest) {
    autoExpandedForTest = $selectedTest
    const group = groups.find(g => g.tests.includes($selectedTest))
    if (group) expanded = new Set([...expanded, group.name])
  }

  function toggle(name: string) {
    const next = new Set(expanded)
    if (next.has(name)) next.delete(name)
    else next.add(name)
    expanded = next
  }
</script>

<!-- The board's views rail, opened: a key per family, its tests listed under it by name. -->
<nav class="rail" aria-label="Tests">
  <div class="rail-h">Tests</div>
  {#each groups as group (group.name)}
    {@const open = expanded.has(group.name)}
    {@const current = group.tests.includes($selectedTest)}
    <div class="fam" class:open>
      <button class="fam-key" class:current on:click={() => toggle(group.name)} aria-expanded={open}>
        <i class="kled" class:on={current}></i>
        <span class="fam-name">{group.name}</span>
        <span class="fam-n">{group.tests.length}</span>
        <span class="caret" aria-hidden="true">▸</span>
      </button>

      <div class="fam-body">
        <div class="fam-inner">
          {#each group.tests as test (test)}
            {@const { connections } = splitTest(test)}
            <button
              class="item"
              class:on={$selectedTest === test}
              aria-current={$selectedTest === test ? 'true' : undefined}
              title={test}
              on:click={() => selectedTest.set(test)}
            >
              <span class="item-t">{profileOf(test)?.label ?? test}</span>
              {#if connections != null}<span class="item-c">{formatConnections(connections)}</span>{/if}
            </button>
          {/each}
        </div>
      </div>
    </div>
  {/each}
</nav>

<style>
  .rail {
    display: flex;
    flex-direction: column;
    padding: 1.1rem .55rem 3rem;
  }

  .rail-h {
    margin: 0 .2rem .55rem;
    font: 600 .75rem/1.2 var(--sans);
    color: var(--text-2);
  }

  .fam { margin-bottom: 3px; }

  /* a family: a key on the rail */
  .fam-key {
    display: flex;
    align-items: center;
    gap: .5rem;
    width: 100%;
    min-height: 32px;
    padding: .35rem .5rem;
    border: 1px solid var(--edge-lo);
    background: var(--face);
    box-shadow: 0 1px 0 var(--edge-hi) inset;
    color: var(--text-2);
    font: 600 .8rem/1 var(--label);
    letter-spacing: .04em;
    text-transform: uppercase;
    text-align: left;
    white-space: nowrap;
    cursor: pointer;
    user-select: none;
  }
  .fam-key:hover, .fam.open > .fam-key { color: var(--text); background: var(--face-h); }
  .fam-key.current { color: var(--text); }
  .fam-key:focus-visible { outline: 2px solid var(--accent); outline-offset: 1px; }

  .fam-name { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; }

  .fam-n {
    font: 500 .7rem/1 var(--mono);
    letter-spacing: 0;
    color: var(--muted);
  }

  .caret {
    display: inline-flex;
    justify-content: center;
    width: .7rem;
    font-size: .6rem;
    color: var(--muted);
    transition: transform .22s cubic-bezier(.4,0,.2,1);
  }
  .fam.open > .fam-key .caret { transform: rotate(90deg); }

  /* smooth accordion: the row track 0fr -> 1fr, the contents fade */
  .fam-body {
    display: grid;
    grid-template-rows: 0fr;
    transition: grid-template-rows .22s cubic-bezier(.4,0,.2,1);
  }
  .fam.open > .fam-body { grid-template-rows: 1fr; }

  .fam-inner {
    overflow: hidden;
    min-height: 0;
    display: flex;
    flex-direction: column;
    opacity: 0;
    transition: opacity .16s ease .04s;
  }
  .fam.open > .fam-body > .fam-inner { opacity: 1; padding: .2rem 0 .5rem; }

  /* a test: a line of the list, lit on the left when on screen */
  .item {
    display: flex;
    align-items: baseline;
    gap: .5rem;
    width: 100%;
    padding: .36rem .55rem .36rem .9rem;
    border: 0;
    border-left: 3px solid transparent;
    background: none;
    color: var(--text-2);
    font: 500 .86rem/1.3 var(--sans);
    text-align: left;
    cursor: pointer;
    -webkit-tap-highlight-color: transparent;
  }
  .item:hover { background: var(--panel-2); color: var(--text); }
  .item.on {
    border-left-color: var(--led-g);
    background: var(--accent-weak);
    color: var(--text);
    font-weight: 650;
  }
  .item:focus-visible { outline: 2px solid var(--accent); outline-offset: -2px; }

  .item-t { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

  .item-c {
    flex: none;
    font: 500 .74rem/1 var(--mono);
    color: var(--muted);
    font-variant-numeric: tabular-nums;
  }
  .item.on .item-c { color: var(--text-2); }
</style>
