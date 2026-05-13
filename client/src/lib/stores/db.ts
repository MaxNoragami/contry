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

// Old stats interfaces removed

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
}

export const DB_NAME = 'contry_db'
export const DB_VERSION = 5

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
        // Clean up legacy stats stores from v4 schema
        const legacyStores = ['country_discovery_stats', 'global_round_stats', 'country_round_difficulty_stats', 'clue_usage_stats']
        for (const store of legacyStores) {
          if ((db.objectStoreNames as DOMStringList).contains(store)) {
            (db as any).deleteObjectStore(store)
          }
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
