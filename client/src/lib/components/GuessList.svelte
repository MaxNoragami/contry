<script lang="ts">
  import type { GuessRow as GuessRowType } from "../stores/game.svelte";
  import GuessRow from "./GuessRow.svelte";
  import { tick } from "svelte";
  import { Sparkle, Sparkles } from "lucide-svelte";
  import MapPinSearch from "./MapPinSearch.svelte";

  interface Props {
    rows: GuessRowType[];
    isTyping?: boolean;
    scrollContainer?: HTMLElement | undefined;
  }

  let {
    rows,
    isTyping = false,
    scrollContainer = $bindable(),
  }: Props = $props();

  let scrollEl: HTMLElement | undefined = $state();

  // Scroll to bottom helper
  function scrollToBottom(behavior: ScrollBehavior = "auto") {
    if (!scrollEl) return;
    tick().then(() => {
      scrollEl?.scrollTo({ top: scrollEl.scrollHeight, behavior });
    });
  }

  // Auto-scroll to the latest guess whenever rows change
  $effect(() => {
    const _len = rows.length;
    scrollToBottom("smooth");
  });

  // Re-scroll when MapModal appears/disappears (isTyping toggle resizes this container)
  $effect(() => {
    if (!scrollEl) return;
    const node = scrollEl;
    const ro = new ResizeObserver(() => {
      // Force scroll to bottom on every resize frame
      if (rows.length > 0) {
        // Use requestAnimationFrame to ensure layout has updated
        requestAnimationFrame(() => {
          node.scrollTop = node.scrollHeight;
        });
      }
    });
    ro.observe(node);
    return () => ro.disconnect();
  });
</script>

<div class="island guess-list-island">
  <div class="guess-list-inner" bind:this={scrollContainer}>
    <div
      class="guess-list-scroll"
      bind:this={scrollEl}
      role="list"
      aria-label="Guesses history"
    >
      {#if rows.length === 0}
        <div class="empty-state">
          <div class="icon-wrapper">
            <Sparkle class="sparkle s1" />
            <Sparkles class="sparkle s2" />
            <Sparkle class="sparkle s3" />
            <MapPinSearch size={160} strokeWidth={1.5} />
          </div>
          <p>Guess the cōntry . . .</p>
        </div>
      {:else}
        {#each rows as row, index}
          <GuessRow {row} isPast={index < rows.length - 1} />
        {/each}
      {/if}
    </div>
  </div>
</div>

<style>
  .guess-list-island {
    min-height: 0;
    flex: 1 1 0;
    overflow: hidden;
    display: flex;
    flex-direction: column;
  }

  .guess-list-inner {
    flex: 1 1 0;
    min-height: 0;
    overflow-x: auto;
    scrollbar-width: none;
    -ms-overflow-style: none;
  }

  .guess-list-inner::-webkit-scrollbar {
    display: none;
  }

  .guess-list-scroll {
    height: 100%;
    overflow-y: auto;
    overscroll-behavior: contain;
    padding: 12px 16px;
    display: flex;
    flex-direction: column;
    gap: 10px;
    scrollbar-width: none;
    -ms-overflow-style: none;
  }

  .guess-list-scroll::-webkit-scrollbar {
    display: none;
  }

  /* Empty state styles */
  .empty-state {
    flex: 1;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    color: var(--muted);
    gap: 16px;
    height: 100%;
    opacity: 0.7;
    animation: fadeIn 0.5s ease-out;
  }

  .icon-wrapper {
    position: relative;
    display: flex;
    align-items: center;
    justify-content: center;
    color: var(--muted);
    margin-bottom: 24px;
  }

  :global(.empty-state .sparkle) {
    position: absolute;
    color: var(--accent);
    opacity: 0;
  }

  :global(.empty-state .s1) {
    top: -20px;
    left: -40px;
    width: 48px;
    height: 48px;
    animation: popTL 12s ease-in-out infinite;
    animation-delay: 0s;
  }

  :global(.empty-state .s2) {
    top: -10px;
    right: -45px;
    width: 60px;
    height: 60px;
    animation: popTR 14s ease-in-out infinite;
    animation-delay: 3s;
  }

  :global(.empty-state .s3) {
    bottom: -15px;
    left: -35px;
    width: 45px;
    height: 45px;
    animation: popBL 15s ease-in-out infinite;
    animation-delay: 8s;
  }

  :global(.empty-state .s4) {
    bottom: -25px;
    right: -55px;
    width: 55px;
    height: 55px;
    animation: popBR 13s ease-in-out infinite;
    animation-delay: 5s;
  }

  .empty-state p {
    margin: 0;
    font-size: 22px;
    font-weight: 600;
    letter-spacing: 0.5px;
    text-align: center;
  }

  @keyframes popTL {
    0% {
      transform: translate(0, 0) scale(0) rotate(0deg);
      opacity: 0;
    }
    5% {
      transform: translate(-8px, -12px) scale(1.1) rotate(15deg);
      opacity: 1;
    }
    10% {
      transform: translate(-4px, -6px) scale(1) rotate(5deg);
      opacity: 0.8;
    }
    15%,
    100% {
      transform: translate(0, 0) scale(0) rotate(0deg);
      opacity: 0;
    }
  }

  @keyframes popTR {
    0% {
      transform: translate(0, 0) scale(0) rotate(0deg);
      opacity: 0;
    }
    5% {
      transform: translate(12px, -8px) scale(1.2) rotate(-15deg);
      opacity: 1;
    }
    10% {
      transform: translate(8px, -4px) scale(1.1) rotate(-5deg);
      opacity: 0.9;
    }
    15%,
    100% {
      transform: translate(0, 0) scale(0) rotate(0deg);
      opacity: 0;
    }
  }

  @keyframes popBL {
    0% {
      transform: translate(0, 0) scale(0) rotate(0deg);
      opacity: 0;
    }
    5% {
      transform: translate(-12px, 12px) scale(1.15) rotate(20deg);
      opacity: 1;
    }
    10% {
      transform: translate(-6px, 6px) scale(1.05) rotate(10deg);
      opacity: 0.8;
    }
    15%,
    100% {
      transform: translate(0, 0) scale(0) rotate(0deg);
      opacity: 0;
    }
  }

  @keyframes popBR {
    0% {
      transform: translate(0, 0) scale(0) rotate(0deg);
      opacity: 0;
    }
    5% {
      transform: translate(10px, 10px) scale(1.25) rotate(-20deg);
      opacity: 1;
    }
    10% {
      transform: translate(6px, 6px) scale(1.1) rotate(-10deg);
      opacity: 0.9;
    }
    15%,
    100% {
      transform: translate(0, 0) scale(0) rotate(0deg);
      opacity: 0;
    }
  }

  @keyframes fadeIn {
    from {
      opacity: 0;
      transform: translateY(10px);
    }
    to {
      opacity: 0.7;
      transform: translateY(0);
    }
  }
</style>
