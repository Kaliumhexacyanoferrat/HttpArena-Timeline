<script lang="ts">
  import { tick } from 'svelte'

  /** Text of the toggle button, e.g. "+ framework". */
  export let label: string
  export let title = ''
  export let placeholder = 'Filter…'
  export let disabled = false
  /** Accessible name of the panel. */
  export let dialogLabel = label
  export let query = ''
  export let open = false
  /** "add": a quiet "+ something" text button; "menu": a value with a caret, for view settings. */
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

  .trigger {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    height: 26px;
    padding: 0 8px;
    border: none;
    border-radius: 6px;
    background: none;
    color: var(--text-2);
    font-size: 13px;
    font-family: inherit;
    white-space: nowrap;
    cursor: pointer;
    transition: background .12s ease, color .12s ease;
  }
  .trigger:hover:not(:disabled), .trigger.active { background: var(--surface); color: var(--text); }
  .trigger:disabled { opacity: .5; cursor: default; }
  .trigger:focus-visible { outline: 2px solid var(--accent); outline-offset: 1px; }

  .menu { color: var(--text); }

  .caret {
    width: 6px;
    height: 6px;
    margin: -3px 1px 0 2px;
    border-right: 1.5px solid var(--text-2);
    border-bottom: 1.5px solid var(--text-2);
    transform: rotate(45deg);
  }

  .panel {
    position: absolute;
    top: calc(100% + 4px);
    left: 0;
    z-index: 100;
    display: flex;
    flex-direction: column;
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 8px;
    box-shadow: 0 12px 32px rgba(0, 0, 0, .45);
    outline: none;
  }
  .panel.right { left: auto; right: 0; }

  .search {
    margin: 8px 8px 4px;
    background: var(--bg);
    border: 1px solid var(--border);
    border-radius: 6px;
    color: var(--text);
    font-size: 13px;
    font-family: inherit;
    padding: 5px 10px;
    outline: none;
  }
  .search:focus { border-color: var(--accent); }

  .list {
    max-height: min(380px, 60vh);
    overflow-y: auto;
    padding: 0 4px 6px;
    scrollbar-width: thin;
    scrollbar-color: var(--border) transparent;
  }

  .list :global(.group-title) {
    position: sticky;
    top: 0;
    z-index: 1;
    display: flex;
    justify-content: space-between;
    background: var(--surface);
    padding: 8px 8px 4px;
    font-size: 12px;
    font-weight: 600;
    color: var(--muted);
  }

  .list :global(.row) {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 10px;
    padding: 3px 4px 3px 8px;
    border-radius: 6px;
  }
  .list :global(.row:hover) { background: var(--surface-hover); }

  .list :global(.empty), .panel :global(.footnote) {
    padding: 8px 12px;
    font-size: 12px;
    color: var(--muted);
  }
  .panel :global(.footnote) { border-top: 1px solid var(--border); }

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
