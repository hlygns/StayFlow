import { useQuery } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { reservationsApi } from '../../api/endpoints'
import { Button } from '../../components/ui/Button'
import { ErrorAlert } from '../../components/ui/Card'
import { Modal } from '../../components/ui/Modal'
import { Spinner } from '../../components/ui/Spinner'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { formatDate, formatMoney, today } from '../../lib/format'
import { useApiMutation } from '../../lib/useApiMutation'

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex justify-between gap-4 py-2 text-sm">
      <dt className="text-slate-500">{label}</dt>
      <dd className="text-right font-medium text-slate-900">{children}</dd>
    </div>
  )
}

/** Takvimden ya da listeden acilan rezervasyon ozeti + onayla/iptal et. */
export function ReservationDetailModal({ reservationId, onClose }: { reservationId: string; onClose: () => void }) {
  const { data: reservation, isLoading, error } = useQuery({
    queryKey: ['reservations', reservationId],
    queryFn: () => reservationsApi.get(reservationId),
  })

  // Durum degisince takvim, liste ve musaitlik de etkilenir -> hepsini yenile.
  const invalidate = [['reservations'], ['availability']]
  const confirm = useApiMutation(() => reservationsApi.confirm(reservationId), { invalidate, successMessage: 'Rezervasyon onaylandı.' })
  const cancel = useApiMutation(() => reservationsApi.cancel(reservationId), {
    invalidate,
    successMessage: 'Rezervasyon iptal edildi; geceler tekrar satışa açıldı.',
    onSuccess: onClose,
  })

  const checkIn = useApiMutation(() => reservationsApi.checkIn(reservationId), { invalidate, successMessage: 'Check-in yapıldı.' })
  const checkOut = useApiMutation(() => reservationsApi.checkOut(reservationId), {
    invalidate,
    successMessage: (r) => (r.nights < (reservation?.nights ?? 0) ? 'Erken check-out yapıldı; kalan geceler satışa açıldı.' : 'Check-out yapıldı.'),
  })

  // Backend'deki kurallarin aynisi - butonu sadece gecerli oldugunda goster.
  const todayIso = today()
  const status = reservation?.status
  const canConfirm = status === 'Pending'
  const canCheckIn = status === 'Confirmed' && reservation!.checkIn <= todayIso && todayIso < reservation!.checkOut
  const canCheckOut = status === 'CheckedIn'
  const canCancel = status === 'Pending' || status === 'Confirmed'
  const hasActions = canConfirm || canCheckIn || canCheckOut || canCancel

  return (
    <Modal
      open
      title={reservation ? `Oda ${reservation.roomNumber} · ${reservation.guest.fullName}` : 'Rezervasyon'}
      onClose={onClose}
      footer={
        reservation && hasActions ? (
          <>
            {canCancel && (
              <Button
                variant="danger"
                loading={cancel.isPending}
                onClick={() => window.confirm('Rezervasyon iptal edilsin mi?') && cancel.mutate(undefined)}
              >
                İptal et
              </Button>
            )}
            {canConfirm && (
              <Button loading={confirm.isPending} onClick={() => confirm.mutate(undefined)}>
                Onayla
              </Button>
            )}
            {canCheckIn && (
              <Button loading={checkIn.isPending} onClick={() => checkIn.mutate(undefined)}>
                Check-in yap
              </Button>
            )}
            {canCheckOut && (
              <Button
                loading={checkOut.isPending}
                onClick={() =>
                  (todayIso >= reservation.checkOut ||
                    window.confirm('Misafir planlanandan erken ayrılıyor. Kalan geceler satışa açılacak. Devam edilsin mi?')) &&
                  checkOut.mutate(undefined)
                }
              >
                Check-out yap
              </Button>
            )}
          </>
        ) : undefined
      }
    >
      {isLoading ? (
        <div className="flex justify-center py-8 text-brand-600">
          <Spinner />
        </div>
      ) : error ? (
        <ErrorAlert error={error} />
      ) : (
        reservation && (
          <dl className="divide-y divide-slate-100">
            <Row label="Durum">
              <StatusBadge status={reservation.status} />
            </Row>
            <Row label="Giriş – Çıkış">
              {formatDate(reservation.checkIn)} – {formatDate(reservation.checkOut)}
            </Row>
            <Row label="Konaklama">
              {reservation.nights} gece · {reservation.guestCount} kişi
            </Row>
            <Row label="Oda">
              {reservation.roomNumber} ({reservation.roomTypeName})
            </Row>
            <Row label="Misafir">{reservation.guest.fullName}</Row>
            {reservation.guest.phone && <Row label="Telefon">{reservation.guest.phone}</Row>}
            {reservation.guest.email && <Row label="E-posta">{reservation.guest.email}</Row>}
            <Row label="Toplam tutar">
              <span className="text-base tabular-nums">{formatMoney(reservation.totalPrice)}</span>
            </Row>
          </dl>
        )
      )}
    </Modal>
  )
}
