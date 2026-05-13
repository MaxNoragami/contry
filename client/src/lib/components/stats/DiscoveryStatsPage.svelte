<script lang="ts">
  import { ArrowLeft } from 'lucide-svelte'
  import { fly } from 'svelte/transition'
  import DiscoveryStatsMap from './DiscoveryStatsMap.svelte'
  import DiscoveryRing from './DiscoveryRing.svelte'
  import { type CountryDiscoveryStatDto, type MyRankedStatsResult } from '../../api/client'
  import type { createAuthStore } from '../../stores/auth.svelte'
  import type { DiscoveryStatsPayload, DiscoveryContinentKey, DiscoveryCountrySummary, DiscoveryContinentSummary } from '../../stores/game.svelte'
  import { getDB } from '../../stores/db'
  import { syncDatasets } from '../../datasets/ingest'

  interface Props {
    auth: ReturnType<typeof createAuthStore>
    goBack: () => void
    direction: 'forward' | 'back'
  }

  let { auth, goBack, direction }: Props = $props()

  let stats = $state<DiscoveryStatsPayload | null>(null)
  let loading = $state(true)

  function percent(part: number, whole: number) {
    if (whole <= 0) return 0
    return (part / whole) * 100
  }

  $effect(() => {
    let cancelled = false
    loading = true

    loadDiscoveryStats().then((data) => {
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

  async function loadDiscoveryStats(): Promise<DiscoveryStatsPayload> {
    const [apiStats, localData] = await Promise.all([
      auth.request<MyRankedStatsResult>('/ranked-stats/me'),
      loadLocalCountryData(),
    ])

    const discoveryByCountry = new Map<string, CountryDiscoveryStatDto>()
    for (const stat of apiStats.countryDiscoveryStats) {
      discoveryByCountry.set(stat.countryId, stat)
    }

    const countries: DiscoveryCountrySummary[] = localData.map((row) => {
      const stat = discoveryByCountry.get(row.country_id)
      return {
        country_id: row.country_id,
        name: row.name,
        lat: row.lat,
        lon: row.lon,
        continent: row.continent,
        discovered: stat?.discovered ?? false,
        best_attempts: stat?.bestAttempts ?? null,
        solved_count: stat?.solvedCount ?? 0,
        last_solved_at: stat?.lastSolvedAtUtc ? new Date(stat.lastSolvedAtUtc).getTime() : null,
      }
    })

    const discoveredCount = countries.filter((c) => c.discovered).length
    const totalCount = countries.length

    const continentAccents: Record<DiscoveryContinentKey, string> = {
      'Africa': '#cc241d',
      'Europe': '#458588',
      'Asia': '#d79921',
      'North America': '#98971a',
      'South America': '#83a598',
      'Oceania': '#b16286',
    }
    const continentLabels: Record<DiscoveryContinentKey, string> = {
      'Africa': 'Africa',
      'Europe': 'Europe',
      'Asia': 'Asia',
      'North America': 'N. America',
      'South America': 'S. America',
      'Oceania': 'Oceania',
    }

    const continents: DiscoveryContinentSummary[] = (Object.keys(continentAccents) as DiscoveryContinentKey[]).map((continent) => {
      const continentCountries = countries.filter((c) => c.continent === continent)
      const solved = continentCountries.filter((c) => c.discovered).length
      return {
        id: continent,
        label: continentLabels[continent],
        discovered_count: solved,
        total_count: continentCountries.length,
        discovered_percent: percent(solved, continentCountries.length),
        accent: continentAccents[continent],
      }
    })

    return {
      countries,
      discovered_count: discoveredCount,
      total_count: totalCount,
      discovered_percent: percent(discoveredCount, totalCount),
      continents,
    }
  }

  async function loadLocalCountryData() {
    await syncDatasets(['continent'])
    const db = await getDB()
    const tx = db.transaction('dataset_rows', 'readonly')
    const index = tx.objectStore('dataset_rows').index('by-dataset')
    const [baseRows, continentRows] = await Promise.all([
      index.getAll('countries_base'),
      index.getAll('continent'),
    ])
    await tx.done

    const continentByCountry = new Map<string, DiscoveryContinentKey>()
    for (const row of continentRows) {
      if (typeof row.value === 'string') {
        continentByCountry.set(row.country_id, row.value as DiscoveryContinentKey)
      }
    }

    return baseRows.map((row) => ({
      country_id: row.country_id,
      name: row.name || row.country_id,
      lat: row.lat || 0,
      lon: row.lon || 0,
      continent: continentByCountry.get(row.country_id) || null,
    }))
  }
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
