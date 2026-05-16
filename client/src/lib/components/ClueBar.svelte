<script lang="ts">
  import { getLucideIconUrl } from '../config/app'
  import type { ClueDef } from '../stores/game.svelte'

  interface Props {
    clues: ClueDef[]
    scrollContainer?: HTMLElement | undefined
  }

  let { clues, scrollContainer = $bindable() }: Props = $props()
</script>

<section class="island clue-bar-island" aria-label="Active clues">
  <div class="clue-bar-inner" bind:this={scrollContainer}>
    {#each clues as clue (clue.id)}
      <div class="clue-item">
        <span class="clue-icon">
          {#if clue.icon}
            {@const IconComponent = clue.icon}
            <IconComponent size={18} />
          {:else if clue.customIcon}
            <div class="custom-icon" style={`mask-image: url('${getLucideIconUrl(clue.customIcon)}'); -webkit-mask-image: url('${getLucideIconUrl(clue.customIcon)}');`}></div>
          {/if}
        </span>
        <span class="clue-label">{clue.label}</span>
      </div>
    {/each}
  </div>
</section>

<style>
  .clue-bar-island {
    padding: 6px 10px;
    height: var(--island-row-h, 54px);
    display: flex;
    align-items: center;
    overflow: hidden;
  }

  .clue-bar-inner {
    width: 100%;
    display: flex;
    gap: 6px;
    overflow-x: auto;
    scrollbar-width: none;
    -ms-overflow-style: none;
    align-items: center;
  }

  .clue-bar-inner::-webkit-scrollbar {
    display: none;
  }

  .clue-item {
    flex: 1 1 0;
    min-width: 46px;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 2px;
    color: var(--muted);
    text-align: center;
  }

  .clue-icon {
    width: 28px;
    height: 28px;
    border: 1px solid var(--border-strong);
    background: var(--panel-2);
    border-radius: 8px;
    display: grid;
    place-items: center;
    color: color-mix(in oklab, var(--text) 84%, var(--muted));
    flex-shrink: 0;
  }

  :global(.clue-icon svg) {
    width: 14px;
    height: 14px;
  }

  .clue-label {
    font-size: clamp(8px, 1.1vw, 11px);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    max-width: 100%;
    display: block;
    line-height: 1.2;
  }

  .custom-icon {
    width: 18px;
    height: 18px;
    background-color: currentColor;
    mask-size: contain;
    mask-repeat: no-repeat;
    mask-position: center;
    -webkit-mask-size: contain;
    -webkit-mask-repeat: no-repeat;
    -webkit-mask-position: center;
  }
</style>
