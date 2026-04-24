<script lang="ts">
  import { selectedFrameworks } from '../stores/selection'
  import { FRAMEWORK_COLORS } from '../data/metrics'
  import type { FrameworkInfo } from '../data/types'

  export let frameworks: Record<string, FrameworkInfo>

  let query = ''
  let open = false
  let inputEl: HTMLInputElement

  $: colorMap = Object.fromEntries(
    $selectedFrameworks.map((fw, i) => [fw, FRAMEWORK_COLORS[i % FRAMEWORK_COLORS.length]])
  )

  $: suggestions = Object.keys(frameworks)
    .filter(fw => !$selectedFrameworks.includes(fw) && fw.toLowerCase().includes(query.toLowerCase()))
    .slice(0, 12)

  function add(fw: string) {
    selectedFrameworks.update(fws => [...fws, fw])
    query = ''
    open = false
  }

  function remove(fw: string) {
    selectedFrameworks.update(fws => fws.filter(f => f !== fw))
  }

  function onKeydown(e: KeyboardEvent) {
    if (e.key === 'Escape') { open = false; query = '' }
    if (e.key === 'Enter' && suggestions.length > 0) add(suggestions[0])
  }

  function onBlur() {
    // Delay close so click on suggestion registers first.
    setTimeout(() => { open = false }, 150)
  }
</script>

<div class="picker">
  {#each $selectedFrameworks as fw (fw)}
    <span class="chip" style="--color: {colorMap[fw]}">
      {fw}
      <button class="remove" on:click={() => remove(fw)} title="Remove">×</button>
    </span>
  {/each}

  <div class="add-wrap">
    <input
      bind:this={inputEl}
      bind:value={query}
      placeholder="+ framework"
      on:focus={() => (open = true)}
      on:blur={onBlur}
      on:keydown={onKeydown}
    />
    {#if open && suggestions.length > 0}
      <ul class="dropdown">
        {#each suggestions as fw (fw)}
          <li>
            <button on:mousedown|preventDefault={() => add(fw)}>
              <span class="fw-name">{fw}</span>
              <span class="lang">{frameworks[fw]?.language ?? ''}</span>
            </button>
          </li>
        {/each}
      </ul>
    {/if}
  </div>
</div>

<style>
  .picker {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 6px;
    padding: 10px 16px;
    border-bottom: 1px solid var(--border);
    min-height: 48px;
  }

  .chip {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    padding: 3px 8px 3px 10px;
    border-radius: 999px;
    font-size: 13px;
    font-weight: 500;
    background: color-mix(in srgb, var(--color) 18%, transparent);
    color: var(--color);
    border: 1px solid color-mix(in srgb, var(--color) 40%, transparent);
  }

  .remove {
    background: none;
    border: none;
    color: inherit;
    cursor: pointer;
    padding: 0 1px;
    font-size: 15px;
    line-height: 1;
    opacity: 0.7;
  }
  .remove:hover { opacity: 1; }

  .add-wrap {
    position: relative;
  }

  input {
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 6px;
    color: var(--text);
    font-size: 13px;
    font-family: inherit;
    padding: 4px 10px;
    width: 160px;
    outline: none;
  }
  input:focus {
    border-color: var(--accent);
  }

  .dropdown {
    position: absolute;
    top: calc(100% + 4px);
    left: 0;
    z-index: 100;
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 6px;
    list-style: none;
    padding: 4px;
    min-width: 200px;
    box-shadow: 0 8px 24px rgba(0,0,0,0.4);
    max-height: 260px;
    overflow-y: auto;
  }

  .dropdown button {
    display: flex;
    justify-content: space-between;
    align-items: center;
    width: 100%;
    padding: 6px 8px;
    border: none;
    background: none;
    color: var(--text);
    cursor: pointer;
    border-radius: 4px;
    font-size: 13px;
    font-family: inherit;
    gap: 8px;
  }
  .dropdown button:hover { background: var(--surface-hover); }

  .lang {
    color: var(--text-muted);
    font-size: 11px;
  }
</style>
