<script lang="ts">
  import { ArrowLeft } from 'lucide-svelte'
  import { fly } from 'svelte/transition'
  import DiscoveryStatsMap from './DiscoveryStatsMap.svelte'
  import DiscoveryRing from './DiscoveryRing.svelte'
  import type { DiscoveryStatsPayload } from '../../stores/game.svelte'

  interface Props {
    game: any
    goBack: () => void
    direction: 'forward' | 'back'
  }

  let { game, goBack, direction }: Props = $props()

  let stats = $state<DiscoveryStatsPayload | null>(null)
  let loading = $state(true)

  $effect(() => {
    let cancelled = false
    loading = true

    game.getDiscoveryStats().then((data: DiscoveryStatsPayload) => {
      if (!cancelled) {
        stats = data
        loading = false
      }
    }).catch(() => {
      if (!cancelled) {
        stats = null
        loading = false
      }
    })

    return () => {
      cancelled = true
    }
  })
</script>

<div class="view-container" in:fly={{ x: direction === 'back' ? -20 : 20, duration: 250, delay: 100 }} out:fly={{ x: direction === 'back' ? 20 : -20, duration: 200 }}>
  <div class="modal-header">
    <button class="icon-btn back-btn" aria-label="Back" onclick={goBack}><ArrowLeft /></button>
    <h2 class="centered-title">Cōntry discovery</h2>
    <div class="header-spacer"></div>
  </div>

  <div class="modal-body">
    {#if loading}
      <div class="message">Loading discovery stats...</div>
    {:else if stats}
      <DiscoveryStatsMap countries={stats.countries} />

      <div class="stats-summary">
        <div class="world-summary">
          <DiscoveryRing
            size="world"
            percent={stats.discovered_percent}
            accent="var(--info)"
          />
        </div>

        <div class="continent-grid">
          {#each stats.continents as continent (continent.id)}
            <DiscoveryRing
              label={continent.label}
              percent={continent.discovered_percent}
              accent={continent.accent}
            />
          {/each}
        </div>
      </div>
    {:else}
      <div class="message">Could not load discovery stats.</div>
    {/if}
  </div>
</div>

<style>
  .view-container {
    position: absolute;
    inset: 0;
    display: flex;
    flex-direction: column;
    width: 100%;
    height: 100%;
    overflow: hidden;
  }

  .modal-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 16px 20px;
    background: var(--panel);
    position: relative;
    z-index: 2;
  }

  .centered-title {
    position: absolute;
    left: 50%;
    transform: translateX(-50%);
    font-size: 18px;
    font-weight: 500;
    margin: 0;
  }

  .header-spacer {
    width: 40px;
    height: 40px;
  }

  .icon-btn {
    width: 40px;
    height: 40px;
    border-radius: 50%;
    border: none;
    background: transparent;
    color: var(--text);
    display: grid;
    place-items: center;
    cursor: pointer;
    transition: background 0.2s, box-shadow 0.2s, color 0.2s;
    outline: none;
  }

  @media (hover: hover) {
    .icon-btn:hover:not(:disabled) { background: var(--hover-strong); }
  }

  .icon-btn:active:not(:disabled) { background: var(--hover-strong); }
  .icon-btn:focus-visible:not(:disabled) { box-shadow: inset 0 0 0 1px var(--info); }

  .modal-body {
    flex: 1;
    min-height: 0;
    overflow-y: auto;
    padding: 16px 20px 24px;
    display: flex;
    flex-direction: column;
    gap: 20px;
    scrollbar-width: none;
    -ms-overflow-style: none;
  }

  .modal-body::-webkit-scrollbar { display: none; }

  .message {
    margin: auto 0;
    text-align: center;
    color: var(--muted);
  }

  .stats-summary {
    display: flex;
    flex-direction: column;
    gap: 20px;
    align-items: center;
    padding-bottom: 8px;
  }

  .world-summary {
    display: flex;
    justify-content: center;
    width: 100%;
  }

  .continent-grid {
    width: 100%;
    display: grid;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: 18px 16px;
    justify-items: center;
  }

  @media (min-width: 430px) {
    .stats-summary {
      display: grid;
      grid-template-columns: 170px 1fr;
      align-items: center;
      gap: 28px;
      width: 100%;
    }

    .world-summary {
      justify-content: flex-start;
    }

    .continent-grid {
      gap: 16px 20px;
    }
  }
</style>
