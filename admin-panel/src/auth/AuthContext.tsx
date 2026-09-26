import { useQueryClient } from '@tanstack/react-query'
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { authApi, type RegisterInput } from '../api/endpoints'
import { refreshTokens } from '../api/client'
import { getSession, onSessionChange, setSession } from '../api/session'
import type { AuthResponse, User } from '../api/types'
import { PageLoader } from '../components/ui/Spinner'

interface AuthContextValue {
  user: User | null
  isOwner: boolean
  login: (email: string, password: string) => Promise<void>
  register: (input: RegisterInput) => Promise<void>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSessionState] = useState<AuthResponse | null>(getSession)
  // Acilista oturum geri yuklenene kadar sayfa cizilmez: aksi halde bir an login ekrani gorunurdu.
  const [restoring, setRestoring] = useState(() => getSession() === null)
  const queryClient = useQueryClient()

  // API istemcisi refresh'te oturumu yenilerse ya da kapatirsa React state'i de guncellensin.
  useEffect(() => onSessionChange(setSessionState), [])

  // Sayfa yenilendi: access token bellekte yok ama refresh cookie varsa oturum devam eder.
  useEffect(() => {
    if (!restoring) return
    void refreshTokens().finally(() => setRestoring(false))
  }, [restoring])

  const login = useCallback(async (email: string, password: string) => {
    setSession(await authApi.login(email, password))
  }, [])

  const register = useCallback(async (input: RegisterInput) => {
    setSession(await authApi.register(input))
  }, [])

  const logout = useCallback(async () => {
    try {
      await authApi.logout()
    } catch {
      // token zaten gecersiz olabilir - yine de yerelde oturumu kapat
    }
    setSession(null)
    // Bir sonraki kullanici (baska otel olabilir) onceki otelin verisini onbellekte gormesin.
    queryClient.clear()
  }, [queryClient])

  const value = useMemo<AuthContextValue>(() => {
    const user = session?.user ?? null
    return { user, isOwner: user?.role === 'Owner', login, register, logout }
  }, [session, login, register, logout])

  if (restoring) return <PageLoader />
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// Hook, provider ile ayni dosyada durmali (context disari acilmiyor) - HMR uyarisi bilerek kapatildi.
// oxlint-disable-next-line react/only-export-components
export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth, AuthProvider icinde kullanilmali')
  return context
}
