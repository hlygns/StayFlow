import { describe, expect, it } from 'vitest'
import type { Reservation } from '../../api/types'
import { computeDashboardStats } from './dashboardStats'

const TODAY = '2027-07-10'

const r = (checkIn: string, checkOut: string, status: Reservation['status'] = 'Confirmed'): Reservation => ({
  id: crypto.randomUUID(),
  roomId: crypto.randomUUID(),
  roomNumber: '101',
  roomTypeName: 'Standart',
  checkIn,
  checkOut,
  nights: 1,
  guestCount: 2,
  status,
  totalPrice: 1000,
  paymentDeadline: null,
  createdAt: '2027-07-01T10:00:00Z',
  guest: { id: 'g', fullName: 'Misafir', email: null, phone: null },
})

describe('computeDashboardStats', () => {
  it('bugun giris, cikis ve bu gece konaklayanlari ayirir', () => {
    const stats = computeDashboardStats(
      [
        r('2027-07-10', '2027-07-12'), // bugun giriyor
        r('2027-07-08', '2027-07-10'), // bugun cikiyor - bu gece konaklamiyor
        r('2027-07-09', '2027-07-11'), // dun girdi, konakliyor
        r('2027-07-10', '2027-07-11', 'Cancelled'), // iptal: hicbir yerde sayilmaz
      ],
      4,
      TODAY,
    )

    expect(stats.arrivals).toHaveLength(1)
    expect(stats.departures).toHaveLength(1)
    expect(stats.inHouse).toBe(2)
    expect(stats.nights[0].rate).toBe(0.5)
  })

  it('odeme bekleyenleri sayar ve 7 gecelik seri uretir', () => {
    const stats = computeDashboardStats([r('2027-07-12', '2027-07-14', 'Pending')], 2, TODAY)

    expect(stats.pendingPayment).toBe(1)
    expect(stats.nights.map((n) => n.booked)).toEqual([0, 0, 1, 1, 0, 0, 0])
  })

  it('oda yoksa doluluk 0 (sifira bolme yok)', () => {
    expect(computeDashboardStats([], 0, TODAY).nights[0].rate).toBe(0)
  })
})
