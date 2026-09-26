import type { IsoDate, Reservation } from '../../api/types'
import { addDaysIso } from '../../lib/format'

export interface NightOccupancy {
  date: IsoDate
  booked: number
  rate: number
}

export interface DashboardStats {
  arrivals: Reservation[]
  departures: Reservation[]
  inHouse: number
  pendingPayment: number
  nights: NightOccupancy[]
}

const isActive = (r: Reservation) => r.status !== 'Cancelled'

/**
 * Bir gece "dolu" = o geceyi kapsayan iptal edilmemis bir rezervasyon var: checkIn <= gece < checkOut.
 * (Takvim ve backend'deki RoomNight tablosuyla ayni kural.)
 */
export function computeDashboardStats(reservations: Reservation[], roomCount: number, today: IsoDate, days = 7): DashboardStats {
  const active = reservations.filter(isActive)
  const covers = (r: Reservation, night: IsoDate) => r.checkIn <= night && night < r.checkOut

  const nights = Array.from({ length: days }, (_, i) => {
    const date = addDaysIso(today, i)
    const booked = active.filter((r) => covers(r, date)).length
    return { date, booked, rate: roomCount === 0 ? 0 : booked / roomCount }
  })

  return {
    arrivals: active.filter((r) => r.checkIn === today && r.status !== 'CheckedIn'),
    departures: active.filter((r) => r.checkOut === today),
    inHouse: nights[0]?.booked ?? 0,
    pendingPayment: active.filter((r) => r.status === 'Pending').length,
    nights,
  }
}
