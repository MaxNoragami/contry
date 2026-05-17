import { ApiError, getCluePack, type CluePackDetailDto } from '../api/client'
import type { IDBPDatabase } from 'idb'
import type { DatasetClueEntry } from '../datasets/manifest'
import type { ContryDBSchema } from '../stores/db'
import {
  getActiveClueWorkspaceId,
  loadWorkspaceCloudLinks,
  loadWorkspaceCustomClues,
  repairWorkspaceSelectedClues,
  removeWorkspaceCustomRowsForClue,
  saveWorkspaceCloudLinks,
  saveWorkspaceCustomClues,
  setWorkspaceCustomRowsForClue,
} from './workspace'

export type ClueCloudSyncState = 'synced' | 'dirty'

const lastWorkspaceSyncAt = new Map<string, number>()

export type ClueCloudLink = {
  remoteId: string
  ownerId: string
  ownerUsername: string
  visibility: 'public' | 'private'
  remoteUpdatedAtUtc: string
  lastSyncedAt: number
  syncState: ClueCloudSyncState
}

export async function loadClueCloudLinks(db: IDBPDatabase<ContryDBSchema>): Promise<Record<string, ClueCloudLink>> {
  return loadWorkspaceCloudLinks(db)
}

export async function saveClueCloudLinks(db: IDBPDatabase<ContryDBSchema>, links: Record<string, ClueCloudLink>) {
  await saveWorkspaceCloudLinks(db, links)
}

export async function setClueCloudLink(db: IDBPDatabase<ContryDBSchema>, clueId: string, link: ClueCloudLink | null) {
  const links = await loadClueCloudLinks(db)
  if (link) links[clueId] = link
  else delete links[clueId]
  await saveClueCloudLinks(db, links)
}

export async function markClueCloudLinkDirty(db: IDBPDatabase<ContryDBSchema>, clueId: string) {
  const links = await loadClueCloudLinks(db)
  const link = links[clueId]
  if (!link) return
  links[clueId] = { ...link, syncState: 'dirty' }
  await saveClueCloudLinks(db, links)
}

export function canPushCloudLink(link: ClueCloudLink | null | undefined, currentUserId?: string | null, role?: string | null): boolean {
  if (!link || !currentUserId) return false
  return link.ownerId === currentUserId || role === 'ADMIN'
}

export async function findLocalClueIdByRemoteId(db: IDBPDatabase<ContryDBSchema>, remoteId: string): Promise<string | null> {
  const links = await loadClueCloudLinks(db)
  for (const [clueId, link] of Object.entries(links)) {
    if (link.remoteId === remoteId) return clueId
  }
  return null
}

export async function importPublishedClueToLocal(
  db: IDBPDatabase<ContryDBSchema>,
  cluePack: CluePackDetailDto,
): Promise<{ localId: string; imported: boolean }> {
  const existingLinkedId = await findLocalClueIdByRemoteId(db, cluePack.id)
  if (existingLinkedId) {
    await overwriteLocalClueFromRemote(db, existingLinkedId, cluePack)
    return { localId: existingLinkedId, imported: false }
  }

  const existingClues = await loadWorkspaceCustomClues(db)
  const links = await loadClueCloudLinks(db)

  const localId = getUniqueLocalId(cluePack.datasetId, existingClues.map((clue) => clue.id))
  const nextClues = [
    ...existingClues,
    toLocalClueEntry(cluePack, localId),
  ]

  await saveWorkspaceCustomClues(db, nextClues)
  await saveWorkspaceCloudLinks(db, {
    ...links,
    [localId]: createCloudLink(cluePack),
  })

  await replaceDatasetRows(db, localId, cluePack.rows)
  return { localId, imported: true }
}

export async function overwriteLocalClueFromRemote(
  db: IDBPDatabase<ContryDBSchema>,
  localId: string,
  cluePack: CluePackDetailDto,
) {
  const existingClues = await loadWorkspaceCustomClues(db)
  const links = await loadClueCloudLinks(db)

  const nextClues = existingClues.map((clue) => clue.id === localId ? toLocalClueEntry(cluePack, localId) : clue)
  await saveWorkspaceCustomClues(db, nextClues)
  await saveWorkspaceCloudLinks(db, {
    ...links,
    [localId]: createCloudLink(cluePack),
  })

  await replaceDatasetRows(db, localId, cluePack.rows)
}

export async function removeClueCloudLink(db: IDBPDatabase<ContryDBSchema>, clueId: string) {
  await setClueCloudLink(db, clueId, null)
}

export async function syncWorkspaceLinkedClues(
  db: IDBPDatabase<ContryDBSchema>,
  fetchDetail: (remoteId: string) => Promise<CluePackDetailDto>,
  options: { force?: boolean; maxAgeMs?: number } = {},
) {
  const workspaceId = getActiveClueWorkspaceId()
  const maxAgeMs = options.maxAgeMs ?? 60000
  const lastSyncAt = lastWorkspaceSyncAt.get(workspaceId) ?? 0
  if (!options.force && Date.now() - lastSyncAt < maxAgeMs) {
    return { updated: 0, removed: 0 }
  }

  const links = await loadWorkspaceCloudLinks(db)
  const clues = await loadWorkspaceCustomClues(db)
  if (Object.keys(links).length === 0 || clues.length === 0) {
    lastWorkspaceSyncAt.set(workspaceId, Date.now())
    return { updated: 0, removed: 0 }
  }

  let nextClues = [...clues]
  let nextLinks = { ...links }
  let updated = 0
  let removed = 0

  for (const [localId, link] of Object.entries(links)) {
    try {
      const detail = await fetchDetail(link.remoteId)
      if (detail.updatedAtUtc <= link.remoteUpdatedAtUtc) continue

      nextClues = nextClues.map((clue) => clue.id === localId ? toLocalClueEntry(detail, localId) : clue)
      nextLinks[localId] = createCloudLink(detail)
      await replaceDatasetRows(db, localId, detail.rows)
      updated += 1
    } catch (error) {
      if (!(error instanceof ApiError) || error.status !== 404) {
        continue
      }

      nextClues = nextClues.filter((clue) => clue.id !== localId)
      delete nextLinks[localId]
      await removeWorkspaceCustomRowsForClue(db, localId)
      removed += 1
    }
  }

  if (updated > 0 || removed > 0) {
    await saveWorkspaceCustomClues(db, nextClues)
    await saveWorkspaceCloudLinks(db, nextLinks)
    await repairWorkspaceSelectedClues(db)
  }

  lastWorkspaceSyncAt.set(workspaceId, Date.now())
  return { updated, removed }
}

export function getCloudDetailFetcher(auth?: {
  isAuthenticated: boolean
  request: <T>(path: string, options?: { method?: string }) => Promise<T>
}) {
  if (auth?.isAuthenticated) {
    return (remoteId: string) => auth.request<CluePackDetailDto>(`/clue-packs/${remoteId}`)
  }

  return (remoteId: string) => getCluePack(remoteId)
}

export function createCloudLinkFromDetail(cluePack: CluePackDetailDto): ClueCloudLink {
  return {
    remoteId: cluePack.id,
    ownerId: cluePack.ownerId,
    ownerUsername: cluePack.ownerUsername,
    visibility: cluePack.visibility,
    remoteUpdatedAtUtc: cluePack.updatedAtUtc,
    lastSyncedAt: Date.now(),
    syncState: 'synced',
  }
}

function createCloudLink(cluePack: CluePackDetailDto): ClueCloudLink {
  return createCloudLinkFromDetail(cluePack)
}

function toLocalClueEntry(cluePack: CluePackDetailDto, localId: string): DatasetClueEntry {
  return {
    id: localId,
    dataset_id: localId,
    source: 'custom',
    type: cluePack.type,
    computed: false,
    metadata_path: '',
    metadata_checksum: 'sha256:custom',
    label: cluePack.label,
    description: cluePack.description,
    comparator: cluePack.comparator,
    unit_symbol: cluePack.unitSymbol || undefined,
    icon: cluePack.icon,
    categories: cluePack.type === 'categorical' ? [...cluePack.categories] : undefined,
  }
}

function getUniqueLocalId(baseId: string, existingIds: string[]): string {
  if (!existingIds.includes(baseId)) return baseId
  let suffix = 2
  while (existingIds.includes(`${baseId}_${suffix}`)) suffix += 1
  return `${baseId}_${suffix}`
}

async function replaceDatasetRows(
  db: IDBPDatabase<ContryDBSchema>,
  localId: string,
  rows: CluePackDetailDto['rows'],
) {
  await removeWorkspaceCustomRowsForClue(db, localId)
  await setWorkspaceCustomRowsForClue(db, localId, rows.map((row) => ({
    country_id: row.countryId,
    value: row.value ?? null,
  })))
}
