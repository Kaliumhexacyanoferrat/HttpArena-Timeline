<script lang="ts">
  import { tick } from 'svelte'

  /** Text of the toggle button, e.g. "Framework" (an "add" button draws its own plus). */
  export let label: string
  export let title = ''
  export let placeholder = 'Filter…'
  export let disabled = false
  /** Accessible name of the panel. */
  export let dialogLabel = label
  export let query = ''
  export let open = false
  /** "add": a tool button with a plus, adding to the chart; "menu": a value with a caret, for view settings. */
  export let variant: 'add' | 'menu' = 'add'
  /** Short lists (menus) need no search box. */
  export let searchable = true
  /** Which edge of the button the panel lines up with. */
  export let align: 'left' | 'right' = 'left'
  export let width = 400

  let wrapEl: HTMLDivElement
  let searchEl: HTMLInputElement

  async function toggleOpen() {
    open = !open
    if (open) {
      query = ''
      await tick()
      if (searchable) searchEl?.focus()
    }
  }

  function onWindowPointer(e: MouseEvent) {
    if (open && wrapEl && !wrapEl.contains(e.target as Node)) open = false
  }

  function onKeydown(e: KeyboardEvent) {
    if (e.key === 'Escape') open = false
  }
</script>

<svelte:window on:mousedown={onWindowPointer} />

<!--
  The shared dropdown of the toolbar pickers: a toggle button and a panel with a search box,
  a scrollable list (default slot) and an optional footer. Closes on Escape or an outside click.
  List content uses the classes group-title, row, empty; the footer slot uses footnote (styled here).
-->
<div class="wrap" bind:this={wrapEl}>
  <button
    class="trigger {variant}"
    class:active={open}
    on:click={toggleOpen}
    aria-expanded={open}
    aria-haspopup="dialog"
    {disabled}
    {title}
  >
    {#if variant === 'add'}
      <svg class="plus" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5v14M5 12h14" /></svg>
    {/if}
    <slot name="trigger">{label}</slot>
    {#if variant === 'menu'}<span class="caret" aria-hidden="true"></span>{/if}
  </button>

  {#if open}
    <div
      class="panel"
      class:right={align === 'right'}
      style="width: {width}px"
      role="dialog"
      aria-label={dialogLabel}
      on:keydown={onKeydown}
      tabindex="-1"
    >
      {#if searchable}
        <input
          bind:this={searchEl}
          bind:value={query}
          on:keydown
          class="search"
          {placeholder}
          aria-label={placeholder}
        />
      {/if}

      <slot name="header" />

      <div class="list">
        <slot />
      </div>

      <slot name="footer" />

    </div>
  {/if}
</div>

<style>
  .wrap { position: relative; }

  /* "add": the board's tool button (its Compare / Thermal camera keys) */
  .trigger {
    display: inline-flex;
    align-items: center;
    gap: .45rem;
    height: 32px;
    padding: 0 .75rem;
    border: 1px solid var(--bay-line);
    background: var(--panel-2);
    color: var(--text);
    font: 600 .8125rem/1 var(--sans);
    white-space: nowrap;
    cursor: pointer;
  }
  .trigger:hover:not(:disabled), .trigger.active { background: var(--panel); border-color: var(--muted); }
  .trigger:disabled { opacity: .5; cursor: default; }
  .plus { width: 14px; height: 14px; flex: none; fill: none; stroke: currentColor; stroke-width: 2.2; stroke-linecap: round; }
  .trigger:focus-visible { outline: 2px solid var(--accent); outline-offset: 1px; }

  /* "menu": a value in a recessed bay, like the board's sort select */
  .menu {
    background: var(--bay);
    font-weight: 600;
  }
  .menu:hover:not(:disabled), .menu.active { background: var(--bay); border-color: var(--accent); }

  .caret {
    width: 6px;
    height: 6px;
    margin: -3px 1px 0 2px;
    border-right: 1.5px solid var(--text-2);
    border-bottom: 1.5px solid var(--text-2);
    transform: rotate(45deg);
    transition: transform .2s ease;
  }
  .active .caret { transform: translateY(3px) rotate(-135deg); }

  .panel {
    position: absolute;
    top: calc(100% + 6px);
    left: 0;
    z-index: 100;
    display: flex;
    flex-direction: column;
    background: var(--panel);
    border: 1px solid var(--line);
    border-radius: 6px;
    box-shadow: var(--shadow);
    outline: none;
    animation: popIn .16s cubic-bezier(.2,.6,.2,1);
  }
  .panel.right { left: auto; right: 0; }
  @keyframes popIn { from { opacity: 0; transform: translateY(-5px) } to { opacity: 1; transform: none } }

  .search {
    margin: .45rem .45rem .25rem;
    height: 34px;
    padding: 0 .65rem;
    background: var(--bay);
    border: 1px solid var(--bay-line);
    color: var(--text);
    font: 500 .85rem var(--sans);
    outline: none;
  }
  .search::placeholder { color: var(--muted); opacity: 1; }
  .search:focus { border-color: var(--accent); box-shadow: 0 0 0 3px var(--accent-weak); }

  .list {
    max-height: min(380px, 60vh);
    overflow-y: auto;
    padding: 0 .35rem .4rem;
    scrollbar-width: thin;
    scrollbar-color: var(--line) transparent;
  }

  .list :global(.group-title) {
    position: sticky;
    top: 0;
    z-index: 1;
    display: flex;
    justify-content: space-between;
    background: var(--panel);
    padding: .55rem .5rem .3rem;
    font: 600 .68rem/1 var(--label);
    letter-spacing: .14em;
    text-transform: uppercase;
    color: var(--text-2);
  }

  .list :global(.row) {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 10px;
    padding: 3px 4px 3px 8px;
    border-radius: 3px;
  }
  .list :global(.row:hover) { background: var(--panel-2); }

  .list :global(.empty), .panel :global(.footnote) {
    padding: .5rem .75rem;
    font-size: .75rem;
    color: var(--muted);
  }
  .panel :global(.footnote) { border-top: 1px solid var(--line-soft); }

  @media (max-width: 639px) {
    /* Pin the panel to the viewport so it never runs off the right edge. */
    .panel {
      position: fixed;
      top: auto;
      left: 16px;
      right: 16px;
      /* Overrides the inline desktop width. */
      width: auto !important;
      margin-top: 4px;
    }
  }
</style>
