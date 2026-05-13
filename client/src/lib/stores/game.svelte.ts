import {
  Compass,
  Globe,
  Navigation,
  Thermometer,
  Users,
  LandPlot,
  type Icon as LucideIcon
} from 'lucide-svelte'
import {
  getDB,
  type CountryDiscoveryStat,
  type GlobalRoundStats,
  type CountryRoundDifficultyStat,
  type ClueUsageStat,
} from './db'
import { syncDatasets } from '../datasets/ingest'
import { evaluateCategorical, evaluateNumeric, evaluateHemisphere, evaluateCoordinates } from '../engine/clues'
import type { DatasetClueEntry, DatasetManifest } from '../datasets/manifest'

// ── Types ──────────────────────────────────────────────

export type ChipTone = 'green' | 'yellow' | 'red' | 'blue'
export type ClueKind = 'text' | 'numeric' | 'direction'

export type ClueResult = {
  clue: string
  value: string
  tone: ChipTone
  kind: ClueKind
  trend?: 'higher' | 'lower'
  pending?: boolean
}

export type GuessRow = {
  rank: number
  country: string
  results: ClueResult[]
  pending?: boolean
}

export type ClueDef = {
  id: string
  label: string
  description?: string
  icon: typeof LucideIcon | null
  type: 'numeric' | 'categorical' | 'computed'
  group?: string
  unitSymbol?: string
  customIcon?: string
  source?: 'builtin' | 'custom'
}

export type RoundClueSnapshotEntry = {
  id: string
  label: string
  description?: string
  iconName?: string
  type: 'numeric' | 'categorical' | 'computed'
  group?: string
  unitSymbol?: string
  source?: 'builtin' | 'custom'
}

export type RoundCustomDataSnapshot = Record<string, Record<string, string | number | null>>

export const iconMap: Record<string, typeof LucideIcon> = {
  'compass': Compass,
  'globe': Globe,
  'navigation': Navigation,
  'thermometer': Thermometer,
  'users': Users,
  'land-plot': LandPlot,
}

export const DEFAULT_CLUES = ['hemisphere', 'continent', 'temperature_avg_c', 'population', 'coordinates']

export interface CountryRecord {
  country_id: string
  name: string
  lat: number
  lon: number
  [clue_id: string]: any
}

export type DiscoveryContinentKey =
  | 'Africa'
  | 'Europe'
  | 'Asia'
  | 'North America'
  | 'South America'
  | 'Oceania'

export type DiscoveryCountrySummary = {
  country_id: string
  name: string
  lat: number
  lon: number
  continent: DiscoveryContinentKey | null
  discovered: boolean
  best_attempts: number | null
  solved_count: number
  last_solved_at: number | null
}

export type DiscoveryContinentSummary = {
  id: DiscoveryContinentKey
  label: string
  discovered_count: number
  total_count: number
  discovered_percent: number
  accent: string
}

export type DiscoveryStatsPayload = {
  countries: DiscoveryCountrySummary[]
  discovered_count: number
  total_count: number
  discovered_percent: number
  continents: DiscoveryContinentSummary[]
}

export type DistributionBucket = {
  label: string
  count: number
}

export type DistributionCountryEntry = {
  country_id: string
  name: string
  give_up_count: number
}

export type DistributionStatsPayload = {
  average_guesses: number | null
  fastest_guess: number | null
  slowest_guess: number | null
  give_up_rate: number
  guess_distribution: DistributionBucket[]
  top_give_up_countries: DistributionCountryEntry[]
}

export type ClueUsageStatsPayload = {
  clues: Array<{
    id: string
    label: string
    customIcon?: string
    icon: typeof LucideIcon | null
    source?: 'builtin' | 'custom'
    usage_count: number
  }>
}

// ── Reactive state ─────────────────────────────────────

export function createArcadeGameState() {
  let query = $state('')
  let previewCountry = $state<string | null>(null)
  let rows = $state<GuessRow[]>([])
  let errorCountry = $state<string | null>(null)
  let correctCountry = $state<string | null>(null)
  let hasWon = $state(false)
  let hasGivenUp = $state(false)

  let loading = $state(true)
  let countryPool = $state<CountryRecord[]>([])
  let targetCountry = $state<CountryRecord | null>(null)
  let gameId = $state<string | null>(null)

  let manifest = $state<DatasetManifest | null>(null)
  let userClues = $state<string[]>([])
  let activeClues = $state<ClueDef[]>([])
  let availableClues = $state<ClueDef[]>([])

  let syncChannel: BroadcastChannel | null = null
  if (typeof window !== 'undefined') {
    syncChannel = new BroadcastChannel('contry_sync')
    syncChannel.onmessage = async (e) => {
      if (e.data.type === 'SYNC') {
        const db = await getDB()
        const gameTx = db.transaction(['games', 'guesses'], 'readonly')
        const gamesStore = gameTx.objectStore('games')
        
        const allGames = await gamesStore.getAll()
        allGames.sort((a, b) => b.started_at - a.started_at)
        const latestGame = allGames[0]

        if (latestGame && (latestGame.status === 'playing' || latestGame.status === 'won' || latestGame.status === 'lost')) {
          gameId = latestGame.game_id
          targetCountry = countryPool.find(c => c.country_id === latestGame.target_country_id) || countryPool[0]
          applyRoundCustomDataSnapshot(latestGame.round_custom_data_snapshot)
          targetCountry = countryPool.find(c => c.country_id === latestGame.target_country_id) || targetCountry
          
          if (latestGame.status === 'won') {
            hasWon = true
            correctCountry = targetCountry.name
            hasGivenUp = false
          } else if (latestGame.status === 'lost') {
            hasGivenUp = true
            hasWon = false
            correctCountry = null
          } else {
            hasWon = false
            hasGivenUp = false
            correctCountry = null
          }
          
          const pastGuesses = await gameTx.objectStore('guesses').index('by-game').getAll(latestGame.game_id)
          pastGuesses.sort((a, b) => a.attempt_no - b.attempt_no)
          
          rows = pastGuesses.map(g => {
            const cName = countryPool.find(c => c.country_id === g.guess_country_id)?.name || 'Unknown'
            return {
              rank: g.attempt_no,
              country: cName,
              results: g.results
            }
          })
          
          // Re-sync clues
          if (latestGame.round_clue_snapshot?.length) {
            applyRoundClueSnapshot(latestGame.round_clue_snapshot)
          } else if (manifest) {
            updateActiveCluesFromList(latestGame.selected_clues, manifest)
          }
        }
        await gameTx.done
      } else if (e.data.type === 'SETTINGS_SYNC') {
        if (rows.length === 0 && manifest) {
           // We can safely apply new clues if no guesses have been made
           const db = await getDB()
           const metaStore = db.transaction('settings', 'readonly').objectStore('settings')
           const saved = await metaStore.get('selected_clues')
           let newClues = Array.isArray(saved) ? saved : DEFAULT_CLUES
           if (newClues.length !== 5) newClues = DEFAULT_CLUES
           userClues = newClues
            updateActiveCluesFromList(newClues, manifest)
            const roundCustomDataSnapshot = await buildRoundCustomDataSnapshot(newClues, db, manifest)
            if (gameId) {
              const tx = db.transaction('games', 'readwrite')
              const g = await tx.objectStore('games').get(gameId)
              if (g && g.status === 'playing' && rows.length === 0) {
                g.selected_clues = newClues
                g.round_clue_snapshot = buildRoundClueSnapshot(newClues, manifest)
                g.round_custom_data_snapshot = roundCustomDataSnapshot
                await tx.objectStore('games').put(g)
              }
              await tx.done
            }
         }
      } else if (e.data.type === 'CATALOG_SYNC') {
        await refreshCustomClueCatalog(false)
      }
    }
  }

  const isTyping = $derived(query.trim().length > 0)

  // Filter out already guessed countries
  const guessedNames = $derived(new Set(rows.map(r => r.country.toLowerCase())))

  const suggestions = $derived.by(() => {
    const cleaned = query.trim().toLowerCase()
    if (cleaned.length < 1) return []
    return countryPool
      .map(c => c.name)
      .filter((name) => {
        const lowerName = name.toLowerCase()
        if (guessedNames.has(lowerName)) return false
        
        // Match only if any word in the country name starts with the query
        const words = lowerName.split(/[\s-]+/)
        return words.some(w => w.startsWith(cleaned))
      })
      .sort((a, b) => {
        const aStarts = a.toLowerCase().startsWith(cleaned)
        const bStarts = b.toLowerCase().startsWith(cleaned)
        if (aStarts && !bStarts) return -1
        if (!aStarts && bStarts) return 1
        return a.localeCompare(b)
      })
      .slice(0, 4)
  })

  const bestMatch = $derived.by(() => {
    if (hasGivenUp && targetCountry) {
      return { name: targetCountry.name, lat: targetCountry.lat, lon: targetCountry.lon }
    }

    if (previewCountry) {
      const q = previewCountry.toLowerCase()
      if (guessedNames.has(q)) return null
      const match = countryPool.find((c) => c.name.toLowerCase() === q)
      if (!match) return null
      return { name: match.name, lat: match.lat, lon: match.lon }
    }

    if (isTyping && suggestions.length > 0) {
      const topSuggestion = suggestions[0]
      const match = countryPool.find((c) => c.name === topSuggestion)
      if (!match) return null
      return { name: match.name, lat: match.lat, lon: match.lon }
    }

    return null
  })

  function resolveClueDef(entry: DatasetClueEntry): ClueDef {
    let label = entry.label || entry.id
    if (entry.group === 'temperature_avg_c') label = 'Average Temperature'
    return {
      id: entry.group || entry.id,
      label,
      description: entry.description,
      icon: entry.icon && iconMap[entry.icon] ? iconMap[entry.icon] : null,
      customIcon: entry.icon && !iconMap[entry.icon] ? entry.icon : undefined,
      type: entry.type,
      group: entry.group,
      unitSymbol: entry.unit_symbol,
      source: entry.source
    }
  }

  function resolveClueDefFromSnapshot(entry: RoundClueSnapshotEntry): ClueDef {
    return {
      id: entry.id,
      label: entry.label,
      description: entry.description,
      icon: entry.iconName && iconMap[entry.iconName] ? iconMap[entry.iconName] : null,
      customIcon: entry.iconName && !iconMap[entry.iconName] ? entry.iconName : undefined,
      type: entry.type,
      group: entry.group,
      unitSymbol: entry.unitSymbol,
      source: entry.source,
    }
  }

  function buildRoundClueSnapshot(clueIds: string[], m: DatasetManifest): RoundClueSnapshotEntry[] {
    const snapshot: RoundClueSnapshotEntry[] = []
    for (const id of clueIds) {
      let entry = m.clues.find(c => c.id === id)
      if (!entry) entry = m.clues.find(c => c.group === id)
      if (!entry) continue
      snapshot.push({
        id: entry.group || entry.id,
        label: entry.group === 'temperature_avg_c' ? 'Average Temperature' : (entry.label || entry.id),
        description: entry.description,
        iconName: entry.icon,
        type: entry.type,
        group: entry.group,
        unitSymbol: entry.unit_symbol,
        source: entry.source,
      })
    }
    return snapshot
  }

  function applyRoundClueSnapshot(snapshot: RoundClueSnapshotEntry[]) {
    activeClues = snapshot.map(resolveClueDefFromSnapshot)
  }

  async function buildRoundCustomDataSnapshot(
    clueIds: string[],
    db: Awaited<ReturnType<typeof getDB>>,
    m: DatasetManifest
  ): Promise<RoundCustomDataSnapshot> {
    const snapshot: RoundCustomDataSnapshot = {}
    const datasetIds = new Set<string>()

    for (const id of clueIds) {
      let entry = m.clues.find(c => c.id === id)
      if (!entry) entry = m.clues.find(c => c.group === id)
      if (!entry || entry.source !== 'custom' || entry.computed) continue
      datasetIds.add(entry.dataset_id)
    }

    if (datasetIds.size === 0) return snapshot

    const tx = db.transaction('dataset_rows', 'readonly')
    const store = tx.objectStore('dataset_rows')
    const index = store.index('by-dataset')

    for (const datasetId of datasetIds) {
      const rows = await index.getAll(datasetId)
      const byCountry: Record<string, string | number | null> = {}
      for (const row of rows) {
        byCountry[row.country_id] = row.value ?? null
      }
      snapshot[datasetId] = byCountry
    }

    await tx.done
    return snapshot
  }

  function applyRoundCustomDataSnapshot(snapshot?: RoundCustomDataSnapshot) {
    if (!snapshot) return
    const nextPool = countryPool.map(country => {
      const patched = { ...country }
      for (const [datasetId, valuesByCountry] of Object.entries(snapshot)) {
        if (Object.prototype.hasOwnProperty.call(valuesByCountry, country.country_id)) {
          patched[datasetId] = valuesByCountry[country.country_id]
        }
      }
      return patched
    })
    countryPool = nextPool
  }

  function percent(part: number, whole: number) {
    if (whole <= 0) return 0
    return (part / whole) * 100
  }

  function attemptsToBucket(attempts: number) {
    if (attempts >= 10) return '10+'
    return String(Math.max(1, Math.floor(attempts)))
  }

  function emptyDistribution(): Record<string, number> {
    return {
      '1': 0,
      '2': 0,
      '3': 0,
      '4': 0,
      '5': 0,
      '6': 0,
      '7': 0,
      '8': 0,
      '9': 0,
      '10+': 0,
    }
  }

  async function getDiscoveryStats(): Promise<DiscoveryStatsPayload> {
    const db = await getDB()
    await syncDatasets([...new Set([...userClues, 'continent'])])

    const tx = db.transaction(['dataset_rows', 'country_discovery_stats'], 'readonly')
    const rowsStore = tx.objectStore('dataset_rows')
    const rowsIndex = rowsStore.index('by-dataset')
    const [baseRows, continentRows, statRows] = await Promise.all([
      rowsIndex.getAll('countries_base'),
      rowsIndex.getAll('continent'),
      tx.objectStore('country_discovery_stats').getAll(),
    ])

    const continentByCountry = new Map<string, DiscoveryContinentKey>()
    for (const row of continentRows) {
      if (typeof row.value === 'string') {
        continentByCountry.set(row.country_id, row.value as DiscoveryContinentKey)
      }
    }

    const statByCountry = new Map<string, CountryDiscoveryStat>()
    for (const stat of statRows) {
      statByCountry.set(stat.country_id, stat)
    }

    const countries: DiscoveryCountrySummary[] = baseRows.map((row) => {
      const stat = statByCountry.get(row.country_id)
      return {
        country_id: row.country_id,
        name: row.name || row.country_id,
        lat: row.lat || 0,
        lon: row.lon || 0,
        continent: continentByCountry.get(row.country_id) || null,
        discovered: stat?.discovered ?? false,
        best_attempts: stat?.best_attempts ?? null,
        solved_count: stat?.solved_count ?? 0,
        last_solved_at: stat?.last_solved_at ?? null,
      }
    })

    const discoveredCount = countries.filter((country) => country.discovered).length
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

    const continents = (Object.keys(continentAccents) as DiscoveryContinentKey[]).map((continent) => {
      const continentCountries = countries.filter((country) => country.continent === continent)
      const solved = continentCountries.filter((country) => country.discovered).length
      return {
        id: continent,
        label: continentLabels[continent],
        discovered_count: solved,
        total_count: continentCountries.length,
        discovered_percent: percent(solved, continentCountries.length),
        accent: continentAccents[continent],
      }
    })

    await tx.done

    return {
      countries,
      discovered_count: discoveredCount,
      total_count: totalCount,
      discovered_percent: percent(discoveredCount, totalCount),
      continents,
    }
  }

  async function getDistributionStats(): Promise<DistributionStatsPayload> {
    const db = await getDB()
    const tx = db.transaction(['global_round_stats', 'country_round_difficulty_stats', 'dataset_rows'], 'readonly')
    const summary = await tx.objectStore('global_round_stats').get('summary') as GlobalRoundStats | undefined
    const difficultyRows = await tx.objectStore('country_round_difficulty_stats').getAll() as CountryRoundDifficultyStat[]
    const baseRows = await tx.objectStore('dataset_rows').index('by-dataset').getAll('countries_base')

    const namesByCountry = new Map(baseRows.map((row) => [row.country_id, row.name || row.country_id]))
    const guessDistributionSource = summary?.guess_distribution ?? emptyDistribution()

    const guess_distribution: DistributionBucket[] = ['1','2','3','4','5','6','7','8','9','10+'].map((label) => ({
      label,
      count: guessDistributionSource[label] ?? 0,
    }))

    const top_give_up_countries = difficultyRows
      .filter((row) => row.give_up_count > 0)
      .sort((a, b) => {
        if (b.give_up_count !== a.give_up_count) return b.give_up_count - a.give_up_count
        return (namesByCountry.get(a.country_id) || a.country_id).localeCompare(namesByCountry.get(b.country_id) || b.country_id)
      })
      .slice(0, 5)
      .map((row) => ({
        country_id: row.country_id,
        name: namesByCountry.get(row.country_id) || row.country_id,
        give_up_count: row.give_up_count,
      }))

    await tx.done

    return {
      average_guesses:
        summary && summary.win_count > 0
          ? summary.total_guesses_on_wins / summary.win_count
          : null,
      fastest_guess: summary?.fastest_win ?? null,
      slowest_guess: summary?.slowest_win ?? null,
      give_up_rate:
        summary && summary.finished_count > 0
          ? percent(summary.give_up_count, summary.finished_count)
          : 0,
      guess_distribution,
      top_give_up_countries,
    }
  }

  async function getClueUsageStats(): Promise<ClueUsageStatsPayload> {
    const db = await getDB()
    const tx = db.transaction('clue_usage_stats', 'readonly')
    const usageRows = await tx.objectStore('clue_usage_stats').getAll() as ClueUsageStat[]
    await tx.done

    const usageById = new Map(usageRows.map((row) => [row.clue_id, row.usage_count]))

    const clues = availableClues
      .map((clue) => ({
        id: clue.id,
        label: clue.label,
        icon: clue.icon,
        customIcon: clue.customIcon,
        source: clue.source,
        usage_count: usageById.get(clue.id) ?? 0,
      }))
      .sort((a, b) => {
        if (b.usage_count !== a.usage_count) return b.usage_count - a.usage_count
        return a.label.localeCompare(b.label)
      })

    return { clues }
  }

  async function updateCountryDiscoveryStats(
    tx: IDBTransaction | any,
    countryId: string,
    attempts: number
  ) {
    const statsStore = tx.objectStore('country_discovery_stats')
    const existing = await statsStore.get(countryId) as CountryDiscoveryStat | undefined
    const continentRow = await tx.objectStore('dataset_rows').get(['continent', countryId])
    const continent = typeof continentRow?.value === 'string' ? continentRow.value : null

    await statsStore.put({
      country_id: countryId,
      continent,
      discovered: true,
      best_attempts:
        existing?.best_attempts == null ? attempts : Math.min(existing.best_attempts, attempts),
      solved_count: (existing?.solved_count ?? 0) + 1,
      last_solved_at: Date.now(),
    } satisfies CountryDiscoveryStat)
  }

  async function updateDistributionStatsOnWin(
    tx: IDBTransaction | any,
    countryId: string,
    attempts: number,
  ) {
    const summaryStore = tx.objectStore('global_round_stats')
    const difficultyStore = tx.objectStore('country_round_difficulty_stats')

    const summary = (await summaryStore.get('summary') as GlobalRoundStats | undefined) ?? {
      key: 'summary' as const,
      finished_count: 0,
      win_count: 0,
      give_up_count: 0,
      total_guesses_on_wins: 0,
      fastest_win: null,
      slowest_win: null,
      guess_distribution: emptyDistribution(),
    }

    const bucket = attemptsToBucket(attempts)
    summary.finished_count += 1
    summary.win_count += 1
    summary.total_guesses_on_wins += attempts
    summary.fastest_win = summary.fastest_win == null ? attempts : Math.min(summary.fastest_win, attempts)
    summary.slowest_win = summary.slowest_win == null ? attempts : Math.max(summary.slowest_win, attempts)
    summary.guess_distribution = { ...summary.guess_distribution, [bucket]: (summary.guess_distribution[bucket] ?? 0) + 1 }
    await summaryStore.put(summary)

    const difficulty = (await difficultyStore.get(countryId) as CountryRoundDifficultyStat | undefined) ?? {
      country_id: countryId,
      give_up_count: 0,
      solved_count: 0,
      total_guesses_when_solved: 0,
    }
    difficulty.solved_count += 1
    difficulty.total_guesses_when_solved += attempts
    await difficultyStore.put(difficulty)
  }

  async function updateDistributionStatsOnGiveUp(
    tx: IDBTransaction | any,
    countryId: string,
    attempts: number,
  ) {
    const summaryStore = tx.objectStore('global_round_stats')
    const difficultyStore = tx.objectStore('country_round_difficulty_stats')

    const summary = (await summaryStore.get('summary') as GlobalRoundStats | undefined) ?? {
      key: 'summary' as const,
      finished_count: 0,
      win_count: 0,
      give_up_count: 0,
      total_guesses_on_wins: 0,
      fastest_win: null,
      slowest_win: null,
      guess_distribution: emptyDistribution(),
    }

    summary.finished_count += 1
    summary.give_up_count += 1
    await summaryStore.put(summary)

    const difficulty = (await difficultyStore.get(countryId) as CountryRoundDifficultyStat | undefined) ?? {
      country_id: countryId,
      give_up_count: 0,
      solved_count: 0,
      total_guesses_when_solved: 0,
    }
    difficulty.give_up_count += 1
    await difficultyStore.put(difficulty)
  }

  async function incrementClueUsageStats(clueIds: string[]) {
    const db = await getDB()
    const tx = db.transaction('clue_usage_stats', 'readwrite')
    const store = tx.objectStore('clue_usage_stats')
    for (const clueId of clueIds) {
      const existing = await store.get(clueId) as ClueUsageStat | undefined
      await store.put({
        clue_id: clueId,
        usage_count: (existing?.usage_count ?? 0) + 1,
      } satisfies ClueUsageStat)
    }
    await tx.done
  }

  async function pruneHistoricalGameData(keepGameId: string | null) {
    if (!keepGameId) return
    const db = await getDB()
    const tx = db.transaction(['games', 'guesses'], 'readwrite')
    const gamesStore = tx.objectStore('games')
    const guessesStore = tx.objectStore('guesses')
    const allGames = await gamesStore.getAll()
    for (const game of allGames) {
      if (game.game_id !== keepGameId) {
        await gamesStore.delete(game.game_id)
      }
    }
    const allGuesses = await guessesStore.getAll()
    for (const guess of allGuesses) {
      if (guess.game_id !== keepGameId) {
        await guessesStore.delete([guess.game_id, guess.attempt_no])
      }
    }
    await tx.done
  }

  function rebuildAvailableCluesFromManifest(m: DatasetManifest) {
    const avail: ClueDef[] = []
    const groupsSeen = new Set<string>()
    for (const entry of m.clues) {
      if (entry.group) {
        if (!groupsSeen.has(entry.group)) {
          groupsSeen.add(entry.group)
          avail.push(resolveClueDef(entry))
        }
      } else {
        avail.push(resolveClueDef(entry))
      }
    }
    availableClues = avail
  }

  async function refreshCustomClueCatalog(broadcast = true) {
    const db = await getDB()
    const settingsStore = db.transaction('settings', 'readonly').objectStore('settings')
    const customClues = await settingsStore.get('custom_clues')
    const savedClues = await settingsStore.get('selected_clues')
    let nextUserClues = Array.isArray(savedClues) ? savedClues : DEFAULT_CLUES
    if (nextUserClues.length !== 5) nextUserClues = DEFAULT_CLUES
    userClues = nextUserClues

    const syncedManifest = await syncDatasets(nextUserClues)

    manifest = {
      ...syncedManifest,
      clues: [
        ...syncedManifest.clues,
        ...(Array.isArray(customClues) ? customClues : []),
      ],
    }

    rebuildAvailableCluesFromManifest(manifest)

    if (rows.length === 0) {
      await reloadCountryPool(db)
      if (targetCountry) {
        targetCountry = countryPool.find(c => c.country_id === targetCountry!.country_id) || targetCountry
      }
      updateActiveCluesFromList(nextUserClues, manifest)
      const roundCustomDataSnapshot = await buildRoundCustomDataSnapshot(nextUserClues, db, manifest)
      if (gameId) {
        const tx = db.transaction('games', 'readwrite')
        const g = await tx.objectStore('games').get(gameId)
        if (g && g.status === 'playing') {
          g.round_clue_snapshot = buildRoundClueSnapshot(nextUserClues, manifest)
          g.round_custom_data_snapshot = roundCustomDataSnapshot
          await tx.objectStore('games').put(g)
        }
        await tx.done
      }
    }

    if (broadcast) {
      syncChannel?.postMessage({ type: 'CATALOG_SYNC' })
    }
  }

  function updateActiveCluesFromList(clueIds: string[], m: DatasetManifest) {
    const active: ClueDef[] = []
    for (const id of clueIds) {
      // Find exact or group
      let entry = m.clues.find(c => c.id === id)
      if (!entry) {
        entry = m.clues.find(c => c.group === id)
      }
      if (entry) {
        active.push(resolveClueDef(entry))
      }
    }
    activeClues = active
  }

  async function initGame() {
    loading = true
    try {
      const db = await getDB()
      
      const metaStore = db.transaction('settings', 'readonly').objectStore('settings')
      const savedClues = await metaStore.get('selected_clues')
      let fetchedUserClues = Array.isArray(savedClues) ? savedClues : DEFAULT_CLUES
      if (fetchedUserClues.length !== 5) fetchedUserClues = DEFAULT_CLUES
      userClues = fetchedUserClues

      // Fetch custom clues BEFORE syncDatasets to avoid transaction closing
      const customClues = await metaStore.get('custom_clues')

      // 1. Sync datasets
      manifest = await syncDatasets(userClues)

       // 1.5 Load custom clues
       if (Array.isArray(customClues) && customClues.length > 0) {
         manifest = { ...manifest, clues: [...manifest.clues, ...customClues] }
       }

       rebuildAvailableCluesFromManifest(manifest)

       await reloadCountryPool(db)

      // 3. Load previous state or Pick random target
      const gameTx = db.transaction(['games', 'guesses'], 'readwrite')
      const gamesStore = gameTx.objectStore('games')
      
      const allGames = await gamesStore.getAll()
      allGames.sort((a, b) => b.started_at - a.started_at)
      const latestGame = allGames[0]

      let shouldCreateNew = true

      if (latestGame) {
        if (latestGame.status === 'playing' || latestGame.status === 'won' || latestGame.status === 'lost') {
          // Auto-repair corrupt games
          if (latestGame.status === 'playing' && latestGame.selected_clues.length !== 5) {
             await gamesStore.delete(latestGame.game_id)
             shouldCreateNew = true
           } else {
             shouldCreateNew = false
           gameId = latestGame.game_id
           targetCountry = countryPool.find(c => c.country_id === latestGame.target_country_id) || countryPool[0]
           applyRoundCustomDataSnapshot(latestGame.round_custom_data_snapshot)
           targetCountry = countryPool.find(c => c.country_id === latestGame.target_country_id) || targetCountry
           if (latestGame.round_clue_snapshot?.length) {
             applyRoundClueSnapshot(latestGame.round_clue_snapshot)
           } else {
             updateActiveCluesFromList(latestGame.selected_clues, manifest)
           }
          
          if (latestGame.status === 'won') {
            hasWon = true
            hasGivenUp = false
            correctCountry = targetCountry.name
          } else if (latestGame.status === 'lost') {
            hasGivenUp = true
            hasWon = false
            correctCountry = null
          }
          
          const guessesStore = gameTx.objectStore('guesses')
          const gameIndex = guessesStore.index('by-game')
          const pastGuesses = await gameIndex.getAll(latestGame.game_id)
          pastGuesses.sort((a, b) => a.attempt_no - b.attempt_no)
          
          rows = pastGuesses.map(g => {
            const cName = countryPool.find(c => c.country_id === g.guess_country_id)?.name || 'Unknown'
            return {
              rank: g.attempt_no,
              country: cName,
              results: g.results
            }
          })

          // Reconstruct blue reveal row for lost games
          if (latestGame.status === 'lost' && targetCountry) {
            const revealResults: ClueResult[] = activeClues.map(cDef => {
              if (cDef.type === 'computed' && cDef.id === 'hemisphere') {
                const r = evaluateHemisphere(targetCountry!.lat, targetCountry!.lat)
                return { ...r, tone: 'blue' as ChipTone }
              }
              if (cDef.type === 'computed' && cDef.id === 'coordinates') {
                return { clue: 'Coordinates', value: '\u2713', tone: 'blue' as ChipTone, kind: 'direction' as ClueKind }
              }
              if (cDef.type === 'categorical') {
                const r = evaluateCategorical(targetCountry![cDef.id], targetCountry![cDef.id], cDef.label)
                return { ...r, tone: 'blue' as ChipTone }
              }
              let datasetKey = cDef.id
              if (cDef.group === 'temperature_avg_c') {
                const found = Object.keys(targetCountry!).find(k => k.startsWith('temperature_avg_c_m'))
                if (found) datasetKey = found
              }
              const r = evaluateNumeric(targetCountry![datasetKey], targetCountry![datasetKey], cDef.label, cDef.unitSymbol)
              return { ...r, tone: 'blue' as ChipTone, trend: undefined }
            })
            rows = [...rows, {
              rank: rows.length + 1,
              country: targetCountry.name,
              results: revealResults
            }]
          }
          }
        }
      }

      if (shouldCreateNew) {
        if (countryPool.length > 0) {
          const randomIndex = Math.floor(Math.random() * countryPool.length)
          targetCountry = countryPool[randomIndex]
        }
        updateActiveCluesFromList(userClues, manifest)
        const roundClueSnapshot = buildRoundClueSnapshot(userClues, manifest)
        const roundCustomDataSnapshot = await buildRoundCustomDataSnapshot(userClues, db, manifest)

        // 4. Record new game
        gameId = crypto.randomUUID()
        await gamesStore.put({
          game_id: gameId,
          target_country_id: targetCountry?.country_id ?? '',
          selected_clues: [...userClues],
          round_clue_snapshot: roundClueSnapshot,
          round_custom_data_snapshot: roundCustomDataSnapshot,
          started_at: Date.now(),
          status: 'playing'
        })
        await incrementClueUsageStats(userClues)
      }
      await gameTx.done
      await pruneHistoricalGameData(gameId)
      
    } catch (e) {
      console.error('Failed to init game', e)
    } finally {
      loading = false
    }
  }

  async function resetGame() {
    rows = []
    query = ''
    previewCountry = null
    errorCountry = null
    correctCountry = null
    hasWon = false
    hasGivenUp = false
    
    const db = await getDB()
    const metaStore = db.transaction('settings', 'readonly').objectStore('settings')
    const savedClues = await metaStore.get('selected_clues')
    let fetchedUserClues = Array.isArray(savedClues) ? savedClues : DEFAULT_CLUES
    if (fetchedUserClues.length !== 5) fetchedUserClues = DEFAULT_CLUES
    userClues = fetchedUserClues

    if (countryPool.length > 0) {
      const randomIndex = Math.floor(Math.random() * countryPool.length)
      targetCountry = countryPool[randomIndex]
    }
    gameId = crypto.randomUUID()
    
    if (manifest) {
      await syncDatasets(userClues)
      await reloadCountryPool(db)
      if (targetCountry) {
        targetCountry = countryPool.find(c => c.country_id === targetCountry!.country_id) || targetCountry
      }
      updateActiveCluesFromList(userClues, manifest)
    }

    const roundClueSnapshot = manifest ? buildRoundClueSnapshot(userClues, manifest) : []
    const roundCustomDataSnapshot = manifest ? await buildRoundCustomDataSnapshot(userClues, db, manifest) : {}

    const gameTx = db.transaction('games', 'readwrite')
    await gameTx.objectStore('games').put({
      game_id: gameId,
      target_country_id: targetCountry?.country_id ?? '',
      selected_clues: [...userClues],
      round_clue_snapshot: roundClueSnapshot,
      round_custom_data_snapshot: roundCustomDataSnapshot,
      started_at: Date.now(),
      status: 'playing'
    })
    await gameTx.done
    await incrementClueUsageStats(userClues)
    await pruneHistoricalGameData(gameId)
    syncChannel?.postMessage({ type: 'SYNC' })
  }

  function isValid(country: string): boolean {
    const lower = country.toLowerCase()
    return !!countryPool.find((c) => c.name.toLowerCase() === lower) && !guessedNames.has(lower)
  }

  function submitGuess(countryName: string): { valid: boolean; correct?: boolean } {
    if (!targetCountry || !gameId) return { valid: false }

    const match = countryPool.find((c) => c.name.toLowerCase() === countryName.toLowerCase())
    if (!match || guessedNames.has(match.name.toLowerCase())) return { valid: false }

    const isCorrect = match.country_id === targetCountry.country_id

    const results: ClueResult[] = activeClues.map(cDef => {
      if (cDef.type === 'computed' && cDef.id === 'hemisphere') {
        return evaluateHemisphere(match.lat, targetCountry!.lat)
      }
      if (cDef.type === 'computed' && cDef.id === 'coordinates') {
        return evaluateCoordinates(match.lat, match.lon, targetCountry!.lat, targetCountry!.lon)
      }
      if (cDef.type === 'categorical') {
        return evaluateCategorical(match[cDef.id], targetCountry![cDef.id], cDef.label)
      }
      
      let datasetKey = cDef.id
      if (cDef.group === 'temperature_avg_c') {
         const found = Object.keys(match).find(k => k.startsWith('temperature_avg_c_m'))
         if (found) datasetKey = found
      }
      return evaluateNumeric(match[datasetKey], targetCountry![datasetKey], cDef.label, cDef.unitSymbol)
    })

    const row: GuessRow = {
      rank: rows.length + 1,
      country: match.name,
      results
    }
    rows = [...rows, row]
    query = ''
    previewCountry = null

    if (isCorrect) {
      correctCountry = match.name
      hasWon = true
    } else {
      errorCountry = match.name
      setTimeout(() => {
        if (errorCountry === match.name) errorCountry = null
      }, 500)
    }

    // Save guess to IDB and broadcast
    getDB().then(async db => {
      const tx = db.transaction(['guesses', 'games', 'country_discovery_stats', 'global_round_stats', 'country_round_difficulty_stats', 'dataset_rows'], 'readwrite')
      tx.objectStore('guesses').put({
        game_id: gameId!,
        attempt_no: row.rank,
        guess_country_id: match.country_id,
        results
      })

      if (isCorrect) {
        const gamesStore = tx.objectStore('games')
        const g = await gamesStore.get(gameId!)
        if (g) {
          g.status = 'won'
          g.ended_at = Date.now()
          await gamesStore.put(g)
        }
        await updateCountryDiscoveryStats(tx, match.country_id, row.rank)
        await updateDistributionStatsOnWin(tx, match.country_id, row.rank)
      }
      await tx.done
      await pruneHistoricalGameData(gameId)
      syncChannel?.postMessage({ type: 'SYNC' })
    }).catch(e => { console.error("Failed to save guess", e); alert("Failed to save guess: " + e.message); })

    return { valid: true, correct: isCorrect }
  }

  async function saveClues(newClues: string[]) {
    userClues = newClues
    const db = await getDB()
    const roundCustomDataSnapshot = manifest && rows.length === 0
      ? await buildRoundCustomDataSnapshot(newClues, db, manifest)
      : undefined
    const tx = db.transaction(['settings', 'games'], 'readwrite')
    await tx.objectStore('settings').put(newClues, 'selected_clues')
    
    // Apply to current game if no guesses
    let appliedNow = false
    if (gameId && rows.length === 0) {
      const g = await tx.objectStore('games').get(gameId)
      if (g && g.status === 'playing') {
        g.selected_clues = newClues
        if (manifest) {
          g.round_clue_snapshot = buildRoundClueSnapshot(newClues, manifest)
          g.round_custom_data_snapshot = roundCustomDataSnapshot ?? {}
        }
        await tx.objectStore('games').put(g)
        appliedNow = true
      }
    }
    await tx.done
    
    if (appliedNow && manifest) {
       await syncDatasets(newClues)
       const db2 = await getDB()
       await reloadCountryPool(db2)
       if (targetCountry) {
         targetCountry = countryPool.find(c => c.country_id === targetCountry!.country_id) || targetCountry
       }
       updateActiveCluesFromList(newClues, manifest)
    }
    
    syncChannel?.postMessage({ type: 'SETTINGS_SYNC' })
  }

  async function reloadCountryPool(db: any) {
    const tx = db.transaction('dataset_rows', 'readonly')
    const store = tx.objectStore('dataset_rows')
    const allRows = await store.getAll()

    const merged = new Map<string, CountryRecord>()

    for (const row of allRows) {
      if (row.dataset_id === 'countries_base') {
        merged.set(row.country_id, {
          country_id: row.country_id,
          name: row.name!,
          lat: row.lat!,
          lon: row.lon!,
        })
      }
    }

    for (const row of allRows) {
      if (row.dataset_id !== 'countries_base') {
        const rec = merged.get(row.country_id)
        if (rec) {
          rec[row.dataset_id] = row.value
        }
      }
    }

    countryPool = Array.from(merged.values())
  }

  async function giveUp() {
    if (!gameId || hasWon || hasGivenUp || !targetCountry) return

    const giveUpAttempts = rows.length

    hasGivenUp = true

    // Generate reveal row with all-blue chips
    const revealResults: ClueResult[] = activeClues.map(cDef => {
      if (cDef.type === 'computed' && cDef.id === 'hemisphere') {
        const r = evaluateHemisphere(targetCountry!.lat, targetCountry!.lat)
        return { ...r, tone: 'blue' as ChipTone }
      }
      if (cDef.type === 'computed' && cDef.id === 'coordinates') {
        return { clue: 'Coordinates', value: '✓', tone: 'blue' as ChipTone, kind: 'direction' as ClueKind }
      }
      if (cDef.type === 'categorical') {
        const r = evaluateCategorical(targetCountry![cDef.id], targetCountry![cDef.id], cDef.label)
        return { ...r, tone: 'blue' as ChipTone }
      }
      let datasetKey = cDef.id
      if (cDef.group === 'temperature_avg_c') {
        const found = Object.keys(targetCountry!).find(k => k.startsWith('temperature_avg_c_m'))
        if (found) datasetKey = found
      }
      const r = evaluateNumeric(targetCountry![datasetKey], targetCountry![datasetKey], cDef.label, cDef.unitSymbol)
      return { ...r, tone: 'blue' as ChipTone, trend: undefined }
    })

    const revealRow: GuessRow = {
      rank: rows.length + 1,
      country: targetCountry.name,
      results: revealResults
    }
    rows = [...rows, revealRow]

    const db = await getDB()
    const tx = db.transaction(['games', 'global_round_stats', 'country_round_difficulty_stats'], 'readwrite')
    const g = await tx.objectStore('games').get(gameId)
    if (g) {
      g.status = 'lost'
      g.ended_at = Date.now()
      await tx.objectStore('games').put(g)
    }
    await updateDistributionStatsOnGiveUp(tx, targetCountry.country_id, giveUpAttempts)
    await tx.done
    await pruneHistoricalGameData(gameId)
    syncChannel?.postMessage({ type: 'SYNC' })
  }

  return {
    get loading() { return loading },
    get query() { return query },
    set query(v: string) { query = v; previewCountry = null },
    get rows() { return rows },
    get errorCountry() { return errorCountry },
    get correctCountry() { return correctCountry },
    get hasWon() { return hasWon },
    get hasGivenUp() { return hasGivenUp },
    get gameOver() { return hasWon || hasGivenUp },
    get targetCountryName() { return targetCountry?.name ?? null },
    get gaveUpCountry() { return hasGivenUp ? (targetCountry?.name ?? null) : null },
    get isTyping() { return isTyping },
    get suggestions() { return suggestions },
    get bestMatch() { return bestMatch },
    get userClues() { return userClues },
    get activeClues() { return activeClues },
    get availableClues() { return availableClues },
    get manifest() { return manifest },
    get countryPool() { return countryPool },
    set preview(country: string | null) { previewCountry = country },
    initGame,
    resetGame,
    isValid,
    submitGuess,
    saveClues,
    refreshCustomClueCatalog,
    getDiscoveryStats,
    getDistributionStats,
    getClueUsageStats,
    giveUp,
  }
}

export const createGameState = createArcadeGameState
