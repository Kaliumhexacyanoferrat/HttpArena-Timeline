<script lang="ts">
  import { selectedRange } from '../stores/selection'
  import { formatRange } from '../data/range'
</script>

<!-- The time filter is set by dragging across the chart (TimelineChart); this shows and clears it. -->
{#if $selectedRange}
  <span class="range" title="Showing {$selectedRange.from} to {$selectedRange.to} (UTC). Drag across the chart to change it, double-click it to reset.">
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
      <rect x="3" y="5" width="18" height="16" rx="2" /><line x1="3" y1="10" x2="21" y2="10" /><line x1="8" y1="3" x2="8" y2="7" /><line x1="16" y1="3" x2="16" y2="7" />
    </svg>
    {formatRange($selectedRange)}
    <button class="remove" on:click={() => selectedRange.set(null)} title="Show the whole history" aria-label="Remove time filter">×</button>
  </span>
{/if}

<style>
  /* a plate like the series beside it, lit in the accent: it narrows every series */
  .range {
    display: inline-flex;
    align-items: center;
    gap: .45rem;
    height: 32px;
    padding: 0 2px 0 .55rem;
    border: 1px solid color-mix(in srgb, var(--accent) 60%, var(--bay-line));
    border-radius: 2px;
    background: color-mix(in srgb, var(--accent) 8%, var(--panel));
    font: 500 .8rem/1 var(--mono);
    font-variant-numeric: tabular-nums;
    color: var(--text);
    white-space: nowrap;
  }

  svg {
    width: 14px;
    height: 14px;
    color: var(--accent);
    flex-shrink: 0;
  }

  .remove {
    width: 22px;
    height: 22px;
    border: none;
    border-radius: 2px;
    background: none;
    color: var(--text-2);
    font: 400 15px/1 var(--sans);
    cursor: pointer;
  }
  .remove:hover { color: var(--led-r); background: var(--bay); }
</style>
