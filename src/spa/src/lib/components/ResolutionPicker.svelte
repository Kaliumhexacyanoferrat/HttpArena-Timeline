<script lang="ts">
  import { selectedResolution } from '../stores/selection'
  import { RESOLUTIONS } from '../data/resolution'
  import PickerPanel from './PickerPanel.svelte'

  let open = false

  $: current = RESOLUTIONS.find(r => r.key === $selectedResolution) ?? RESOLUTIONS[0]

  function pick(key: typeof RESOLUTIONS[number]['key']) {
    selectedResolution.set(key)
    open = false
  }
</script>

<PickerPanel
  label={current.label}
  dialogLabel="Choose the time resolution"
  variant="menu"
  searchable={false}
  align="right"
  width={270}
  title="Time resolution: {current.title.toLowerCase()}"
  bind:open
>
  {#each RESOLUTIONS as r (r.key)}
    {@const on = r.key === $selectedResolution}
    <button class="row option" class:on role="menuitemradio" aria-checked={on} on:click={() => pick(r.key)}>
      <span class="name">{r.label}</span>
      <span class="hint">{r.title}</span>
    </button>
  {/each}
</PickerPanel>

<style>
  .option {
    width: 100%;
    flex-direction: column;
    align-items: flex-start !important;
    gap: 0 !important;
    padding-top: 5px;
    padding-bottom: 5px;
    border: none;
    background: none;
    color: var(--text);
    font: inherit;
    text-align: left;
    cursor: pointer;
  }
  .option:first-child { margin-top: 4px; }
  .option.on .name { color: var(--accent); font-weight: 600; }

  .name { font-size: 13px; }
  .hint { font-size: 11.5px; color: var(--muted); }
</style>
