import { describe, expect, it } from 'vitest'
import type { Reservation } from '../../api/types'
import { layoutBlocks, occupancyRate } from './calendarLayout'

const reservation = (overrides: Partial<Reservation>): Reservation => ({
  id: crypto.randomUUID(),
  roomId: 'room-101',
  roomNumber: '101',
  roomTypeName: 'Standart',
  checkIn: '2027-07-10',
  checkOut: '2027-07-13',
  nights: 3,
  guestCount: 2,
  status: 'Confirmed',
  totalPrice: 3000,
  paymentDeadline: null,
  createdAt: '2027-07-01T10:00:00Z',
  guest: { id: 'g', fullName: 'Ayşe Yılmaz', email: null, phone: null },
  ...overrides,
})

describe('layoutBlocks', () => {
  it('rezervasyonu gece sayisi kadar sutuna yerlestirir', () => {
    const blocks = layoutBlocks([reservation({})], '2027-07-08', 14)

    expect(blocks.get('room-101')).toEqual([
      expect.objectContaining({ startColumn: 2, span: 3, continuesBefore: false, continuesAfter: false }),
    ])
  })

  it('cikis gunu giren misafirin blogu oncekiyle ust uste binmez', () => {
    const blocks = layoutBlocks(
      [reservation({ checkIn: '2027-07-10', checkOut: '2027-07-13' }), reservation({ checkIn: '2027-07-13', checkOut: '2027-07-15' })],
      '2027-07-10',
      14,
    )

    const [first, second] = blocks.get('room-101')!
    expect(first.startColumn + first.span).toBe(second.startColumn)
  })

  it('gorunen araligin disina tasan rezervasyonu kirpar ve isaretler', () => {
    const blocks = layoutBlocks([reservation({ checkIn: '2027-07-05', checkOut: '2027-07-30' })], '2027-07-10', 7)

    expect(blocks.get('room-101')![0]).toMatchObject({ startColumn: 0, span: 7, continuesBefore: true, continuesAfter: true })
  })

  it('iptal edilenleri ve aralikla kesismeyenleri gostermez', () => {
    const blocks = layoutBlocks(
      [
        reservation({ status: 'Cancelled' }),
        reservation({ checkIn: '2027-07-01', checkOut: '2027-07-10' }), // pencere 10'unda basliyor: cikis gunu = kesismez
      ],
      '2027-07-10',
      7,
    )

    expect(blocks.size).toBe(0)
  })

  it('doluluk orani dolu oda-gece / toplam oda-gece', () => {
    const blocks = layoutBlocks([reservation({ checkIn: '2027-07-10', checkOut: '2027-07-12' })], '2027-07-10', 2)

    expect(occupancyRate(blocks, 2, 2)).toBe(0.5) // 2 dolu / (2 oda x 2 gece)
    expect(occupancyRate(blocks, 0, 2)).toBe(0)
  })
})
