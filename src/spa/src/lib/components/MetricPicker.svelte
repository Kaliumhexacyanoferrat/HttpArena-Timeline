<script lang="ts">
  import { selectedMetrics } from '../stores/selection'
  import { METRIC_CONFIGS, SCALE_LABEL } from '../data/metrics'

  function toggle(key: string) {
    selectedMetrics.update(ms => {
      if (ms.includes(key)) {
        // Keep at least one metric selected.
        if (ms.length === 1) return ms
        return ms.filter(m => m !== key)
      }
      return [...ms, key]
    })
  }
</script>

<div class="metric-picker">
  {#each METRIC_CONFIGS as m (m.key)}
    <label class:active={$selectedMetrics.includes(m.key)}>
      <input
        type="checkbox"
        checked={$selectedMetrics.includes(m.key)}
        on:change={() => toggle(m.key)}
      />
      {m.label}
      <span class="unit">{SCALE_LABEL[m.scale]}</span>
    </label>
  {/each}
</div>

<style>
  .metric-picker {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
    padding: 10px 16px;
    border-top: 1px solid var(--border);
  }

  label {
    display: inline-flex;
    align-items: center;
    gap: 5px;
    padding: 4px 10px;
    border-radius: 6px;
    border: 1px solid var(--border);
    cursor: pointer;
    font-size: 13px;
    color: var(--text-muted);
    background: var(--surface);
    transition: border-color 0.1s, color 0.1s;
    user-select: none;
  }

  label:hover {
    border-color: var(--accent);
    color: var(--text);
  }

  label.active {
    border-color: var(--accent);
    color: var(--text);
    background: var(--accent-subtle);
  }

  input[type="checkbox"] {
    accent-color: var(--accent);
    width: 13px;
    height: 13px;
  }

  .unit {
    font-size: 11px;
    color: var(--text-muted);
  }
</style>
