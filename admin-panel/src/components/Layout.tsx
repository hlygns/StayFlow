import { useQuery } from '@tanstack/react-query'
import clsx from 'clsx'
import { useState, type ComponentType, type SVGProps } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router'
import { hotelApi } from '../api/endpoints'
import { useAuth } from '../auth/AuthContext'
import { useReservationEvents, type LiveStatus } from '../realtime/useReservationEvents'
import { BedIcon, CalendarIcon, CogIcon, HomeIcon, ListIcon, LogoutIcon, MenuIcon, StarIcon, TagIcon, UsersIcon } from './icons'

interface NavItem {
  to: string
  label: string
  icon: ComponentType<SVGProps<SVGSVGElement>>
  ownerOnly?: boolean
}

const navItems: NavItem[] = [
  { to: '/', label: 'Genel Bakış', icon: HomeIcon },
  { to: '/calendar', label: 'Takvim', icon: CalendarIcon },
  { to: '/reservations', label: 'Rezervasyonlar', icon: ListIcon },
  { to: '/reviews', label: 'Yorumlar', icon: StarIcon },
  { to: '/room-types', label: 'Oda Tipleri ve Fiyatlar', icon: TagIcon },
  { to: '/rooms', label: 'Odalar', icon: BedIcon },
  { to: '/staff', label: 'Personel', icon: UsersIcon, ownerOnly: true },
  { to: '/settings', label: 'Otel Ayarları', icon: CogIcon, ownerOnly: true },
]

export function Layout() {
  const { user, isOwner, logout } = useAuth()
  const [menuOpen, setMenuOpen] = useState(false)
  const location = useLocation()
  const { data: hotel } = useQuery({ queryKey: ['hotel'], queryFn: hotelApi.get })
  const live = useReservationEvents()

  // Mobilde bir sayfaya gecince menu kapansin (render sirasinda: ek effect gerekmez).
  const [lastPath, setLastPath] = useState(location.pathname)
  if (lastPath !== location.pathname) {
    setLastPath(location.pathname)
    setMenuOpen(false)
  }

  const sidebar = (
    <nav className="flex h-full flex-col bg-brand-900 text-brand-100">
      <div className="px-5 pt-6 pb-5">
        <p className="text-lg font-semibold tracking-tight text-white">StayFlow</p>
        <p className="mt-0.5 truncate text-sm text-brand-200/80">{hotel?.name ?? ' '}</p>
        <LiveIndicator status={live} />
      </div>
      <ul className="flex-1 space-y-0.5 px-3">
        {navItems
          .filter((item) => isOwner || !item.ownerOnly)
          .map(({ to, label, icon: Icon }) => (
            <li key={to}>
              <NavLink
                to={to}
                end={to === '/'}
                className={({ isActive }) =>
                  clsx(
                    'flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
                    isActive ? 'bg-white/10 text-white' : 'text-brand-100/80 hover:bg-white/5 hover:text-white',
                  )
                }
              >
                <Icon className="size-5 shrink-0" />
                {label}
              </NavLink>
            </li>
          ))}
      </ul>
      <div className="border-t border-white/10 p-3">
        <div className="px-3 py-2">
          <p className="truncate text-sm font-medium text-white">{user?.fullName}</p>
          <p className="text-xs text-brand-200/80">{user?.role === 'Owner' ? 'Otel sahibi' : 'Resepsiyon'}</p>
        </div>
        <button
          type="button"
          onClick={logout}
          className="flex w-full items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium text-brand-100/80 hover:bg-white/5 hover:text-white"
        >
          <LogoutIcon className="size-5" />
          Çıkış yap
        </button>
      </div>
    </nav>
  )

  return (
    <div className="min-h-dvh lg:pl-64">
      {/* Masaustu: sabit yan menu */}
      <aside className="fixed inset-y-0 left-0 hidden w-64 lg:block">{sidebar}</aside>

      {/* Mobil: ust bar + acilir menu */}
      <header className="sticky top-0 z-30 flex items-center gap-3 bg-brand-900 px-4 py-3 text-white lg:hidden">
        <button type="button" onClick={() => setMenuOpen(true)} aria-label="Menüyü aç" className="-m-1 rounded-md p-1">
          <MenuIcon className="size-6" />
        </button>
        <span className="font-semibold">StayFlow</span>
      </header>
      {menuOpen && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <div className="absolute inset-0 bg-slate-900/50" onClick={() => setMenuOpen(false)} />
          <aside className="absolute inset-y-0 left-0 w-72 max-w-[85%]">{sidebar}</aside>
        </div>
      )}

      <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
        <Outlet />
      </main>
    </div>
  )
}

const liveLabels: Record<LiveStatus, string> = {
  live: 'Canlı: değişiklikler anında görünür',
  connecting: 'Bağlanıyor…',
  offline: 'Canlı bağlantı yok, sayfayı yenileyin',
}

function LiveIndicator({ status }: { status: LiveStatus }) {
  return (
    <p className="mt-3 flex items-center gap-2 text-xs text-brand-100/70" role="status">
      <span
        className={clsx(
          'size-2 rounded-full',
          status === 'live' ? 'bg-emerald-400' : status === 'connecting' ? 'animate-pulse bg-amber-300' : 'bg-red-400',
        )}
        aria-hidden="true"
      />
      {liveLabels[status]}
    </p>
  )
}
