import type { IDBPDatabase } from 'idb'
import { DEFAULT_CLUE_IDS } from '../config/app'
import { STORAGE_KEYS } from '../config/app'
import type { DatasetClueEntry } from '../datasets/manifest'
import type { ContryDBSchema } from '../stores/db'
import type { ClueCloudLink } from './cloud'

export type ClueWorkspaceId = string

export type CustomClueRow = {
  country_id: string
  value: string | number | null
}

export type CustomClueRowsMap = Record<string, CustomClueRow[]>

const GUEST_WORKSPACE_ID = 'guest'

export function getGuestWorkspaceId(): ClueWorkspaceId {
  return GUEST_WORKSPACE_ID
}

export function getUserWorkspaceId(userId: string): ClueWorkspaceId {
  return `user:${userId}`
}

export function getWorkspaceIdForUser(userId?: string | null): ClueWorkspaceId {
  return userId ? getUserWorkspaceId(userId) : getGuestWorkspaceId()
}

export function getActiveClueWorkspaceId(): ClueWorkspaceId {
  if (typeof window === 'undefined') return GUEST_WORKSPACE_ID
  return window.localStorage.getItem(STORAGE_KEYS.clueWorkspace) || GUEST_WORKSPACE_ID
}

export function setActiveClueWorkspaceId(workspaceId: ClueWorkspaceId) {
  if (typeof window === 'undefined') return
  window.localStorage.setItem(STORAGE_KEYS.clueWorkspace, workspaceId)
}

export function getSelectedCluesStorageKey(workspaceId = getActiveClueWorkspaceId()): string {
  return `selected_clues:${workspaceId}`
}

export function getCustomCluesStorageKey(workspaceId = getActiveClueWorkspaceId()): string {
  return `custom_clues:${workspaceId}`
}

export function getCloudLinksStorageKey(workspaceId = getActiveClueWorkspaceId()): string {
  return `clue_cloud_links:${workspaceId}`
}

export function getCustomRowsStorageKey(workspaceId = getActiveClueWorkspaceId()): string {
  return `custom_clue_rows:${workspaceId}`
}

export async function loadWorkspaceCustomClues(db: IDBPDatabase<ContryDBSchema>, workspaceId = getActiveClueWorkspaceId()): Promise<DatasetClueEntry[]> {
  const settings = db.transaction('settings', 'readonly').objectStore('settings')
  const raw = await settings.get(getCustomCluesStorageKey(workspaceId))
  if (!Array.isArray(raw)) return []
  return raw as DatasetClueEntry[]
}

export async function saveWorkspaceCustomClues(db: IDBPDatabase<ContryDBSchema>, clues: DatasetClueEntry[], workspaceId = getActiveClueWorkspaceId()) {
  const tx = db.transaction('settings', 'readwrite')
  await tx.objectStore('settings').put(clues, getCustomCluesStorageKey(workspaceId))
  await tx.done
}

export async function loadWorkspaceSelectedClues(db: IDBPDatabase<ContryDBSchema>, workspaceId = getActiveClueWorkspaceId()): Promise<string[] | null> {
  const settings = db.transaction('settings', 'readonly').objectStore('settings')
  const raw = await settings.get(getSelectedCluesStorageKey(workspaceId))
  return Array.isArray(raw) ? raw as string[] : null
}

export async function saveWorkspaceSelectedClues(db: IDBPDatabase<ContryDBSchema>, clueIds: string[], workspaceId = getActiveClueWorkspaceId()) {
  const tx = db.transaction('settings', 'readwrite')
  await tx.objectStore('settings').put(clueIds, getSelectedCluesStorageKey(workspaceId))
  await tx.done
}

export async function repairWorkspaceSelectedClues(
  db: IDBPDatabase<ContryDBSchema>,
  workspaceId = getActiveClueWorkspaceId(),
  activeCount = 5,
) {
  const selected = await loadWorkspaceSelectedClues(db, workspaceId)
  const customClues = await loadWorkspaceCustomClues(db, workspaceId)
  const availableCustomIds = customClues.map((clue) => clue.id)
  const availableIdSet = new Set([...DEFAULT_CLUE_IDS, ...availableCustomIds])
  const nextSelected = Array.isArray(selected)
    ? selected.filter((id) => availableIdSet.has(id))
    : []

  for (const id of availableCustomIds) {
    if (nextSelected.length >= activeCount) break
    if (!nextSelected.includes(id)) nextSelected.push(id)
  }

  for (const id of DEFAULT_CLUE_IDS) {
    if (nextSelected.length >= activeCount) break
    if (!nextSelected.includes(id)) nextSelected.push(id)
  }

  await saveWorkspaceSelectedClues(db, nextSelected.slice(0, activeCount), workspaceId)
}

export async function loadWorkspaceCloudLinks(db: IDBPDatabase<ContryDBSchema>, workspaceId = getActiveClueWorkspaceId()): Promise<Record<string, ClueCloudLink>> {
  const settings = db.transaction('settings', 'readonly').objectStore('settings')
  const raw = await settings.get(getCloudLinksStorageKey(workspaceId))
  if (!raw || typeof raw !== 'object') return {}
  return raw as Record<string, ClueCloudLink>
}

export async function saveWorkspaceCloudLinks(db: IDBPDatabase<ContryDBSchema>, links: Record<string, ClueCloudLink>, workspaceId = getActiveClueWorkspaceId()) {
  const tx = db.transaction('settings', 'readwrite')
  await tx.objectStore('settings').put(links, getCloudLinksStorageKey(workspaceId))
  await tx.done
}

export async function loadWorkspaceCustomRows(db: IDBPDatabase<ContryDBSchema>, workspaceId = getActiveClueWorkspaceId()): Promise<CustomClueRowsMap> {
  const settings = db.transaction('settings', 'readonly').objectStore('settings')
  const raw = await settings.get(getCustomRowsStorageKey(workspaceId))
  if (!raw || typeof raw !== 'object') return {}
  return raw as CustomClueRowsMap
}

export async function saveWorkspaceCustomRows(db: IDBPDatabase<ContryDBSchema>, rows: CustomClueRowsMap, workspaceId = getActiveClueWorkspaceId()) {
  const tx = db.transaction('settings', 'readwrite')
  await tx.objectStore('settings').put(rows, getCustomRowsStorageKey(workspaceId))
  await tx.done
}

export async function setWorkspaceCustomRowsForClue(
  db: IDBPDatabase<ContryDBSchema>,
  clueId: string,
  rows: CustomClueRow[],
  workspaceId = getActiveClueWorkspaceId(),
) {
  const allRows = await loadWorkspaceCustomRows(db, workspaceId)
  allRows[clueId] = rows.map((row) => ({ country_id: row.country_id, value: row.value }))
  await saveWorkspaceCustomRows(db, allRows, workspaceId)
}

export async function removeWorkspaceCustomRowsForClue(
  db: IDBPDatabase<ContryDBSchema>,
  clueId: string,
  workspaceId = getActiveClueWorkspaceId(),
) {
  const allRows = await loadWorkspaceCustomRows(db, workspaceId)
  delete allRows[clueId]
  await saveWorkspaceCustomRows(db, allRows, workspaceId)
}

export async function switchActiveClueWorkspace(db: IDBPDatabase<ContryDBSchema>, nextWorkspaceId: ClueWorkspaceId) {
  const previousWorkspaceId = getActiveClueWorkspaceId()
  if (previousWorkspaceId === nextWorkspaceId) {
    await materializeWorkspaceCustomRows(db, nextWorkspaceId)
    return
  }

  await clearWorkspaceRowsFromDatasetStore(db, previousWorkspaceId)
  setActiveClueWorkspaceId(nextWorkspaceId)
  await materializeWorkspaceCustomRows(db, nextWorkspaceId)
}

export async function materializeWorkspaceCustomRows(db: IDBPDatabase<ContryDBSchema>, workspaceId = getActiveClueWorkspaceId()) {
  await clearWorkspaceRowsFromDatasetStore(db, workspaceId)
  const rowsByClue = await loadWorkspaceCustomRows(db, workspaceId)
  const tx = db.transaction('dataset_rows', 'readwrite')
  const store = tx.objectStore('dataset_rows')

  for (const [clueId, rows] of Object.entries(rowsByClue)) {
    for (const row of rows) {
      await store.put({
        dataset_id: clueId,
        country_id: row.country_id,
        value: row.value ?? undefined,
      })
    }
  }

  await tx.done
}

async function clearWorkspaceRowsFromDatasetStore(db: IDBPDatabase<ContryDBSchema>, workspaceId: ClueWorkspaceId) {
  const clues = await loadWorkspaceCustomClues(db, workspaceId)
  if (clues.length === 0) return

  const tx = db.transaction('dataset_rows', 'readwrite')
  const store = tx.objectStore('dataset_rows')
  const index = store.index('by-dataset')

  for (const clue of clues) {
    let cursor = await index.openCursor(clue.id)
    while (cursor) {
      await cursor.delete()
      cursor = await cursor.continue()
    }
  }

  await tx.done
}
