<script lang="ts">
  import { typeFilter } from '../stores/selection'
  import { TYPES, TUNED, EXCLUSIVE_TYPES, toggleType } from '../data/classification'

  const frameworkTypes = TYPES.filter(t => !EXCLUSIVE_TYPES.includes(t.key))
  const exclusiveTypes = TYPES.filter(t => EXCLUSIVE_TYPES.includes(t.key))

  function toggle(t: string) {
    typeFilter.update(f => ({ ...f, types: toggleType(f.types, t) }))
  }
</script>

<!--
  HttpArena's type filter: the same buttons, colours and toggle rules as the leaderboard. Global:
  it narrows the framework picker and decides which frameworks the indicators aggregate.
-->
<div class="type-filter" role="group" aria-label="Framework types">
  {#each frameworkTypes as t (t.key)}
    {@const on = $typeFilter.types.includes(t.key)}
    <button class:on aria-pressed={on} title="{t.tip} Shown in the framework list and aggregated by the indicators." style="--on-text: {t.text}; --on-bg: {t.background}" on:click={() => toggle(t.key)}>{t.label}</button>
  {/each}
  <span class="sep" aria-hidden="true"></span>
  {#each exclusiveTypes as t (t.key)}
    {@const on = $typeFilter.types.includes(t.key)}
    <button class:on aria-pressed={on} title="{t.tip} Shown in the framework list and aggregated by the indicators." style="--on-text: {t.text}; --on-bg: {t.background}" on:click={() => toggle(t.key)}>{t.label}</button>
  {/each}
  <span class="sep" aria-hidden="true"></span>
  <button
    class:on={$typeFilter.showTuned}
    aria-pressed={$typeFilter.showTuned}
    title="Show tuned entries. {TUNED.tip}"
    style="--on-text: {TUNED.text}; --on-bg: {TUNED.background}"
    on:click={() => typeFilter.update(f => ({ ...f, showTuned: !f.showTuned }))}
  >Tuned</button>
</div>

<style>
  /* Sized and placed like the leaderboard's top-bar filter. */
  .type-filter {
    display: flex;
    align-items: center;
    gap: .25rem;
    margin-right: auto;
    padding: 3px;
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 10px;
    min-width: 0;
    overflow-x: auto;
    scrollbar-width: none;
  }

  button {
    flex: none;
    border: 0;
    background: transparent;
    color: var(--muted);
    cursor: pointer;
    font: inherit;
    font-size: .8rem;
    font-weight: 650;
    padding: .36rem .72rem;
    border-radius: 7px;
    white-space: nowrap;
    transition: color .12s ease, background .12s ease;
  }
  button:hover { color: var(--text-2); }
  button.on { color: var(--on-text); background: var(--on-bg); }

  /* Phones: tighter, so all six fit on one row. */
  @media (max-width: 639px) {
    .type-filter { gap: 2px; }
    button { font-size: 11.5px; padding: 5px 5px; }
    .sep { margin: 0 1px; }
  }

  .sep {
    flex: none;
    width: 1px;
    height: 18px;
    margin: 0 .2rem;
    background: var(--border);
  }
</style>
