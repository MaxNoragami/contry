<script lang="ts">
  import type { GuessRow } from "../stores/game.svelte";
  import Chip from "./Chip.svelte";

  interface Props {
    row: GuessRow;
    isPast?: boolean;
  }

  let { row, isPast = false }: Props = $props();
</script>

<article class="guess-row" class:past={isPast} role="listitem">
  <h2>{row.rank}. {row.country}</h2>
  <div class="chips">
    {#each row.results as result}
      <Chip {result} />
    {/each}
  </div>
</article>

<style>
  .guess-row {
    transition: opacity 0.3s ease;
    animation: rowSlide 0.3s ease-out;
  }

  @keyframes rowSlide {
    from {
      opacity: 0;
      transform: translateY(8px);
    }
  }

  .guess-row.past {
    opacity: 0.55;
  }

  h2 {
    margin: 0 0 6px;
    font-size: clamp(14px, 1.8vw, 24px);
    font-weight: 600;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .chips {
    display: flex;
    gap: 6px;
  }

  .chips > :global(*) {
    flex: 1 1 0;
    min-width: 44px;
  }
</style>
