<script lang="ts">
  import { BadgeCheck } from 'lucide-svelte'

  interface Props {
    percent: number
    label?: string
    accent: string
    size?: 'world' | 'continent'
  }

  let { percent, label = '', accent, size = 'continent' }: Props = $props()

  const clamped = $derived(Math.max(0, Math.min(percent, 100)))
  const ringSize = $derived(size === 'world' ? 168 : 78)
  const ringThickness = $derived(size === 'world' ? 12 : 8)
  const displayPercent = $derived(`${clamped.toFixed(clamped < 10 ? 1 : 0)}%`)
</script>

<div class="ring-wrap" class:world={size === 'world'}>
  {#if label && size !== 'world'}
    <div class="ring-label">{label}</div>
  {/if}

  <div
    class="ring"
    style={`--ring-size:${ringSize}px; --ring-thickness:${ringThickness}px; --ring-accent:${accent}; --ring-progress:${clamped}%;`}
  >
    <div class="ring-inner">
      {#if size === 'world'}
        <div class="world-icon"><BadgeCheck size={140} /></div>
      {/if}
      <div class="ring-value">{displayPercent}</div>
    </div>
  </div>

  {#if size === 'world'}
    <div class="world-caption">Cōntries discovered</div>
  {/if}
</div>

<style>
  .ring-wrap {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 10px;
  }

  .ring-label,
  .world-caption {
    font-size: 13px;
    color: var(--muted);
    text-align: center;
  }

  .ring {
    width: var(--ring-size);
    height: var(--ring-size);
    border-radius: 50%;
    background: conic-gradient(var(--ring-accent) var(--ring-progress), var(--ring-track) 0);
    display: grid;
    place-items: center;
    position: relative;
    box-shadow: inset 0 0 0 1px var(--inset-soft);
  }

  .ring-inner {
    width: calc(var(--ring-size) - var(--ring-thickness) * 2);
    height: calc(var(--ring-size) - var(--ring-thickness) * 2);
    border-radius: 50%;
    background: var(--panel);
    display: grid;
    place-items: center;
    position: relative;
    overflow: hidden;
  }

  .world-icon {
    position: absolute;
    inset: 0;
    display: grid;
    place-items: center;
    color: color-mix(in oklab, var(--text) 12%, transparent);
  }

  .ring-value {
    position: relative;
    z-index: 1;
    font-size: 28px;
    font-weight: 700;
    line-height: 1;
    color: var(--chip-bg);
  }

  .ring-wrap:not(.world) .ring-value {
    font-size: 20px;
  }
</style>
