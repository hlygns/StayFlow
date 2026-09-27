// Native depolama modulleri Jest'te yok: kutuphanelerin kendi sahte (mock) surumleri
jest.mock('@react-native-async-storage/async-storage', () => require('@react-native-async-storage/async-storage/jest/async-storage-mock'))
jest.mock('expo-secure-store', () => ({ getItemAsync: jest.fn(), setItemAsync: jest.fn() }))

import { upsertBooking, type SavedBooking } from '../bookings'

const booking = (code: string, savedAt: number): SavedBooking => ({ code, email: 'a@test.com', hotelName: 'Otel', checkIn: '2027-07-10', savedAt })

describe('kayitli rezervasyonlar', () => {
  it('yeni rezervasyon listenin basina eklenir', () => {
    const list = upsertBooking([booking('AAAA1111', 1)], booking('BBBB2222', 2))
    expect(list.map((b) => b.code)).toEqual(['BBBB2222', 'AAAA1111'])
  })

  it('ayni kod tekrar eklenirse cift kayit olmaz, guncellenip en uste tasinir', () => {
    const list = upsertBooking([booking('AAAA1111', 1), booking('BBBB2222', 2)], booking('BBBB2222', 3))
    expect(list).toHaveLength(2)
    expect(list[0]).toMatchObject({ code: 'BBBB2222', savedAt: 3 })
  })
})
