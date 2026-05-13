import Papa from 'papaparse'
import { getApiBaseUrl } from '../api/client'
import { getDB, type DatasetRow } from '../stores/db'
import type { DatasetManifest, DatasetBaseEntry, DatasetClueEntry } from './manifest'
import { loadCustomCluesFromSettings, resolveSelectedClues } from './clue-registry'

export async function syncDatasets(clueIds: string[]): Promise<DatasetManifest> {
  const db = await getDB()
  const manifest = await fetchCanonicalManifest(db)
  const customClues = await loadCustomCluesFromSettings(db)

  const metaTx = db.transaction('dataset_meta', 'readonly')
  const metaStore = metaTx.objectStore('dataset_meta')

  const toFetch: (DatasetBaseEntry | DatasetClueEntry)[] = []

  // Check base
  const baseMeta = await metaStore.get('countries_base')
  if (!baseMeta || baseMeta.checksum !== manifest.base.checksum) {
    toFetch.push(manifest.base)
  }

  const resolvedClues = resolveSelectedClues(clueIds, manifest, customClues)

  // Check which clues need fetching
  for (const resolved of resolvedClues) {
    const clue = resolved.entry
    if (resolved.origin === 'custom') continue
    if (clue.computed) continue // No data to fetch for computed clues
    const meta = await metaStore.get(clue.dataset_id)
    if (!meta || meta.checksum !== clue.data_checksum) {
      toFetch.push(clue)
    }
  }

  await metaTx.done

  // Fetch and parse all outdated
  for (const entry of toFetch) {
    const isBase = 'dataset_id' in entry && (entry as DatasetBaseEntry).dataset_id === 'countries_base'
    const id = isBase ? (entry as DatasetBaseEntry).dataset_id : (entry as DatasetClueEntry).dataset_id
    const checksum = isBase ? (entry as DatasetBaseEntry).checksum : (entry as DatasetClueEntry).data_checksum!
    const path = isBase ? (entry as DatasetBaseEntry).path : (entry as DatasetClueEntry).data_path!

    const csvRes = await fetch(`${getApiBaseUrl()}${path}`)
    const csvText = await csvRes.text()

    const parsed = Papa.parse<any>(csvText, {
      header: true,
      skipEmptyLines: true,
      dynamicTyping: (field) => {
        // Prevent 'country_id' and 'name' from being converted
        return field !== 'country_id' && field !== 'name'
      }
    })

    const rows: DatasetRow[] = parsed.data.map(row => {
      if (isBase) {
        return {
          dataset_id: id,
          country_id: String(row.country_id),
          name: row.name,
          lat: row.lat,
          lon: row.lon
        }
      } else {
        return {
          dataset_id: id,
          country_id: String(row.country_id),
          value: row.value
        }
      }
    })

    const tx = db.transaction(['dataset_rows', 'dataset_meta'], 'readwrite')
    const rowsStore = tx.objectStore('dataset_rows')
    
    // Clear old data for this dataset
    const index = rowsStore.index('by-dataset')
    let cursor = await index.openCursor(id)
    while (cursor) {
      await cursor.delete()
      cursor = await cursor.continue()
    }

    // Insert new data
    for (const row of rows) {
      if (!row.country_id) continue
      await rowsStore.put(row)
    }

    // Update metadata
    await tx.objectStore('dataset_meta').put({
      dataset_id: id,
      checksum,
      updated_at: Date.now()
    })

    await tx.done
  }

  return manifest
}

async function fetchCanonicalManifest(db: Awaited<ReturnType<typeof getDB>>): Promise<DatasetManifest> {
  try {
    const response = await fetch(`${getApiBaseUrl()}/datasets/manifest.json`)
    const manifest: DatasetManifest = await response.json()
    await db.transaction('settings', 'readwrite').objectStore('settings').put(manifest, 'dataset_manifest')
    return manifest
  } catch (error) {
    const cachedManifest = await db.transaction('settings', 'readonly').objectStore('settings').get('dataset_manifest')
    if (cachedManifest) {
      return cachedManifest as DatasetManifest
    }

    throw error
  }
}
