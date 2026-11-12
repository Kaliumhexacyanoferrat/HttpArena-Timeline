<script lang="ts">
  import { selectedRange } from '../stores/selection'
  import { formatRange } from '../data/range'
</script>

<!-- The time filter is set by dragging across the chart (TimelineChart); this shows and clears it. -->
{#if $selectedRange}
  <span class="divider" aria-hidden="true"></span>
  <span class="range" title="Showing {$selectedRange.from} to {$selectedRange.to} (UTC). Drag across the chart to change it, double-click it to reset.">
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
      <rect x="3" y="5" width="18" height="16" rx="2" /><line x1="3" y1="10" x2="21" y2="10" /><line x1="8" y1="3" x2="8" y2="7" /><line x1="16" y1="3" x2="16" y2="7" />
    </svg>
    {formatRange($selectedRange)}
    <button class="remove" on:click={() => selectedRange.set(null)} title="Show the whole history" aria-label="Remove time filter">×</button>
  </span>
{/if}

<style>
  .divider {
    width: 1px;
    height: 16px;
    margin: 0 6px;
    background: var(--border);
  }

  .range {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    height: 26px;
    padding: 0 2px 0 8px;
    border-radius: 6px;
    font-size: 13px;
    font-variant-numeric: tabular-nums;
    color: var(--text);
    background: var(--accent-weak);
    white-space: nowrap;
  }

  svg {
    width: 14px;
    height: 14px;
    color: var(--accent);
    flex-shrink: 0;
  }

  .remove {
    width: 18px;
    height: 18px;
    border: none;
    border-radius: 4px;
    background: none;
    color: var(--text-2);
    font-size: 15px;
    line-height: 1;
    cursor: pointer;
  }
  .remove:hover { color: var(--text); }
</style>
