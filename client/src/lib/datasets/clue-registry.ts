import type { IDBPDatabase } from 'idb'
import type { ContryDBSchema } from '../stores/db'
import type { DatasetClueEntry, DatasetManifest } from './manifest'
import { resolveTemperatureClueForMonth } from './manifest'

export type ResolvedRuntimeClue = {
  id: string
  entry: DatasetClueEntry
  origin: 'builtin' | 'custom'
}

export async function loadCustomCluesFromSettings(
  db: IDBPDatabase<ContryDBSchema>
): Promise<DatasetClueEntry[]> {
  const settings = db.transaction('settings', 'readonly').objectStore('settings')
  const raw = await settings.get('custom_clues')
  if (!Array.isArray(raw)) return []
  return raw as DatasetClueEntry[]
}

export function buildRuntimeClueRegistry(
  manifest: DatasetManifest,
  customClues: DatasetClueEntry[]
) {
  const all = [...manifest.clues, ...customClues]
  const byId = new Map<string, DatasetClueEntry>()
  const byDataset = new Map<string, DatasetClueEntry>()
  const byGroup = new Map<string, DatasetClueEntry>()

  for (const clue of all) {
    byId.set(clue.id, clue)
    byDataset.set(clue.dataset_id, clue)
    if (clue.group && !byGroup.has(clue.group)) byGroup.set(clue.group, clue)
  }

  return { all, byId, byDataset, byGroup }
}

export function resolveSelectedClues(
  clueIds: string[],
  manifest: DatasetManifest,
  customClues: DatasetClueEntry[],
  month = new Date().getMonth() + 1
): ResolvedRuntimeClue[] {
  const registry = buildRuntimeClueRegistry(manifest, customClues)

  return clueIds.map((selectedId) => {
    let entry: DatasetClueEntry | undefined

    if (selectedId === 'temperature_avg_c') {
      entry = resolveTemperatureClueForMonth(manifest, month)
      if (!entry) {
        throw new Error(`Missing temperature clue for month ${month}`)
      }
    } else {
      entry =
        registry.byId.get(selectedId) ||
        registry.byDataset.get(selectedId) ||
        registry.byGroup.get(selectedId)
    }

    if (!entry) {
      throw new Error(`Clue ${selectedId} not found in runtime clue registry`)
    }

    return {
      id: selectedId,
      entry,
      origin: entry.source === 'custom' ? 'custom' : 'builtin',
    }
  })
}
