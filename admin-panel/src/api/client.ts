import { getSession, setSession } from './session'
import type { AuthResponse, ProblemDetails } from './types'

const BASE_URL = import.meta.env.VITE_API_URL ?? '/api'

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails

  constructor(status: number, problem: ProblemDetails) {
    super(ApiError.messageFrom(status, problem))
    this.status = status
    this.problem = problem
  }

  /** Kullaniciya gosterilecek tek satirlik mesaj. */
  private static messageFrom(status: number, problem: ProblemDetails): string {
    if (problem.detail) return problem.detail
    if (problem.errors) {
      const first = Object.values(problem.errors).flat()[0]
      if (first) return first
    }
    if (status === 403) return 'Bu işlem için yetkiniz yok.'
    if (status === 0) return 'Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edin.'
    return problem.title ?? 'Beklenmeyen bir hata oluştu.'
  }
}

// Ayni anda 5 istek 401 alirsa 5 kere refresh yapmamak icin: ilk istek refresh'i baslatir,
// digerleri ayni Promise'i bekler ("single flight"). Backend refresh token'i her kullanimda
// yeniledigi icin (rotation) paralel iki refresh, ikincisinin reddedilmesi demek olurdu.
let refreshInFlight: Promise<boolean> | null = null

/**
 * Cookie'deki refresh token ile yeni bir access token alir. Hem 401 sonrasinda hem de
 * sayfa ilk acildiginda (oturumu geri yuklemek icin) kullanilir.
 */
export async function refreshTokens(): Promise<boolean> {
  refreshInFlight ??= (async () => {
    try {
      // Govde yok: tarayici httpOnly refresh cookie'sini kendisi ekler (ayni origin).
      const response = await fetch(`${BASE_URL}/auth/refresh`, { method: 'POST', credentials: 'same-origin' })
      if (!response.ok) {
        setSession(null)
        return false
      }
      setSession((await response.json()) as AuthResponse)
      return true
    } catch {
      return false
    } finally {
      refreshInFlight = null
    }
  })()

  return refreshInFlight
}

type Method = 'GET' | 'POST' | 'PUT' | 'DELETE'

async function request<T>(method: Method, path: string, body?: unknown, retry = true): Promise<T> {
  const session = getSession()
  const headers: Record<string, string> = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (session) headers.Authorization = `Bearer ${session.accessToken}`

  let response: Response
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, {})
  }

  // Access token suresi dolduysa bir kere yenileyip ayni istegi tekrar dene.
  if (response.status === 401 && session && retry && !path.startsWith('/auth/')) {
    if (await refreshTokens()) return request<T>(method, path, body, false)
  }

  if (!response.ok) {
    let problem: ProblemDetails = {}
    try {
      problem = (await response.json()) as ProblemDetails
    } catch {
      // govdesiz hata (ör. 403) - bos ProblemDetails yeterli
    }
    throw new ApiError(response.status, problem)
  }

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

export const api = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body),
  put: <T>(path: string, body: unknown) => request<T>('PUT', path, body),
  delete: (path: string) => request<void>('DELETE', path),
}

/** Query string olustururken bos degerleri atla. */
export function qs(params: Record<string, string | number | undefined | null>) {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') search.set(key, String(value))
  }
  const text = search.toString()
  return text ? `?${text}` : ''
}
