<script lang="ts">
  interface Props {
    suggestions: string[]
    selectedIndex: number
    onSelect: (country: string) => void
    listRef?: HTMLUListElement | undefined
  }

  let {
    suggestions,
    selectedIndex,
    onSelect,
    listRef = $bindable(),
  }: Props = $props()
</script>

<ul
  class="suggestions"
  bind:this={listRef}
  aria-label="Country suggestions"
>
  {#each suggestions as country, i}
    <li>
      <button
        type="button"
        class:selected={i === selectedIndex}
        onmousedown={(e) => e.preventDefault()}
        onclick={() => onSelect(country)}
      >
        {country}
      </button>
    </li>
  {/each}
</ul>

<style>
  .suggestions {
    list-style: none;
    margin: 0;
    padding: 0;
    position: absolute;
    bottom: 100%;
    left: 10px;
    right: 10px;
    max-height: 200px;
    overflow: auto;
    display: flex;
    flex-direction: column;
    gap: 4px;
    background: var(--panel);
    border: 1px solid var(--border);
    border-radius: 12px;
    padding: 6px;
    z-index: 50;
    box-shadow: var(--shadow-strong);
    margin-bottom: 6px;
  }

  .suggestions button {
    width: 100%;
    border-radius: 8px;
    border: 1px solid transparent;
    background: transparent;
    color: var(--text);
    padding: 8px 12px;
    text-align: left;
    cursor: pointer;
    font: inherit;
    font-size: clamp(13px, 1.4vw, 15px);
    transition:
      background 0.1s,
      border-color 0.1s;
  }

  @media (hover: hover) {
    .suggestions button:hover {
      border-color: color-mix(in oklab, var(--border) 76%, var(--text));
      background: var(--panel-3);
    }
  }

  .suggestions button:active {
    border-color: color-mix(in oklab, var(--border) 76%, var(--text));
    background: var(--panel-3);
  }

  .suggestions button.selected {
    border-color: color-mix(in oklab, var(--border) 76%, var(--text));
    background: var(--panel-3);
  }
</style>
