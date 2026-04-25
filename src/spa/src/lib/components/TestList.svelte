<script lang="ts">
  import { selectedTest } from '../stores/selection'

  export let tests: string[]
</script>

<nav class="test-list">
  {#each tests as test (test)}
    <button
      class:active={$selectedTest === test}
      on:click={() => selectedTest.set(test)}
    >
      {test}
    </button>
  {/each}
</nav>

<style>
  .test-list {
    display: flex;
    flex-direction: column;
    gap: 1px;
    padding: 8px;
  }

  button {
    text-align: left;
    padding: 6px 10px;
    border-radius: 4px;
    border: none;
    background: none;
    color: var(--text-muted);
    cursor: pointer;
    font-size: 13px;
    font-family: inherit;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    transition: background 0.1s, color 0.1s;
  }

  @media (max-width: 639px) {
    .test-list {
      flex-direction: row;
      overflow-x: auto;
      padding: 6px 8px;
      scrollbar-width: none;
      gap: 4px;
    }
    .test-list::-webkit-scrollbar { display: none; }
    button { flex-shrink: 0; overflow: visible; text-overflow: unset; }
  }

  button:hover {
    background: var(--surface-hover);
    color: var(--text);
  }

  button.active {
    background: var(--accent-subtle);
    color: var(--accent);
    font-weight: 500;
  }
</style>
