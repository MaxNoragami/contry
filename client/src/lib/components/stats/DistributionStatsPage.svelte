<script lang="ts">
  import { ArrowLeft } from 'lucide-svelte'
  import { fly } from 'svelte/transition'
  import GuessDistributionChart from './GuessDistributionChart.svelte'
  import type { DistributionStatsPayload } from '../../stores/game.svelte'

  interface Props {
    game: any
    goBack: () => void
    direction: 'forward' | 'back'
  }

  let { game, goBack, direction }: Props = $props()

  let stats = $state<DistributionStatsPayload | null>(null)
  let loading = $state(true)

  $effect(() => {
    let cancelled = false
    loading = true

    game.getDistributionStats().then((data: DistributionStatsPayload) => {
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

  function formatAverage(value: number | null) {
    if (value == null) return '—'
    return value.toFixed(1)
  }

  function formatInt(value: number | null) {
    return value == null ? '—' : String(value)
  }
</script>

<div class="view-container" in:fly={{ x: direction === 'back' ? -20 : 20, duration: 250, delay: 100 }} out:fly={{ x: direction === 'back' ? 20 : -20, duration: 200 }}>
  <div class="modal-header">
    <button class="icon-btn back-btn" aria-label="Back" onclick={goBack}><ArrowLeft /></button>
    <h2 class="centered-title">Distributions</h2>
    <div class="header-spacer"></div>
  </div>

  <div class="modal-body">
    {#if loading}
      <div class="message">Loading distribution stats...</div>
    {:else if stats}
      <div class="metrics-grid">
        <div class="metric-card">
          <div class="metric-value">{formatAverage(stats.average_guesses)}</div>
          <div class="metric-label">Average guesses</div>
        </div>
        <div class="metric-card">
          <div class="metric-value">{formatInt(stats.fastest_guess)}</div>
          <div class="metric-label">Fastest guess</div>
        </div>
        <div class="metric-card">
          <div class="metric-value">{formatInt(stats.slowest_guess)}</div>
          <div class="metric-label">Slowest guess</div>
        </div>
        <div class="metric-card">
          <div class="metric-value">{stats.give_up_rate.toFixed(stats.give_up_rate < 10 ? 1 : 0)}%</div>
          <div class="metric-label">Give up rate</div>
        </div>
      </div>

      <section class="section-block">
        <h3>Guess distribution</h3>
        <GuessDistributionChart buckets={stats.guess_distribution} />
      </section>

      <section class="section-block">
        <h3>Most given up countries</h3>
        {#if stats.top_give_up_countries.length > 0}
          <div class="country-list">
            {#each stats.top_give_up_countries as country, index (country.country_id)}
              <div class="country-row">
                <div class="country-rank">#{index + 1}</div>
                <div class="country-name">{country.name}</div>
                <div class="country-value">{country.give_up_count}</div>
              </div>
            {/each}
          </div>
        {:else}
          <p class="empty-text">No give-ups recorded yet.</p>
        {/if}
      </section>

    {:else}
      <div class="message">Could not load distribution stats.</div>
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
    gap: 24px;
    scrollbar-width: none;
    -ms-overflow-style: none;
  }

  .modal-body::-webkit-scrollbar { display: none; }

  .message,
  .empty-text {
    margin: 0;
    color: var(--muted);
    line-height: 1.5;
  }

  .message {
    margin: auto 0;
    text-align: center;
  }

  .metrics-grid {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 16px 14px;
  }

  .metric-card {
    display: flex;
    flex-direction: column;
    gap: 4px;
    align-items: center;
    text-align: center;
  }

  .metric-value {
    font-size: 36px;
    line-height: 1;
    font-weight: 700;
    color: var(--chip-bg);
  }

  .metric-label {
    font-size: 14px;
    color: var(--muted);
    line-height: 1.3;
  }

  .section-block {
    display: flex;
    flex-direction: column;
    gap: 14px;
  }

  .section-block h3 {
    margin: 0;
    font-size: 18px;
    font-weight: 600;
  }

  .country-list {
    display: flex;
    flex-direction: column;
    gap: 10px;
  }

  .country-row {
    display: grid;
    grid-template-columns: 40px 1fr auto;
    gap: 12px;
    align-items: center;
    padding: 10px 12px;
    border-radius: 10px;
    background: var(--panel-soft);
  }

  .country-rank,
  .country-value {
    font-weight: 700;
    color: var(--warn);
  }

  .country-name {
    color: var(--text);
  }

  @media (min-width: 460px) {
    .metrics-grid {
      grid-template-columns: repeat(4, minmax(0, 1fr));
    }
  }
</style>
