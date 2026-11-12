<script lang="ts">
  import { selectedMetrics, markMetricsTouched } from '../stores/selection'
  import { METRIC_CONFIGS, METRIC_GROUPS, METRIC_BY_KEY, SCALE_LABEL } from '../data/metrics'
  import PickerPanel from './PickerPanel.svelte'

  let open = false

  const groups = METRIC_GROUPS.map(name => ({ name, metrics: METRIC_CONFIGS.filter(m => m.group === name) }))

  function toggle(key: string) {
    markMetricsTouched()
    selectedMetrics.update(ms => {
      if (ms.includes(key)) {
        // Keep at least one metric selected.
        if (ms.length === 1) return ms
        return ms.filter(m => m !== key)
      }
      return [...ms, key]
    })
  }

  // With several metrics, the trigger doubles as their key: the first is drawn solid, the rest dashed.
  $: shown = $selectedMetrics.slice(0, 2)
  $: more = $selectedMetrics.length - shown.length
</script>

<PickerPanel
  label="Metrics"
  dialogLabel="Choose metrics"
  variant="menu"
  searchable={false}
  align="right"
  width={240}
  title="Choose the metrics to plot"
  bind:open
>
  <svelte:fragment slot="trigger">
    {#each shown as key, i (key)}
      <span class="key">
        {#if $selectedMetrics.length > 1}
          <svg viewBox="0 0 16 10" aria-hidden="true"><path d="M1 5 H15" stroke-dasharray={i > 0 ? '3 3' : undefined} /></svg>
        {/if}
        {METRIC_BY_KEY[key]?.label ?? key}
      </span>
    {/each}
    {#if more > 0}<span class="more">+{more}</span>{/if}
  </svelte:fragment>

  {#each groups as group (group.name)}
    <div class="group-title">{group.name}</div>
    {#each group.metrics as m (m.key)}
      {@const on = $selectedMetrics.includes(m.key)}
      <button class="row metric" class:on role="menuitemcheckbox" aria-checked={on} on:click={() => toggle(m.key)}>
        <span class="check" aria-hidden="true">{on ? '✓' : ''}</span>
        <span class="name">{m.label}</span>
        <span class="unit">{SCALE_LABEL[m.scale]}</span>
      </button>
    {/each}
  {/each}
</PickerPanel>

<style>
  .key { display: inline-flex; align-items: center; gap: 5px; }
  .key + .key { margin-left: 4px; }

  svg {
    width: 14px;
    height: 10px;
    fill: none;
    stroke: var(--text-2);
    stroke-width: 2;
    stroke-linecap: round;
  }

  .more { color: var(--text-2); }

  .metric {
    width: 100%;
    justify-content: flex-start;
    gap: 8px;
    padding-top: 4px;
    padding-bottom: 4px;
    border: none;
    background: none;
    color: var(--text);
    font: inherit;
    font-size: 13px;
    text-align: left;
    cursor: pointer;
  }

  .check {
    flex-shrink: 0;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 15px;
    height: 15px;
    border-radius: 4px;
    border: 1px solid var(--border);
    font-size: 11px;
    font-weight: 700;
    line-height: 1;
  }
  .metric.on .check { border-color: var(--accent); background: var(--accent-weak); color: var(--accent); }

  .name { flex: 1; }
  .unit { font-size: 11px; color: var(--muted); }
</style>
