<script lang="ts">
  import type { ClueResult } from '../stores/game.svelte'

  interface Props {
    result: ClueResult
  }

  let { result }: Props = $props()
</script>

<div class="chip {result.tone}" class:pending={result.pending}>
  {#if result.pending}
    <span class="chip-skeleton" aria-hidden="true"></span>
  {:else if result.kind === 'numeric' && result.trend}
    <span class="trend {result.trend}" aria-hidden="true">
      {result.trend === 'higher' ? '▲' : '▼'}
    </span>
  {/if}
  {#if !result.pending}
    <span class="chip-value" class:is-checkmark={result.value === '✓'}>{result.value}</span>
  {/if}
</div>

<style>
  .chip {
    aspect-ratio: 1 / 1;
    min-width: 0;
    min-height: 44px;
    border-radius: 10px;
    border: 1px solid transparent;
    display: grid;
    place-items: center;
    text-align: center;
    padding: 6px 4px;
    position: relative;
    font-weight: 600;
    transition: transform 0.2s ease, opacity 0.3s ease;
    animation: chipIn 0.25s ease-out both;
  }

  @media (hover: hover) {
    .chip:hover {
      transform: scale(1.03);
    }
  }

  .chip:active {
    transform: scale(1.03);
  }

  @keyframes chipIn {
    from {
      opacity: 0;
      transform: scale(0.92);
    }
    to {
      opacity: 1;
      transform: scale(1);
    }
  }

  .chip-value {
    font-size: clamp(10px, 1.4vw, 17px);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    max-width: 100%;
    display: block;
    line-height: 1.2;
  }

  .chip-value.is-checkmark {
    font-size: clamp(30px, 4.2vw, 51px);
    line-height: 1;
  }

  .chip.green {
    background: color-mix(in oklab, var(--ok) 26%, var(--panel));
    border-color: color-mix(in oklab, var(--ok) 56%, var(--panel-3));
  }

  .chip.yellow {
    background: color-mix(in oklab, var(--warn) 24%, var(--panel));
    border-color: color-mix(in oklab, var(--warn) 58%, var(--panel-3));
  }

  .chip.red {
    background: color-mix(in oklab, var(--bad) 22%, var(--panel));
    border-color: color-mix(in oklab, var(--bad) 55%, var(--panel-3));
  }

  .chip.blue {
    background: color-mix(in oklab, var(--info) 24%, var(--panel));
    border-color: color-mix(in oklab, var(--info) 52%, var(--panel-3));
  }

  .trend {
    position: absolute;
    top: 12%;
    left: 50%;
    transform: translateX(-50%);
    font-size: clamp(12px, 1.5vw, 24px);
    color: var(--chip-bg);
    line-height: 1;
  }

  .trend.lower {
    top: auto;
    bottom: 12%;
  }

  .chip.pending {
    background: color-mix(in oklab, var(--panel-3) 60%, var(--panel));
    border-color: color-mix(in oklab, var(--border) 88%, var(--panel-3));
  }

  .chip-skeleton {
    width: 72%;
    height: 14px;
    border-radius: 999px;
    background: linear-gradient(90deg, color-mix(in oklab, var(--panel-3) 86%, white) 0%, color-mix(in oklab, var(--panel-2) 78%, white) 45%, color-mix(in oklab, var(--panel-3) 86%, white) 100%);
    background-size: 180% 100%;
    animation: shimmer 1.1s linear infinite;
  }

  @keyframes shimmer {
    from { background-position: 180% 0; }
    to { background-position: -180% 0; }
  }
</style>
