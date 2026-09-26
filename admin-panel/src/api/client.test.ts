import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, ApiError } from './client'
import { getSession, setSession } from './session'
import type { AuthResponse } from './types'

const session = (token: string): AuthResponse => ({
  accessToken: token,
  accessTokenExpiresAt: '2030-01-01T00:00:00Z',
  refreshToken: `refresh-${token}`,
  user: { id: 'u1', fullName: 'Test', email: 't@test.com', role: 'Owner', hotelId: 'h1' },
})

const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('api client', () => {
  it('ProblemDetails.detail mesajini ApiError olarak firlatir', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => json(409, { title: 'Cakisma', detail: 'Secilen oda bu tarihlerde musait degil.' })))

    const error = await api.post('/reservations', {}).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(409)
    expect((error as ApiError).message).toBe('Secilen oda bu tarihlerde musait degil.')
  })

  it('ayni anda 401 alan istekler icin TEK refresh yapar, sonra hepsini yeni token ile tekrarlar', async () => {
    setSession(session('old'))
    const calls: string[] = []

    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init: RequestInit) => {
        const auth = (init.headers as Record<string, string>).Authorization
        calls.push(`${url} ${auth ?? ''}`)
        if (url.endsWith('/auth/refresh')) {
          await new Promise((resolve) => setTimeout(resolve, 10)) // refresh suruyorken diger istekler de 401 alsin
          return json(200, session('new'))
        }
        return auth === 'Bearer new' ? json(200, { ok: true }) : json(401, {})
      }),
    )

    const results = await Promise.all([api.get('/rooms'), api.get('/room-types'), api.get('/hotel')])

    expect(results).toEqual([{ ok: true }, { ok: true }, { ok: true }])
    expect(calls.filter((c) => c.includes('/auth/refresh'))).toHaveLength(1)
    expect(getSession()?.accessToken).toBe('new')
  })

  it('refresh basarisizsa oturumu kapatir ve 401 hatasi verir', async () => {
    setSession(session('old'))
    vi.stubGlobal('fetch', vi.fn(async () => json(401, {})))

    const error = await api.get('/rooms').catch((e: unknown) => e)

    expect((error as ApiError).status).toBe(401)
    expect(getSession()).toBeNull()
  })

  it('ag hatasini anlasilir mesaja cevirir', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => Promise.reject(new TypeError('Failed to fetch'))))

    const error = await api.get('/rooms').catch((e: unknown) => e)

    expect((error as ApiError).status).toBe(0)
    expect((error as ApiError).message).toMatch(/Sunucuya ulaşılamadı/)
  })
})
