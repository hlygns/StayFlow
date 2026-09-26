import { useQuery } from '@tanstack/react-query'
import clsx from 'clsx'
import { format, isWeekend, parseISO } from 'date-fns'
import { tr } from 'date-fns/locale'
import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { reservationsApi, roomsApi } from '../../api/endpoints'
import type { Room } from '../../api/types'
import { ChevronLeftIcon, ChevronRightIcon, PlusIcon } from '../../components/icons'
import { Button } from '../../components/ui/Button'
import { Card, EmptyState, ErrorAlert, PageHeader } from '../../components/ui/Card'
import { PageLoader } from '../../components/ui/Spinner'
import { addDaysIso, formatDate, statusLabels, statusStyles, today } from '../../lib/format'
import { ReservationDetailModal } from '../reservations/ReservationDetailModal'
import { layoutBlocks, occupancyRate } from './calendarLayout'

const DAYS = 14

export function CalendarPage() {
  const navigate = useNavigate()
  const [start, setStart] = useState(() => addDaysIso(today(), -1))
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const end = addDaysIso(start, DAYS)
  const todayIso = today()

  const roomsQuery = useQuery({ queryKey: ['rooms', { showInactive: false }], queryFn: () => roomsApi.list(false) })
  const reservationsQuery = useQuery({
    queryKey: ['reservations', { from: start, to: end }],
    queryFn: () => reservationsApi.list({ from: start, to: end }),
    placeholderData: (previous) => previous, // hafta degistirirken tablo bosalip titremesin
  })

  const days = useMemo(() => Array.from({ length: DAYS }, (_, i) => addDaysIso(start, i)), [start])
  const blocks = useMemo(() => layoutBlocks(reservationsQuery.data ?? [], start, DAYS), [reservationsQuery.data, start])

  // Odalari tiplerine gore grupla: "Standart" satirlari bir arada, "Suit" satirlari bir arada.
  const groups = useMemo(() => {
    const map = new Map<string, Room[]>()
    for (const room of roomsQuery.data ?? []) map.set(room.roomTypeName, [...(map.get(room.roomTypeName) ?? []), room])
    return [...map.entries()]
  }, [roomsQuery.data])

  const rooms = roomsQuery.data ?? []
  const occupancy = occupancyRate(blocks, rooms.length, DAYS)
  const gridColumns = { gridTemplateColumns: `repeat(${DAYS}, minmax(3.25rem, 1fr))` }

  return (
    <>
      <PageHeader
        title="Rezervasyon takvimi"
        description="Boş bir hücreye tıklayarak o oda ve tarih için rezervasyon oluşturun."
        actions={
          <Link to="/reservations/new">
            <Button>
              <PlusIcon className="size-4" />
              Yeni rezervasyon
            </Button>
          </Link>
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <div className="flex items-center rounded-lg bg-white ring-1 ring-slate-300">
          <button type="button" onClick={() => setStart(addDaysIso(start, -7))} className="rounded-l-lg p-2 hover:bg-slate-50" aria-label="Önceki hafta">
            <ChevronLeftIcon className="size-4" />
          </button>
          <button type="button" onClick={() => setStart(addDaysIso(todayIso, -1))} className="border-x border-slate-200 px-3 py-1.5 text-sm font-medium hover:bg-slate-50">
            Bugün
          </button>
          <button type="button" onClick={() => setStart(addDaysIso(start, 7))} className="rounded-r-lg p-2 hover:bg-slate-50" aria-label="Sonraki hafta">
            <ChevronRightIcon className="size-4" />
          </button>
        </div>
        <p className="text-sm font-medium text-slate-700">
          {formatDate(start)} – {formatDate(addDaysIso(end, -1))}
        </p>
        <p className="text-sm text-slate-500 sm:ml-auto">
          Bu aralıkta doluluk: <span className="font-semibold text-slate-900 tabular-nums">%{Math.round(occupancy * 100)}</span>
        </p>
      </div>

      {roomsQuery.isLoading || reservationsQuery.isLoading ? (
        <PageLoader />
      ) : roomsQuery.error || reservationsQuery.error ? (
        <ErrorAlert error={roomsQuery.error ?? reservationsQuery.error} />
      ) : rooms.length === 0 ? (
        <Card>
          <EmptyState
            title="Takvimde gösterilecek oda yok"
            description="Önce oda tiplerini ve odaları ekleyin."
            action={
              <Link to="/rooms" className="text-sm font-semibold text-brand-700 hover:underline">
                Odalara git →
              </Link>
            }
          />
        </Card>
      ) : (
        <Card className="overflow-hidden">
          {/* Tablo yatayda kendi icinde kayar; oda sutunu sabit (sticky) kalir. */}
          <div className="overflow-x-auto">
            <div className="grid min-w-max grid-cols-[6.5rem_1fr]">
              {/* Baslik satiri */}
              <div className="sticky left-0 z-20 border-b border-r border-slate-200 bg-white" />
              <div className="grid border-b border-slate-200" style={gridColumns}>
                {days.map((day) => {
                  const date = parseISO(day)
                  return (
                    <div
                      key={day}
                      className={clsx(
                        'border-l border-slate-100 px-1 py-2 text-center first:border-l-0',
                        isWeekend(date) && 'bg-slate-50',
                        day === todayIso && 'bg-brand-50',
                      )}
                    >
                      <p className={clsx('text-[11px] uppercase', day === todayIso ? 'font-semibold text-brand-700' : 'text-slate-500')}>
                        {format(date, 'EEE', { locale: tr })}
                      </p>
                      <p className={clsx('text-sm tabular-nums', day === todayIso ? 'font-semibold text-brand-700' : 'text-slate-900')}>
                        {format(date, 'd MMM', { locale: tr })}
                      </p>
                    </div>
                  )
                })}
              </div>

              {groups.map(([typeName, typeRooms]) => (
                <div key={typeName} className="contents">
                  <div className="sticky left-0 z-10 col-span-2 border-b border-slate-200 bg-slate-50 px-3 py-1.5 text-xs font-semibold text-slate-600">
                    {typeName}
                  </div>
                  {typeRooms.map((room) => (
                    <div key={room.id} className="contents">
                      <div className="sticky left-0 z-10 flex items-center border-b border-r border-slate-200 bg-white px-3 text-sm font-semibold text-slate-900 tabular-nums">
                        {room.roomNumber}
                      </div>
                      <div className="grid h-12 border-b border-slate-100" style={gridColumns}>
                        {/* Arka plan hucreleri: tiklayinca o oda + tarih icin yeni rezervasyon */}
                        {days.map((day, i) => (
                          <button
                            key={day}
                            type="button"
                            style={{ gridRow: 1, gridColumn: i + 1 }}
                            disabled={day < todayIso}
                            onClick={() => navigate(`/reservations/new?roomId=${room.id}&checkIn=${day}`)}
                            aria-label={`Oda ${room.roomNumber}, ${formatDate(day)} için rezervasyon oluştur`}
                            className={clsx(
                              'border-l border-slate-100 first:border-l-0 enabled:hover:bg-brand-50',
                              isWeekend(parseISO(day)) && 'bg-slate-50/70',
                              day === todayIso && 'bg-brand-50/60',
                            )}
                          />
                        ))}
                        {(blocks.get(room.id) ?? []).map(({ reservation, startColumn, span, continuesBefore, continuesAfter }) => (
                          <button
                            key={reservation.id}
                            type="button"
                            onClick={() => setSelectedId(reservation.id)}
                            style={{ gridRow: 1, gridColumn: `${startColumn + 1} / span ${span}` }}
                            title={`${reservation.guest.fullName} · ${statusLabels[reservation.status]}`}
                            className={clsx(
                              'z-[1] my-1.5 mx-0.5 truncate px-2 text-left text-xs font-medium ring-1 ring-inset transition-shadow hover:shadow-md',
                              statusStyles[reservation.status],
                              continuesBefore ? 'rounded-l-none' : 'rounded-l-md',
                              continuesAfter ? 'rounded-r-none' : 'rounded-r-md',
                            )}
                          >
                            {reservation.guest.fullName}
                          </button>
                        ))}
                      </div>
                    </div>
                  ))}
                </div>
              ))}
            </div>
          </div>
        </Card>
      )}

      <div className="mt-4 flex flex-wrap gap-x-4 gap-y-2 text-xs text-slate-600">
        {(['Pending', 'Confirmed', 'CheckedIn', 'CheckedOut'] as const).map((status) => (
          <span key={status} className="flex items-center gap-1.5">
            <span className={clsx('size-3 rounded-sm ring-1 ring-inset', statusStyles[status])} />
            {statusLabels[status]}
          </span>
        ))}
      </div>

      {selectedId && <ReservationDetailModal reservationId={selectedId} onClose={() => setSelectedId(null)} />}
    </>
  )
}
