import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { reservationsApi } from '../../api/endpoints'
import type { ReservationStatus } from '../../api/types'
import { PlusIcon } from '../../components/icons'
import { Button } from '../../components/ui/Button'
import { Card, EmptyState, ErrorAlert, PageHeader } from '../../components/ui/Card'
import { Input, Select } from '../../components/ui/Field'
import { PageLoader } from '../../components/ui/Spinner'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { addDaysIso, formatMoney, formatRange, statusLabels, today } from '../../lib/format'
import { ReservationDetailModal } from './ReservationDetailModal'

const statuses = Object.keys(statusLabels) as ReservationStatus[]

export function ReservationsPage() {
  const [from, setFrom] = useState(() => addDaysIso(today(), -7))
  const [to, setTo] = useState(() => addDaysIso(today(), 60))
  const [status, setStatus] = useState<ReservationStatus | ''>('')
  const [search, setSearch] = useState('')
  const [selectedId, setSelectedId] = useState<string | null>(null)

  const { data, isLoading, error } = useQuery({
    queryKey: ['reservations', { from, to, status }],
    queryFn: () => reservationsApi.list({ from, to, status: status || undefined }),
    placeholderData: (previous) => previous,
  })

  // Isim/oda aramasi istemci tarafinda: liste zaten tarih araligiyla sinirli.
  const term = search.trim().toLocaleLowerCase('tr')
  const reservations = (data ?? []).filter(
    (r) =>
      !term ||
      r.guest.fullName.toLocaleLowerCase('tr').includes(term) ||
      r.roomNumber.includes(term) ||
      (r.confirmationCode?.toLocaleLowerCase('tr').includes(term) ?? false),
  )

  return (
    <>
      <PageHeader
        title="Rezervasyonlar"
        description="Seçilen tarih aralığına denk gelen tüm rezervasyonlar."
        actions={
          <Link to="/reservations/new">
            <Button>
              <PlusIcon className="size-4" />
              Yeni rezervasyon
            </Button>
          </Link>
        }
      />

      <Card className="mb-4 grid gap-4 p-4 sm:grid-cols-2 lg:grid-cols-4">
        <Input label="Başlangıç" type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        <Input label="Bitiş" type="date" value={to} onChange={(e) => setTo(e.target.value)} />
        <Select label="Durum" value={status} onChange={(e) => setStatus(e.target.value as ReservationStatus | '')}>
          <option value="">Tümü</option>
          {statuses.map((s) => (
            <option key={s} value={s}>
              {statusLabels[s]}
            </option>
          ))}
        </Select>
        <Input label="Ara" type="search" placeholder="Misafir, oda no veya rezervasyon kodu" value={search} onChange={(e) => setSearch(e.target.value)} />
      </Card>

      {isLoading ? (
        <PageLoader />
      ) : error ? (
        <ErrorAlert error={error} />
      ) : reservations.length === 0 ? (
        <Card>
          <EmptyState title="Rezervasyon bulunamadı" description="Filtreleri değiştirmeyi deneyin." />
        </Card>
      ) : (
        <Card className="overflow-hidden">
          {/* Masaustu: tablo */}
          <table className="hidden w-full text-sm md:table">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500">
              <tr>
                <th className="px-4 py-3">Misafir</th>
                <th className="px-4 py-3">Oda</th>
                <th className="px-4 py-3">Tarihler</th>
                <th className="px-4 py-3 text-right">Tutar</th>
                <th className="px-4 py-3">Durum</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {reservations.map((r) => (
                <tr key={r.id} onClick={() => setSelectedId(r.id)} className="cursor-pointer hover:bg-slate-50">
                  <td className="px-4 py-3">
                    <button type="button" className="text-left font-medium text-slate-900 hover:underline" onClick={() => setSelectedId(r.id)}>
                      {r.guest.fullName}
                    </button>
                    <p className="text-xs text-slate-500">{r.guestCount} kişi</p>
                  </td>
                  <td className="px-4 py-3">
                    <span className="font-medium tabular-nums">{r.roomNumber}</span>
                    <p className="text-xs text-slate-500">{r.roomTypeName}</p>
                  </td>
                  <td className="px-4 py-3">
                    {formatRange(r.checkIn, r.checkOut)}
                    <p className="text-xs text-slate-500">{r.nights} gece</p>
                  </td>
                  <td className="px-4 py-3 text-right font-medium tabular-nums">{formatMoney(r.totalPrice)}</td>
                  <td className="px-4 py-3">
                    <StatusBadge status={r.status} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {/* Mobil: kart listesi */}
          <ul className="divide-y divide-slate-100 md:hidden">
            {reservations.map((r) => (
              <li key={r.id}>
                <button type="button" onClick={() => setSelectedId(r.id)} className="w-full px-4 py-3 text-left">
                  <div className="flex items-start justify-between gap-3">
                    <p className="font-medium text-slate-900">{r.guest.fullName}</p>
                    <StatusBadge status={r.status} />
                  </div>
                  <p className="mt-1 text-sm text-slate-500">
                    Oda {r.roomNumber} · {formatRange(r.checkIn, r.checkOut)}
                  </p>
                  <p className="mt-1 text-sm font-medium tabular-nums">{formatMoney(r.totalPrice)}</p>
                </button>
              </li>
            ))}
          </ul>
        </Card>
      )}

      {selectedId && <ReservationDetailModal reservationId={selectedId} onClose={() => setSelectedId(null)} />}
    </>
  )
}
