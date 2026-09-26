import type { AuthResponse } from './types'

// Oturum (access token + kullanici) SADECE bellekte tutulur - localStorage'a yazilmaz.
// Refresh token ise backend'in koydugu httpOnly cookie'de: JavaScript ona hic erisemez.
// Boylece sayfada bir XSS acigi olsa bile uzun omurlu bir token calinamaz. Bedeli: sayfa
// yenilenince access token kaybolur; AuthProvider acilista cookie ile sessizce yenisini alir.

type Listener = (session: AuthResponse | null) => void

let current: AuthResponse | null = null
const listeners = new Set<Listener>()

export function getSession(): AuthResponse | null {
  return current
}

export function setSession(session: AuthResponse | null) {
  current = session
  listeners.forEach((listener) => listener(session))
}

/** AuthContext, API istemcisinin oturumu yenilemesini ya da kapatmasini buradan duyar. */
export function onSessionChange(listener: Listener) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}
