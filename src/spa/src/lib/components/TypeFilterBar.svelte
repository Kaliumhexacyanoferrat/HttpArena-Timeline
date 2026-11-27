<script lang="ts">
  import { typeFilter } from '../stores/selection'
  import { TYPES, TUNED, EXCLUSIVE_TYPES, toggleType } from '../data/classification'

  const frameworkTypes = TYPES.filter(t => !EXCLUSIVE_TYPES.includes(t.key))
  const exclusiveTypes = TYPES.filter(t => EXCLUSIVE_TYPES.includes(t.key))

  function toggle(t: string) {
    typeFilter.update(f => ({ ...f, types: toggleType(f.types, t) }))
  }

  const hint = 'Shown in the framework list and aggregated by the indicators.'
</script>

<!--
  HttpArena's type filter: the same keys, lights and toggle rules as the board ("Show", then
  the leagues as banks of keys; a key that is on is raised and lit). Global: it narrows the
  framework picker and decides which frameworks the indicators aggregate.
-->
<div class="type-filter" role="group" aria-label="Framework types">
  <span class="cap">Show</span>
  <div class="banks">
    {#each [frameworkTypes, exclusiveTypes] as bank}
      <div class="kbank">
        {#each bank as t (t.key)}
          {@const on = $typeFilter.types.includes(t.key)}
          <button class:on aria-pressed={on} title="{t.tip} {hint}" on:click={() => toggle(t.key)}>
            <i class="kled" class:on style="--led: {t.color}"></i>{t.label}
          </button>
        {/each}
      </div>
    {/each}
    <div class="kbank">
      <button
        class:on={$typeFilter.showTuned}
        aria-pressed={$typeFilter.showTuned}
        title="Show tuned entries. {TUNED.tip}"
        on:click={() => typeFilter.update(f => ({ ...f, showTuned: !f.showTuned }))}
      ><i class="kled" class:on={$typeFilter.showTuned} style="--led: {TUNED.color}"></i>Tuned</button>
    </div>
  </div>
</div>

<style>
  .type-filter {
    display: flex;
    align-items: center;
    gap: .6rem;
    min-width: 0;
  }

  .cap {
    font: 600 .8125rem/1 var(--sans);
    color: var(--text-2);
    white-space: nowrap;
  }

  .banks {
    display: flex;
    flex-wrap: wrap;
    gap: .5rem;
    min-width: 0;
  }

  /* a bank: keys in a recessed bay */
  .kbank {
    display: inline-flex;
    align-items: stretch;
    gap: 2px;
    padding: 2px;
    background: var(--bay);
    border: 1px solid var(--bay-line);
  }

  button {
    display: inline-flex;
    align-items: center;
    gap: .45rem;
    height: 30px;
    padding: 0 .75rem;
    border: 1px solid transparent;
    background: transparent;
    color: var(--text-2);
    font: 600 .8125rem/1 var(--sans);
    white-space: nowrap;
    cursor: pointer;
  }
  button:hover { color: var(--text); background: color-mix(in srgb, var(--text) 7%, transparent); }
  button.on {
    color: var(--text);
    background: var(--face);
    border-color: var(--edge-lo);
    box-shadow: 0 1px 0 var(--edge-hi) inset, 0 1px 2px rgba(0,0,0,.25);
  }
  button:focus-visible { outline: 2px solid var(--accent); outline-offset: 1px; }

  /* Phones: tighter, so the banks take two rows at most. */
  @media (max-width: 639px) {
    .cap { display: none; }
    .banks { gap: .35rem; }
    button { height: 28px; padding: 0 .5rem; gap: .35rem; font-size: .75rem; }
  }
</style>
