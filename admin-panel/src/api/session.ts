import type { AuthResponse } from './types'

// Oturum (token'lar + kullanici) localStorage'da tutulur: sayfa yenilenince oturum kapanmasin.
// Not: XSS'e karsi daha guvenlisi refresh token'i httpOnly cookie'de tutmaktir - Faz 2'de
// backend cookie destegi eklenirse buraya tasinacak.

const STORAGE_KEY = 'stayflow.session'

type Listener = (session: AuthResponse | null) => void
const listeners = new Set<Listener>()

export function getSession(): AuthResponse | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? (JSON.parse(raw) as AuthResponse) : null
  } catch {
    return null
  }
}

export function setSession(session: AuthResponse | null) {
  try {
    if (session) localStorage.setItem(STORAGE_KEY, JSON.stringify(session))
    else localStorage.removeItem(STORAGE_KEY)
  } catch {
    // localStorage kapali olabilir (gizli mod vb.) - oturum sadece bu sekmede yasar
  }
  listeners.forEach((listener) => listener(session))
}

/** AuthContext, API istemcisinin oturumu kapatmasini (refresh basarisiz) buradan duyar. */
export function onSessionChange(listener: Listener) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}
