import { Globe, Compass, Thermometer, Users, Navigation, type Icon as LucideIcon } from 'lucide-svelte'
import { getApiBaseUrl } from '../api/client'
import { syncDatasets } from '../datasets/ingest'
import type { DatasetManifest } from '../datasets/manifest'
import { formatNumeric } from '../engine/clues'
import type { createAuthStore } from './auth.svelte'
import { toastStore } from './toasts.svelte'
import type {
  ClueDef,
  ClueKind,
  ClueResult,
  ChipTone,
  CountryRecord,
  GuessRow,
} from './game.svelte'

type RankedClueResponse = {
  id: string
  label: string
  description: string
  icon: string
  type: 'numeric' | 'categorical' | 'computed'
  comparator: string
  group: string | null
  unitSymbol: string | null
}

type RankedClueResultResponse = {
  clueId: string
  value: string
  tone: 'green' | 'yellow' | 'red' | 'blue'
  kind: 'text' | 'numeric' | 'direction'
  trend: 'higher' | 'lower' | null
}

type RankedGuessResponse = {
  attemptNumber: number
  guessCountryId: string
  guessCountryName: string
  results: RankedClueResultResponse[]
  createdAtUtc: string
}

type RankedChallengeResponse = {
  challengeDateUtc: string
  clues: RankedClueResponse[]
}

type RankedSessionResponse = {
  challengeDateUtc: string
  status: 'not_started' | 'playing' | 'won' | 'lost'
  guessCount: number
  completedAtUtc: string | null
  guesses: RankedGuessResponse[]
}

type CreateRankedGuessResponse = {
  challengeDateUtc: string
  status: 'playing' | 'won' | 'lost'
  guessCount: number
  completedAtUtc: string | null
  guess: RankedGuessResponse
}

const iconMap: Record<string, typeof LucideIcon> = {
  globe: Globe,
  compass: Compass,
  thermometer: Thermometer,
  users: Users,
  navigation: Navigation,
}

const DEFAULT_RANKED_CLUES = ['hemisphere', 'continent', 'temperature_avg_c', 'population', 'coordinates']

export function createRankedGameState(auth: ReturnType<typeof createAuthStore>) {
  let query = $state('')
  let previewCountry = $state<string | null>(null)
  let rows = $state<GuessRow[]>([])
  let errorCountry = $state<string | null>(null)
  let correctCountry = $state<string | null>(null)
  let gaveUpCountry = $state<string | null>(null)
  let hasWon = $state(false)
  let loading = $state(false)
  let countryPool = $state<CountryRecord[]>([])
  let activeClues = $state<ClueDef[]>([])
  let availableClues = $state<ClueDef[]>([])
  let challengeDateUtc = $state<string | null>(null)
  let sessionStatus = $state<'not_started' | 'playing' | 'won' | 'lost'>('not_started')
  let initialized = $state(false)
  let manifest = $state<DatasetManifest | null>(null)
  let pendingGuessCountryId = $state<string | null>(null)

  const isTyping = $derived(query.trim().length > 0)
  const guessedNames = $derived(new Set(rows.map((row) => row.country.toLowerCase())))

  const suggestions = $derived.by(() => {
    const cleaned = query.trim().toLowerCase()
    if (cleaned.length < 1) return []

    return countryPool
      .map((country) => country.name)
      .filter((name) => {
        const lowerName = name.toLowerCase()
        if (guessedNames.has(lowerName)) return false
        return lowerName.split(/[\s-]+/).some((word) => word.startsWith(cleaned))
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
    if (gaveUpCountry) {
      const match = countryPool.find((country) => country.name === gaveUpCountry)
      return match ? { name: match.name, lat: match.lat, lon: match.lon } : null
    }

    if (previewCountry) {
      const queryName = previewCountry.toLowerCase()
      if (guessedNames.has(queryName)) return null
      const match = countryPool.find((country) => country.name.toLowerCase() === queryName)
      return match ? { name: match.name, lat: match.lat, lon: match.lon } : null
    }

    if (isTyping && suggestions.length > 0) {
      const match = countryPool.find((country) => country.name === suggestions[0])
      return match ? { name: match.name, lat: match.lat, lon: match.lon } : null
    }

    return null
  })

  async function initGame() {
    if (!auth.isAuthenticated) {
      resetState()
      loading = false
      initialized = false
      return
    }

    loading = true

    try {
      if (countryPool.length === 0) {
        await hydrateCountryPool()
      }

      const challenge = await auth.request<RankedChallengeResponse>('/ranked/challenges/current')
      challengeDateUtc = challenge.challengeDateUtc
      activeClues = challenge.clues.map(toClueDef)
      availableClues = [...activeClues]

      const session = await auth.request<RankedSessionResponse>('/ranked/sessions/current')
      applySession(session)
      initialized = true
    } finally {
      loading = false
    }
  }

  async function hydrateCountryPool() {
    manifest = await syncDatasets(DEFAULT_RANKED_CLUES)
    const baseResponse = await fetch(`${getApiBaseUrl()}/datasets/base/countries.csv`)
    const baseCsv = await baseResponse.text()
    const lines = baseCsv.trim().split(/\r?\n/).slice(1)

    countryPool = lines.map((line) => {
      const [countryId, name, lat, lon] = parseCsvLine(line)
      return {
        country_id: countryId,
        name,
        lat: Number(lat),
        lon: Number(lon),
      }
    })
  }

  function applySession(session: RankedSessionResponse) {
    sessionStatus = session.status
    hasWon = session.status === 'won'
    rows = session.guesses.map(toGuessRow)
    gaveUpCountry = session.status === 'lost' && session.guesses.length > 0
      ? session.guesses[session.guesses.length - 1].guessCountryName
      : null
    correctCountry = session.status === 'won' && session.guesses.length > 0
      ? session.guesses[session.guesses.length - 1].guessCountryName
      : null
  }

  function isValid(country: string) {
    const lower = country.toLowerCase()
    return !!countryPool.find((entry) => entry.name.toLowerCase() === lower) && !guessedNames.has(lower)
  }

  async function submitGuess(countryName: string): Promise<{ valid: boolean; correct?: boolean }> {
    if (!auth.isAuthenticated || hasWon || sessionStatus === 'lost') {
      return { valid: false }
    }

    const match = countryPool.find((country) => country.name.toLowerCase() === countryName.toLowerCase())
    if (!match || guessedNames.has(match.name.toLowerCase())) {
      return { valid: false }
    }

    const pendingRow = createPendingRow(match.name)
    rows = [...rows, pendingRow]
    pendingGuessCountryId = match.country_id

    try {
      const response = await auth.request<CreateRankedGuessResponse>('/ranked/guesses', {
        method: 'POST',
        body: { countryId: match.country_id },
      })

      sessionStatus = response.status
      hasWon = response.status === 'won'
      const nextRow = toGuessRow(response.guess)
      rows = rows.map((row) => row.pending ? nextRow : row)
      correctCountry = response.status === 'won' ? response.guess.guessCountryName : null
      query = ''
      previewCountry = null
      pendingGuessCountryId = null

      if (!hasWon) {
        errorCountry = response.guess.guessCountryName
        setTimeout(() => {
          if (errorCountry === response.guess.guessCountryName) {
            errorCountry = null
          }
        }, 500)
      }

      return { valid: true, correct: hasWon }
    } catch (error) {
      rows = rows.filter((row) => !row.pending)
      pendingGuessCountryId = null
      if (!auth.isAuthenticated) {
        toastStore.push('Your session expired. Please log in again.')
      } else {
        toastStore.push('Failed to submit ranked guess. Please try again.')
      }
      return { valid: false }
    }
  }

  async function resetGame() {
    await initGame()
  }

  async function giveUp() {
    if (!auth.isAuthenticated || hasWon || sessionStatus === 'lost') {
      return
    }

    try {
      const session = await auth.request<RankedSessionResponse>('/ranked/sessions/current/give-up', {
        method: 'POST',
      })

      applySession(session)
      query = ''
      previewCountry = null
      pendingGuessCountryId = null
    } catch (error) {
      if (!auth.isAuthenticated) {
        toastStore.push('Your session expired. Please log in again.')
      } else {
        toastStore.push('Failed to give up in ranked. Please try again.')
      }
    }
  }

  function clearSession() {
    resetState()
    initialized = false
  }

  function resetState() {
    query = ''
    previewCountry = null
    rows = []
    errorCountry = null
    correctCountry = null
    gaveUpCountry = null
    hasWon = false
    sessionStatus = 'not_started'
    activeClues = []
    availableClues = []
    challengeDateUtc = null
    pendingGuessCountryId = null
  }

  function toClueDef(clue: RankedClueResponse): ClueDef {
    return {
      id: clue.id,
      label: clue.label,
      description: clue.description,
      icon: clue.icon && iconMap[clue.icon] ? iconMap[clue.icon] : null,
      customIcon: clue.icon && !iconMap[clue.icon] ? clue.icon : undefined,
      type: clue.type,
      group: clue.group || undefined,
      unitSymbol: clue.unitSymbol || undefined,
      source: 'builtin',
    }
  }

  function toGuessRow(guess: RankedGuessResponse): GuessRow {
    return {
      rank: guess.attemptNumber,
      country: guess.guessCountryName,
      results: guess.results.map((result) => ({
        clue: activeClues.find((entry) => entry.id === result.clueId)?.label || result.clueId,
        value: normalizeResultValue(result),
        tone: result.tone as ChipTone,
        kind: result.kind as ClueKind,
        trend: result.trend || undefined,
      } satisfies ClueResult)),
    }
  }

  function createPendingRow(countryName: string): GuessRow {
    return {
      rank: rows.length + 1,
      country: countryName,
      pending: true,
      results: activeClues.map((clue) => ({
        clue: clue.label,
        value: '',
        tone: 'blue' as ChipTone,
        kind: clue.type === 'numeric' ? 'numeric' as ClueKind : clue.id === 'coordinates' ? 'direction' as ClueKind : 'text' as ClueKind,
        pending: true,
      })),
    }
  }

  function normalizeResultValue(result: RankedClueResultResponse): string {
    const clue = activeClues.find((entry) => entry.id === result.clueId)
    if (!clue || clue.type !== 'numeric' || result.value === 'NO DATA') {
      return result.value
    }

    const parsedNumber = parseLeadingNumber(result.value)
    if (parsedNumber === null) {
      return result.value
    }

    return formatNumeric(parsedNumber, clue.unitSymbol)
  }

  function parseLeadingNumber(value: string): number | null {
    const normalized = value.replace(/,/g, '')
    const match = normalized.match(/-?\d+(?:\.\d+)?/)
    if (!match) {
      return null
    }

    const parsed = Number(match[0])
    return Number.isFinite(parsed) ? parsed : null
  }

  function parseCsvLine(line: string) {
    const values: string[] = []
    let current = ''
    let inQuotes = false

    for (let index = 0; index < line.length; index += 1) {
      const character = line[index]
      if (character === '"') {
        if (inQuotes && line[index + 1] === '"') {
          current += '"'
          index += 1
          continue
        }

        inQuotes = !inQuotes
        continue
      }

      if (character === ',' && !inQuotes) {
        values.push(current)
        current = ''
        continue
      }

      current += character
    }

    values.push(current)
    return values
  }

  return {
    get loading() { return loading },
    get query() { return query },
    set query(value: string) { query = value; previewCountry = null },
    get rows() { return rows },
    get errorCountry() { return errorCountry },
    get correctCountry() { return correctCountry },
    get hasWon() { return hasWon },
    get hasGivenUp() { return sessionStatus === 'lost' },
    get gameOver() { return hasWon || sessionStatus === 'lost' },
    get targetCountryName() { return correctCountry ?? gaveUpCountry },
    get gaveUpCountry() { return gaveUpCountry },
    get isTyping() { return isTyping },
    get suggestions() { return suggestions },
    get bestMatch() { return bestMatch },
    get userClues() { return DEFAULT_RANKED_CLUES },
    get activeClues() { return activeClues },
    get availableClues() { return availableClues },
    get manifest() { return manifest },
    get countryPool() { return countryPool },
    get initialized() { return initialized },
    set preview(country: string | null) { previewCountry = country },
    initGame,
    resetGame,
    clearSession,
    isValid,
    submitGuess,
    giveUp,
  }
}
