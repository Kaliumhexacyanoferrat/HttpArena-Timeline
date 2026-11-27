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
  /* a name plate, as on the rack's units */
  .chip {
    display: inline-flex;
    align-items: center;
    gap: .45rem;
    height: 32px;
    padding: 0 2px 0 .55rem;
    background: var(--plate);
    border: 1px solid var(--plate-line);
    border-radius: 2px;
    box-shadow: inset 0 1px 2px rgba(0,0,0,.12);
    font: 600 .95rem/1 var(--label);
    letter-spacing: .01em;
    color: var(--text);
    white-space: nowrap;
  }

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
    width: 22px;
    height: 22px;
    border: none;
    border-radius: 2px;
    background: none;
    color: var(--muted);
    font: 400 15px/1 var(--sans);
    cursor: pointer;
    opacity: .55;
    transition: opacity .12s ease, color .12s ease, background .12s ease;
  }
  .chip:hover .remove, .remove:focus-visible { opacity: 1; }
  .remove:hover { color: var(--led-r); background: var(--bay); }
</style>
