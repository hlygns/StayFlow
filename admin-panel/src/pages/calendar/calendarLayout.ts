import type { IsoDate, Reservation } from '../../api/types'
import { addDaysIso, nightsBetween } from '../../lib/format'

export interface CalendarBlock {
  reservation: Reservation
  /** 0'dan baslayan sutun indeksi (gorunen ilk gece = 0) */
  startColumn: number
  /** Kac sutun (gece) kapladigi */
  span: number
  /** Rezervasyon gorunen aralik disindan basliyor/bitiyorsa kenar yuvarlatilmaz */
  continuesBefore: boolean
  continuesAfter: boolean
}

/**
 * Takvimde her sutun bir GECE'dir. Rezervasyon [checkIn, checkOut) gecelerini kaplar:
 * 10-13 Temmuz = 10, 11, 12 Temmuz geceleri = 3 sutun. Boylece 13 Temmuz'da giren yeni
 * misafirin blogu, cikan misafirinkiyle ust uste binmez - backend'deki RoomNight mantiginin aynisi.
 */
export function layoutBlocks(reservations: Reservation[], windowStart: IsoDate, days: number): Map<string, CalendarBlock[]> {
  const windowEnd = addDaysIso(windowStart, days)
  const byRoom = new Map<string, CalendarBlock[]>()

  for (const reservation of reservations) {
    if (reservation.status === 'Cancelled') continue
    // Gorunen aralikla hic kesismiyorsa atla
    if (reservation.checkOut <= windowStart || reservation.checkIn >= windowEnd) continue

    const visibleStart = reservation.checkIn < windowStart ? windowStart : reservation.checkIn
    const visibleEnd = reservation.checkOut > windowEnd ? windowEnd : reservation.checkOut

    const block: CalendarBlock = {
      reservation,
      startColumn: nightsBetween(windowStart, visibleStart),
      span: nightsBetween(visibleStart, visibleEnd),
      continuesBefore: reservation.checkIn < windowStart,
      continuesAfter: reservation.checkOut > windowEnd,
    }
    byRoom.set(reservation.roomId, [...(byRoom.get(reservation.roomId) ?? []), block])
  }

  return byRoom
}

/** Gorunen araliktaki dolu gece sayisi / toplam oda-gece. Takvimin ustundeki doluluk orani. */
export function occupancyRate(blocks: Map<string, CalendarBlock[]>, roomCount: number, days: number) {
  if (roomCount === 0 || days === 0) return 0
  let bookedNights = 0
  for (const roomBlocks of blocks.values()) for (const block of roomBlocks) bookedNights += block.span
  return bookedNights / (roomCount * days)
}
