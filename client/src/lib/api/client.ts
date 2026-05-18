import { API_PATHS, APP_LIMITS, APP_URLS } from '../config/app'

export interface ProblemDetailsResponse {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  traceId?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  status: number
  problem: ProblemDetailsResponse | null

  constructor(status: number, problem: ProblemDetailsResponse | null, fallbackMessage: string) {
    super(problem?.detail || problem?.title || fallbackMessage)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

type ApiRequestOptions = {
  method?: string
  body?: unknown
  headers?: HeadersInit
  xsrfToken?: string | null
  signal?: AbortSignal
}

export function getApiBaseUrl(): string {
  const explicitBaseUrl = import.meta.env.VITE_API_BASE_URL
  if (explicitBaseUrl) {
    return explicitBaseUrl.replace(/\/$/, '')
  }

  if (import.meta.env.DEV) {
    return ''
  }

  return APP_URLS.api.production
}

export async function apiRequest<T>(path: string, options: ApiRequestOptions = {}): Promise<T> {
  const method = (options.method || 'GET').toUpperCase()
  const headers = new Headers(options.headers)
  const hasJsonBody = options.body !== undefined

  if (hasJsonBody && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  if (options.xsrfToken && ['POST', 'PUT', 'PATCH', 'DELETE'].includes(method)) {
    headers.set('X-XSRF-TOKEN', options.xsrfToken)
  }

  const response = await fetch(`${getApiBaseUrl()}${path}`, {
    method,
    headers,
    credentials: 'include',
    body: hasJsonBody ? JSON.stringify(options.body) : undefined,
    signal: options.signal,
  })

  if (response.status === 204) {
    return undefined as T
  }

  const contentType = response.headers.get('content-type') || ''
  const isJson = contentType.includes('application/json') || contentType.includes('application/problem+json')
  const payload = isJson ? await response.json() : await response.text()

  if (!response.ok) {
    throw new ApiError(
      response.status,
      typeof payload === 'object' && payload !== null ? payload as ProblemDetailsResponse : null,
      `Request to ${path} failed with status ${response.status}`,
    )
  }

  return payload as T
}

export interface RankedLeaderboardEntry {
  username: string
  averageTries: number
  playedCount: number
}

export interface GetRankedLeaderboardResult {
  items: RankedLeaderboardEntry[]
  totalCount: number
  page: number
  pageSize: number
}

export async function getRankedLeaderboard(page: number = 1, pageSize: number = APP_LIMITS.leaderboardPageSize, signal?: AbortSignal): Promise<GetRankedLeaderboardResult> {
  return apiRequest<GetRankedLeaderboardResult>(`${API_PATHS.leaderboards.ranked}?page=${page}&pageSize=${pageSize}`, { signal })
}

export interface CountryDiscoveryStatDto {
  countryId: string
  discovered: boolean
  bestAttempts: number | null
  solvedCount: number
  lastSolvedAtUtc: string | null
}

export interface ClueUsageStatDto {
  clueId: string
  usageCount: number
  label: string
  icon: string | null
}

export interface MyRankedStatsResult {
  playedCount: number
  wonCount: number
  totalGuessesOnWins: number
  fastestWinGuessCount: number | null
  slowestWinGuessCount: number | null
  currentStreak: number
  bestStreak: number
  guessDistributionJson: string
  countryDiscoveryStats: CountryDiscoveryStatDto[]
  clueUsageStats: ClueUsageStatDto[]
}

export async function getMyRankedStats(signal?: AbortSignal): Promise<MyRankedStatsResult> {
  return apiRequest<MyRankedStatsResult>(API_PATHS.ranked.statsMe, { signal })
}

export async function resetMyRankedStats(xsrfToken: string, signal?: AbortSignal): Promise<void> {
  return apiRequest<void>(API_PATHS.ranked.statsMe, { method: 'DELETE', xsrfToken, signal })
}

export interface RankedCountryOptionDto {
  countryId: string
  name: string
}

export interface AdminRankedClueDto {
  id: string
  label: string
  description: string
  icon: string
  type: 'numeric' | 'categorical' | 'computed'
  comparator: 'higher_lower' | 'exact'
  group: string | null
  unitSymbol: string | null
  source: 'builtin' | 'published'
}

export interface AdminRankedClueOptionDto extends AdminRankedClueDto {
  remoteId: string | null
  ownerUsername: string | null
  categories: string[] | null
}

export interface AdminRankedChallengeEditorDto {
  challengeDateUtc: string
  scope: string
  isPersisted: boolean
  targetCountryId: string
  targetCountryName: string
  selectedClues: AdminRankedClueDto[]
  countries: RankedCountryOptionDto[]
  availableClues: AdminRankedClueOptionDto[]
  canResetSessions: boolean
  canDeleteSchedule: boolean
  sessionsReset: boolean
}

export interface SaveAdminRankedChallengeBody {
  targetCountryId: string
  clueIds: string[]
  resetSessions: boolean
}

export interface DeleteAdminRankedChallengeResult {
  challengeDateUtc: string
  deleted: boolean
  sessionsReset: boolean
}

export async function getAdminRankedChallenge(date: string, signal?: AbortSignal): Promise<AdminRankedChallengeEditorDto> {
  return apiRequest<AdminRankedChallengeEditorDto>(`${API_PATHS.ranked.challengeAdmin}/${date}`, { signal })
}

export async function saveAdminRankedChallenge(date: string, body: SaveAdminRankedChallengeBody, xsrfToken: string, signal?: AbortSignal): Promise<AdminRankedChallengeEditorDto> {
  return apiRequest<AdminRankedChallengeEditorDto>(`${API_PATHS.ranked.challengeAdmin}/${date}`, { method: 'PUT', body, xsrfToken, signal })
}

export async function deleteAdminRankedChallenge(date: string, xsrfToken: string, signal?: AbortSignal): Promise<DeleteAdminRankedChallengeResult> {
  return apiRequest<DeleteAdminRankedChallengeResult>(`${API_PATHS.ranked.challengeAdmin}/${date}`, { method: 'DELETE', xsrfToken, signal })
}

export async function resetRankedLeaderboard(xsrfToken: string, signal?: AbortSignal): Promise<void> {
  return apiRequest<void>(API_PATHS.leaderboards.ranked, { method: 'DELETE', xsrfToken, signal })
}

export type CluePackVisibility = 'public' | 'private'

export interface CluePackRowDto {
  countryId: string
  value: string | number | null
}

export interface CluePackListItemDto {
  id: string
  datasetId: string
  label: string
  description: string
  type: 'numeric' | 'categorical'
  comparator: 'higher_lower' | 'exact'
  unitSymbol: string | null
  icon: string
  ownerId: string
  ownerUsername: string
  visibility: CluePackVisibility
  updatedAtUtc: string
  canEdit: boolean
}

export interface CluePackDetailDto extends CluePackListItemDto {
  categories: string[]
  rows: CluePackRowDto[]
  createdAtUtc: string
}

export interface ListCluePacksResult {
  items: CluePackListItemDto[]
  totalCount: number
  page: number
  pageSize: number
}

export interface UpsertCluePackBody {
  datasetId: string
  label: string
  description: string
  type: 'numeric' | 'categorical'
  comparator: 'higher_lower' | 'exact'
  unitSymbol: string | null
  icon: string
  categories: string[]
  rows: CluePackRowDto[]
  visibility: CluePackVisibility
}

export interface ListCluePacksOptions {
  page?: number
  pageSize?: number
  q?: string
  ownerId?: string
  visibility?: CluePackVisibility
  signal?: AbortSignal
}

export async function listCluePacks(options: ListCluePacksOptions = {}): Promise<ListCluePacksResult> {
  const params = new URLSearchParams()
  params.set('page', String(options.page ?? 1))
  params.set('pageSize', String(options.pageSize ?? APP_LIMITS.cluePackPageSize))
  if (options.q?.trim()) params.set('q', options.q.trim())
  if (options.ownerId) params.set('ownerId', options.ownerId)
  if (options.visibility) params.set('visibility', options.visibility)
  return apiRequest<ListCluePacksResult>(`${API_PATHS.cluePacks.root}?${params.toString()}`, { signal: options.signal })
}

export async function getCluePack(id: string, signal?: AbortSignal): Promise<CluePackDetailDto> {
  return apiRequest<CluePackDetailDto>(`${API_PATHS.cluePacks.root}/${id}`, { signal })
}

export async function createCluePack(body: UpsertCluePackBody, xsrfToken: string, signal?: AbortSignal): Promise<CluePackDetailDto> {
  return apiRequest<CluePackDetailDto>(API_PATHS.cluePacks.root, { method: 'POST', body, xsrfToken, signal })
}

export async function updateCluePack(id: string, body: UpsertCluePackBody, xsrfToken: string, signal?: AbortSignal): Promise<CluePackDetailDto> {
  return apiRequest<CluePackDetailDto>(`${API_PATHS.cluePacks.root}/${id}`, { method: 'PUT', body, xsrfToken, signal })
}

export async function deleteCluePack(id: string, xsrfToken: string, signal?: AbortSignal): Promise<void> {
  return apiRequest<void>(`${API_PATHS.cluePacks.root}/${id}`, { method: 'DELETE', xsrfToken, signal })
}
