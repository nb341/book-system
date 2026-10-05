import type { ProblemDetails } from './types.ts'

const TOKEN_KEY = 'booking.jwt'

// Only the JWT is ever persisted.
export function getStoredToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function storeToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token)
}

export function clearStoredToken(): void {
  localStorage.removeItem(TOKEN_KEY)
}

export class ApiError extends Error {
  status: number
  title: string
  detail?: string
  fieldErrors: Record<string, string[]>

  constructor(status: number, title: string, detail?: string, fieldErrors: Record<string, string[]> = {}) {
    super(detail || title)
    this.name = 'ApiError'
    this.status = status
    this.title = title
    this.detail = detail
    this.fieldErrors = fieldErrors
  }
}

let unauthorizedHandler: (() => void) | null = null

/** Called when an authenticated request gets 401 (expired/invalid token). */
export function setUnauthorizedHandler(handler: (() => void) | null): void {
  unauthorizedHandler = handler
}

async function toApiError(response: Response): Promise<ApiError> {
  let problem: Partial<ProblemDetails> | null = null
  try {
    problem = await response.json()
  } catch {
    // Non-JSON body (e.g. proxy error page); fall through to defaults.
  }
  const title = problem?.title || response.statusText || `Request failed (${response.status})`
  return new ApiError(response.status, title, problem?.detail, problem?.errors)
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'DELETE'
  body?: unknown
  headers?: Record<string, string>
  signal?: AbortSignal
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, headers = {}, signal } = options
  const token = getStoredToken()

  const requestHeaders: Record<string, string> = { Accept: 'application/json', ...headers }
  if (body !== undefined) requestHeaders['Content-Type'] = 'application/json'
  if (token) requestHeaders.Authorization = `Bearer ${token}`

  let response: Response
  try {
    response = await fetch(path, {
      method,
      headers: requestHeaders,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new ApiError(0, 'Network error', 'Could not reach the server. Check your connection and try again.')
  }

  if (!response.ok) {
    if (response.status === 401 && token) unauthorizedHandler?.()
    throw await toApiError(response)
  }

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

/** Field-level messages for a form input, matching keys case-insensitively. */
export function fieldMessages(error: ApiError | null, field: string): string[] {
  if (!error) return []
  const key = Object.keys(error.fieldErrors).find((k) => k.toLowerCase() === field.toLowerCase())
  return key ? [...error.fieldErrors[key]] : []
}
