import { useQuery } from '@tanstack/react-query'
import { useState, type ReactNode } from 'react'
import { Link } from 'react-router'
import { reservationsApi, roomsApi } from '../api/endpoints'
import type { Reservation } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { PlusIcon } from '../components/icons'
import { Button } from '../components/ui/Button'
import { Card, ErrorAlert, PageHeader } from '../components/ui/Card'
import { PageLoader } from '../components/ui/Spinner'
import { StatusBadge } from '../components/ui/StatusBadge'
import { addDaysIso, formatDate, today } from '../lib/format'
import { computeDashboardStats } from './dashboard/dashboardStats'
import { OccupancyChart } from './dashboard/OccupancyChart'
import { ReservationDetailModal } from './reservations/ReservationDetailModal'

function StatTile({ label, value, detail, to }: { label: string; value: ReactNode; detail?: string; to?: string }) {
  const body = (
    <Card className="h-full p-5 transition-shadow hover:shadow-md">
      <p className="text-sm text-slate-500">{label}</p>
      <p className="mt-2 text-3xl font-semibold text-slate-900">{value}</p>
      {detail && <p className="mt-1 text-xs text-slate-500">{detail}</p>}
    </Card>
  )
  return to ? <Link to={to} className="block">{body}</Link> : body
}

function GuestList({ title, empty, items, onSelect }: { title: string; empty: string; items: Reservation[]; onSelect: (id: string) => void }) {
  return (
    <Card>
      <h2 className="border-b border-slate-100 px-5 py-3 font-semibold text-slate-900">
        {title} <span className="ml-1 text-sm font-normal text-slate-500">{items.length}</span>
      </h2>
      {items.length === 0 ? (
        <p className="px-5 py-6 text-sm text-slate-500">{empty}</p>
      ) : (
        <ul className="divide-y divide-slate-100">
          {items.map((r) => (
            <li key={r.id}>
              <button type="button" onClick={() => onSelect(r.id)} className="flex w-full items-center gap-3 px-5 py-3 text-left hover:bg-slate-50">
                <span className="w-12 shrink-0 text-lg font-semibold text-slate-900 tabular-nums">{r.roomNumber}</span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate font-medium text-slate-900">{r.guest.fullName}</span>
                  <span className="block text-xs text-slate-500">
                    {r.nights} gece · {r.guestCount} kişi
                  </span>
                </span>
                <StatusBadge status={r.status} />
              </button>
            </li>
          ))}
        </ul>
      )}
    </Card>
  )
}

export function DashboardPage() {
  const { user } = useAuth()
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const todayIso = today()

  const rooms = useQuery({ queryKey: ['rooms', { showInactive: false }], queryFn: () => roomsApi.list(false) })
  // from = dun: bugun cikis yapanlar (checkOut == bugun) da gelsin
  const from = addDaysIso(todayIso, -1)
  const to = addDaysIso(todayIso, 7)
  const reservations = useQuery({ queryKey: ['reservations', { from, to }], queryFn: () => reservationsApi.list({ from, to }) })

  const firstName = user?.fullName.split(' ')[0] ?? ''

  const header = (
    <PageHeader
      title={`Merhaba, ${firstName}`}
      description={`${formatDate(todayIso)} · otelinizin bugünkü durumu`}
      actions={
        <Link to="/reservations/new">
          <Button>
            <PlusIcon className="size-4" />
            Yeni rezervasyon
          </Button>
        </Link>
      }
    />
  )

  if (rooms.isLoading || reservations.isLoading) return <>{header}<PageLoader /></>
  if (rooms.error || reservations.error) return <>{header}<ErrorAlert error={rooms.error ?? reservations.error} /></>

  const roomCount = rooms.data!.length
  if (roomCount === 0) {
    return (
      <>
        {header}
        <Card className="p-6">
          <h2 className="font-semibold text-slate-900">Başlamak için üç adım</h2>
          <ol className="mt-3 list-decimal space-y-1.5 pl-5 text-sm text-slate-600">
            <li>
              <Link to="/room-types" className="font-medium text-brand-700 hover:underline">Oda tiplerini</Link> ve fiyatlarını tanımlayın.
            </li>
            <li>
              <Link to="/rooms" className="font-medium text-brand-700 hover:underline">Odalarınızı</Link> numaralarıyla ekleyin.
            </li>
            <li>İlk rezervasyonunuzu oluşturun; takvimde görünecek.</li>
          </ol>
        </Card>
      </>
    )
  }

  const stats = computeDashboardStats(reservations.data!, roomCount, todayIso)
  const tonight = stats.nights[0]

  return (
    <>
      {header}

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatTile label="Bu gece doluluk" value={`%${Math.round(tonight.rate * 100)}`} detail={`${stats.inHouse} / ${roomCount} oda`} to="/calendar" />
        <StatTile label="Bugün giriş" value={stats.arrivals.length} detail="beklenen misafir" />
        <StatTile label="Bugün çıkış" value={stats.departures.length} detail="ayrılacak misafir" />
        <StatTile label="Ödeme bekleyen" value={stats.pendingPayment} detail="önümüzdeki 7 gün" to="/reservations" />
      </div>

      <Card className="mt-6 p-5">
        <div className="mb-4 flex items-baseline justify-between gap-4">
          <h2 className="font-semibold text-slate-900">Önümüzdeki 7 gece doluluk</h2>
          <Link to="/calendar" className="text-sm font-medium text-brand-700 hover:underline">
            Takvimi aç →
          </Link>
        </div>
        <OccupancyChart nights={stats.nights} roomCount={roomCount} />
      </Card>

      <div className="mt-6 grid gap-6 lg:grid-cols-2">
        <GuestList title="Bugün gelecekler" empty="Bugün giriş yapacak misafir yok." items={stats.arrivals} onSelect={setSelectedId} />
        <GuestList title="Bugün ayrılacaklar" empty="Bugün çıkış yapacak misafir yok." items={stats.departures} onSelect={setSelectedId} />
      </div>

      {selectedId && <ReservationDetailModal reservationId={selectedId} onClose={() => setSelectedId(null)} />}
    </>
  )
}
