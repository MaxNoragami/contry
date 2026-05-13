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

  if (typeof window !== 'undefined' && window.location.hostname === 'localhost') {
    return 'http://localhost:5087'
  }

  return 'https://api.contry.app'
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
