import { deleteDB, openDB, type DBSchema, type IDBPDatabase } from 'idb'
import type { ClueResult, RoundClueSnapshotEntry } from './game.svelte'

export interface DatasetMeta {
  dataset_id: string
  checksum: string
  updated_at: number
}

export interface DatasetRow {
  dataset_id: string
  country_id: string
  value?: string | number
  lat?: number
  lon?: number
  name?: string
}

export interface GameRecord {
  game_id: string
  target_country_id: string
  selected_clues: string[]
  round_clue_snapshot?: RoundClueSnapshotEntry[]
  round_custom_data_snapshot?: Record<string, Record<string, string | number | null>>
  started_at: number
  ended_at?: number
  status: 'playing' | 'won' | 'lost'
}

export interface GuessRecord {
  game_id: string
  attempt_no: number
  guess_country_id: string
  results: ClueResult[]
}

export interface CountryDiscoveryStat {
  country_id: string
  continent: string | null
  discovered: boolean
  best_attempts: number | null
  solved_count: number
  last_solved_at?: number
}

export interface GlobalRoundStats {
  key: 'summary'
  finished_count: number
  win_count: number
  give_up_count: number
  total_guesses_on_wins: number
  fastest_win: number | null
  slowest_win: number | null
  guess_distribution: Record<string, number>
}

export interface CountryRoundDifficultyStat {
  country_id: string
  give_up_count: number
  solved_count: number
  total_guesses_when_solved: number
}

export interface ClueUsageStat {
  clue_id: string
  usage_count: number
}

export interface ContryDBSchema extends DBSchema {
  settings: {
    key: string
    value: any
  }
  dataset_meta: {
    key: string
    value: DatasetMeta
  }
  dataset_rows: {
    key: [string, string]
    value: DatasetRow
    indexes: {
      'by-dataset': string
    }
  }
  games: {
    key: string
    value: GameRecord
  }
  guesses: {
    key: [string, number]
    value: GuessRecord
    indexes: {
      'by-game': string
    }
  }
  country_discovery_stats: {
    key: string
    value: CountryDiscoveryStat
  }
  global_round_stats: {
    key: string
    value: GlobalRoundStats
  }
  country_round_difficulty_stats: {
    key: string
    value: CountryRoundDifficultyStat
  }
  clue_usage_stats: {
    key: string
    value: ClueUsageStat
  }
}

export const DB_NAME = 'contry_db'
export const DB_VERSION = 4

let dbPromise: Promise<IDBPDatabase<ContryDBSchema>> | null = null

export function getDB(): Promise<IDBPDatabase<ContryDBSchema>> {
  if (!dbPromise) {
    dbPromise = openDB<ContryDBSchema>(DB_NAME, DB_VERSION, {
      upgrade(db) {
        if (!db.objectStoreNames.contains('settings')) {
          db.createObjectStore('settings')
        }
        if (!db.objectStoreNames.contains('dataset_meta')) {
          db.createObjectStore('dataset_meta', { keyPath: 'dataset_id' })
        }
        if (!db.objectStoreNames.contains('dataset_rows')) {
          const rowsStore = db.createObjectStore('dataset_rows', { keyPath: ['dataset_id', 'country_id'] })
          rowsStore.createIndex('by-dataset', 'dataset_id')
        }
        if (!db.objectStoreNames.contains('games')) {
          db.createObjectStore('games', { keyPath: 'game_id' })
        }
        if (!db.objectStoreNames.contains('guesses')) {
          const guessesStore = db.createObjectStore('guesses', { keyPath: ['game_id', 'attempt_no'] })
          guessesStore.createIndex('by-game', 'game_id')
        }
        if (!db.objectStoreNames.contains('country_discovery_stats')) {
          db.createObjectStore('country_discovery_stats', { keyPath: 'country_id' })
        }
        if (!db.objectStoreNames.contains('global_round_stats')) {
          db.createObjectStore('global_round_stats', { keyPath: 'key' })
        }
        if (!db.objectStoreNames.contains('country_round_difficulty_stats')) {
          db.createObjectStore('country_round_difficulty_stats', { keyPath: 'country_id' })
        }
        if (!db.objectStoreNames.contains('clue_usage_stats')) {
          db.createObjectStore('clue_usage_stats', { keyPath: 'clue_id' })
        }
      },
    })
  }
  return dbPromise
}

export async function clearAllCachedData() {
  if (dbPromise) {
    try {
      const db = await dbPromise
      db.close()
    } catch {
      // Ignore close failures during destructive reset.
    }
    dbPromise = null
  }

  await deleteDB(DB_NAME)
}
