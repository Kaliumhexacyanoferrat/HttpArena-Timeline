<script lang="ts">
  import { createEventDispatcher } from 'svelte'

  /** Series color. */
  export let color: string
  /** How the series is drawn: a plain line (frameworks) or steps (indicators). */
  export let kind: 'line' | 'step' = 'line'
  export let title = ''
  export let removeLabel = 'Remove'

  const dispatch = createEventDispatcher<{ remove: void }>()
</script>

<!-- A selected series. Doubles as the chart legend: the swatch draws the line the way the chart does. -->
<span class="chip" style="--color: {color}" {title}>
  <svg class="swatch" viewBox="0 0 16 10" aria-hidden="true">
    {#if kind === 'step'}
      <path d="M1 8 H6 V2 H11 V5 H15" />
    {:else}
      <path d="M1 5 H15" />
    {/if}
  </svg>
  <span class="label"><slot /></span>
  <button class="remove" on:click={() => dispatch('remove')} aria-label={removeLabel} title={removeLabel}>×</button>
</span>

<style>
  .chip {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    height: 26px;
    padding: 0 2px 0 6px;
    border-radius: 6px;
    font-size: 13px;
    color: var(--text);
    white-space: nowrap;
    transition: background .12s ease;
  }
  .chip:hover { background: var(--surface); }

  .swatch {
    width: 16px;
    height: 10px;
    flex-shrink: 0;
    fill: none;
    stroke: var(--color);
    stroke-width: 2.4;
    stroke-linecap: round;
    stroke-linejoin: round;
  }

  .label { display: inline-flex; align-items: center; gap: 4px; }

  .remove {
    width: 18px;
    height: 18px;
    border: none;
    border-radius: 4px;
    background: none;
    color: var(--muted);
    font-size: 15px;
    line-height: 1;
    cursor: pointer;
    opacity: 0;
    transition: opacity .12s ease, color .12s ease;
  }
  .chip:hover .remove, .remove:focus-visible { opacity: 1; }
  .remove:hover { color: var(--text); }

  /* No hover on touch screens: keep the remove button visible. */
  @media (hover: none) {
    .remove { opacity: .7; }
  }
</style>
