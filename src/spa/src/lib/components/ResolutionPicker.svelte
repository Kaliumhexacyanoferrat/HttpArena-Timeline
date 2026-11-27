<script lang="ts">
  import { selectedResolution } from '../stores/selection'
  import { RESOLUTIONS } from '../data/resolution'
</script>

<!-- One point per period, or every point: the board's segmented switch (its View row). -->
<div class="res">
  <span class="cap" id="res-cap">Resolution</span>
  <div class="seg" role="radiogroup" aria-labelledby="res-cap">
    {#each RESOLUTIONS as r (r.key)}
      {@const on = r.key === $selectedResolution}
      <button class:on role="radio" aria-checked={on} title={r.title} on:click={() => selectedResolution.set(r.key)}>{r.short}</button>
    {/each}
  </div>
</div>

<style>
  .res {
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

  .seg {
    display: inline-flex;
    gap: 2px;
    padding: 2px;
    background: var(--bay);
    border: 1px solid var(--bay-line);
    min-width: 0;
    overflow-x: auto;
    scrollbar-width: none;
  }

  button {
    flex: none;
    height: 30px;
    padding: 0 .75rem;
    border: 1px solid transparent;
    background: transparent;
    color: var(--text-2);
    font: 600 .8125rem/1 var(--sans);
    white-space: nowrap;
    cursor: pointer;
  }
  button:hover { color: var(--text); }
  button.on {
    color: var(--text);
    background: var(--face);
    border-color: var(--edge-lo);
    box-shadow: 0 1px 0 var(--edge-hi) inset;
  }
  button:focus-visible { outline: 2px solid var(--accent); outline-offset: -2px; }

  @media (max-width: 639px) {
    .cap { display: none; }
    button { padding: 0 .55rem; }
  }
</style>
