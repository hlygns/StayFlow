import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router'
import { useAuth } from './AuthContext'

/** Giris yapmamis kullaniciyi login'e yollar; girdikten sonra geldigi sayfaya donebilsin diye adresi saklar. */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const location = useLocation()

  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  return children
}

/** Sadece otel sahibinin gorebilecegi sayfalar. Asil kontrol backend'de; bu sadece arayuz. */
export function RequireOwner({ children }: { children: ReactNode }) {
  const { isOwner } = useAuth()
  if (!isOwner) return <Navigate to="/" replace />
  return children
}
