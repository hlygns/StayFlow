import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AuthProvider } from '../../auth/AuthContext'
import { LoginPage } from './LoginPage'

function renderLogin() {
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <MemoryRouter initialEntries={['/login']}>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/" element={<p>Panel ana sayfası</p>} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
})

/** Acilistaki oturum geri yukleme istegi (/auth/refresh) cookie olmadigi icin 401 alir. */
function mockFetch(handler: (url: string) => Response) {
  const fetchMock = vi.fn(async (url: string) =>
    url.endsWith('/auth/refresh') ? new Response('{}', { status: 401 }) : handler(url),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('LoginPage', () => {
  it('bos formda sunucuya gitmeden hata gosterir', async () => {
    const fetchMock = mockFetch(() => new Response('{}'))
    renderLogin()

    await userEvent.click(await screen.findByRole('button', { name: 'Giriş yap' }))

    expect(await screen.findByText('Geçerli bir e-posta girin.')).toBeInTheDocument()
    expect(screen.getByText('Şifre gerekli.')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([url]) => url.endsWith('/auth/login'))).toBe(false)
  })

  it('sunucunun hata mesajini gosterir', async () => {
    mockFetch(() => new Response(JSON.stringify({ status: 401, detail: 'E-posta veya sifre hatali.' }), { status: 401 }))
    renderLogin()

    await userEvent.type(await screen.findByLabelText('E-posta'), 'owner@otel.test')
    await userEvent.type(screen.getByLabelText('Şifre'), 'yanlis-sifre')
    await userEvent.click(screen.getByRole('button', { name: 'Giriş yap' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('E-posta veya sifre hatali.')
  })

  it('basarili giriste panele yonlendirir', async () => {
    const auth = {
      accessToken: 'a',
      accessTokenExpiresAt: '2030-01-01T00:00:00Z',
      user: { id: 'u', fullName: 'Hülya Güneş', email: 'owner@otel.test', role: 'Owner', hotelId: 'h' },
    }
    mockFetch(() => new Response(JSON.stringify(auth), { status: 200 }))
    renderLogin()

    await userEvent.type(await screen.findByLabelText('E-posta'), 'owner@otel.test')
    await userEvent.type(screen.getByLabelText('Şifre'), 'Parola123!')
    await userEvent.click(screen.getByRole('button', { name: 'Giriş yap' }))

    expect(await screen.findByText('Panel ana sayfası')).toBeInTheDocument()
  })
})
