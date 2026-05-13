import { ApiError, apiRequest, type ProblemDetailsResponse } from '../api/client'

export type AuthStatus = 'loading' | 'anonymous' | 'authenticated'

export type AuthUser = {
  id: string
  username: string
  email: string
  role: string
}

export type LogoutReason = 'manual' | 'expired' | null

type AuthSessionResponse = {
  user: AuthUser
  accessTokenExpiresAtUtc: string
  refreshTokenExpiresAtUtc: string
}

type XsrfTokenResponse = {
  token: string
}

type AuthRequestOptions = {
  method?: string
  body?: unknown
  headers?: HeadersInit
  signal?: AbortSignal
  retryUnauthorized?: boolean
}

export function createAuthStore() {
  let status = $state<AuthStatus>('loading')
  let user = $state<AuthUser | null>(null)
  let xsrfToken = $state<string | null>(null)
  let initialized = $state(false)
  let refreshPromise: Promise<boolean> | null = null
  let lastLogoutReason = $state<LogoutReason>(null)

  async function init() {
    if (initialized) return
    initialized = true
    await hydrateCurrentUser(false)
  }

  async function hydrateCurrentUser(allowRefresh = true) {
    status = 'loading'

    try {
      const currentUser = await request<AuthUser>('/users/me', { retryUnauthorized: allowRefresh })
      user = currentUser
      status = 'authenticated'
      lastLogoutReason = null
      return currentUser
    } catch (error) {
      user = null
      xsrfToken = null
      status = 'anonymous'
      if (error instanceof ApiError && error.status === 401) {
        return null
      }
      throw error
    }
  }

  async function login(credential: string, password: string) {
    const session = await apiRequest<AuthSessionResponse>('/sessions', {
      method: 'POST',
      body: { credential, password },
    })

    user = session.user
    status = 'authenticated'
    await fetchXsrfToken()
    return session.user
  }

  async function register(username: string, email: string, password: string) {
    const session = await apiRequest<AuthSessionResponse>('/users', {
      method: 'POST',
      body: { username, email, password },
    })

    user = session.user
    status = 'authenticated'
    await fetchXsrfToken()
    return session.user
  }

  async function logout() {
    try {
      await request<void>('/sessions/current', { method: 'DELETE', retryUnauthorized: false })
    } finally {
      clearSessionState('manual')
    }
  }

  async function fetchXsrfToken() {
    const response = await apiRequest<XsrfTokenResponse>('/xsrf')
    xsrfToken = response.token
    return xsrfToken
  }

  async function ensureXsrfToken() {
    if (xsrfToken) {
      return xsrfToken
    }

    try {
      return await fetchXsrfToken()
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        clearSessionState('expired')
        return null
      }
      throw error
    }
  }

  async function refreshSession(): Promise<boolean> {
    if (refreshPromise) {
      return refreshPromise
    }

    refreshPromise = (async () => {
      try {
        const token = await ensureXsrfToken()
        if (!token) {
          clearSessionState('expired')
          return false
        }

        const session = await apiRequest<AuthSessionResponse>('/tokens/refresh', {
          method: 'POST',
          xsrfToken: token,
        })

        user = session.user
        status = 'authenticated'
        lastLogoutReason = null
        return true
      } catch {
        clearSessionState('expired')
        return false
      } finally {
        refreshPromise = null
      }
    })()

    return refreshPromise
  }

  async function request<T>(path: string, options: AuthRequestOptions = {}): Promise<T> {
    const method = (options.method || 'GET').toUpperCase()
    const unsafe = ['POST', 'PUT', 'PATCH', 'DELETE'].includes(method)

    try {
      const xsrfToken = unsafe ? await ensureXsrfToken() : null
      if (unsafe && !xsrfToken) {
        throw new ApiError(401, { detail: 'Your session expired. Please log in again.' }, 'Your session expired. Please log in again.')
      }

      return await apiRequest<T>(path, {
        ...options,
        method,
        xsrfToken,
      })
    } catch (error) {
      if (!(error instanceof ApiError) || error.status !== 401 || options.retryUnauthorized === false) {
        throw error
      }

      const refreshed = await refreshSession()
      if (!refreshed) {
        throw error
      }

      return apiRequest<T>(path, {
        ...options,
        method,
        xsrfToken: unsafe ? xsrfToken : null,
      })
    }
  }

  function clearSessionState(reason: LogoutReason) {
    user = null
    xsrfToken = null
    status = 'anonymous'
    lastLogoutReason = reason
  }

  return {
    get status() { return status },
    get user() { return user },
    get xsrfToken() { return xsrfToken },
    get lastLogoutReason() { return lastLogoutReason },
    get isAuthenticated() { return status === 'authenticated' && user !== null },
    init,
    login,
    register,
    logout,
    request,
    fetchXsrfToken,
    hydrateCurrentUser,
  }
}

export function getProblemMessage(error: unknown): string {
  if (error instanceof ApiError) {
    const problem = error.problem as ProblemDetailsResponse | null
    if (problem?.errors) {
      const firstError = Object.values(problem.errors)[0]?.[0]
      if (firstError) return firstError
    }
    return problem?.detail || problem?.title || error.message
  }

  if (error instanceof Error) {
    return error.message
  }

  return 'Something went wrong.'
}

export function getProblemFieldErrors(error: unknown): Record<string, string[]> {
  if (!(error instanceof ApiError) || !error.problem?.errors) {
    return {}
  }

  return Object.fromEntries(
    Object.entries(error.problem.errors).map(([key, value]) => [key.toLowerCase(), value]),
  )
}
