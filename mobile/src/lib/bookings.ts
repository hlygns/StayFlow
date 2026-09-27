import AsyncStorage from '@react-native-async-storage/async-storage'
import * as SecureStore from 'expo-secure-store'
import { Platform } from 'react-native'

/**
 * Misafirin rezervasyonlari telefonda saklanir (hesap yok). "Kod + e-posta" bir rezervasyonun anahtaridir:
 * telefonda sifreli depolamada (iOS Keychain / Android Keystore), web onizlemesinde tarayici deposunda tutulur.
 */
export interface SavedBooking {
  code: string
  email: string
  hotelName: string
  checkIn: string
  savedAt: number
}

const KEY = 'stayflow.bookings'

const storage = {
  get: (key: string) => (Platform.OS === 'web' ? AsyncStorage.getItem(key) : SecureStore.getItemAsync(key)),
  set: (key: string, value: string) => (Platform.OS === 'web' ? AsyncStorage.setItem(key, value) : SecureStore.setItemAsync(key, value)),
}

export async function loadBookings(): Promise<SavedBooking[]> {
  try {
    const raw = await storage.get(KEY)
    return raw ? (JSON.parse(raw) as SavedBooking[]) : []
  } catch {
    return []
  }
}

/** Ayni kod tekrar eklenirse guncellenir; en yeni en ustte. */
export function upsertBooking(list: SavedBooking[], booking: SavedBooking): SavedBooking[] {
  return [booking, ...list.filter((b) => b.code !== booking.code)]
}

export async function saveBooking(booking: Omit<SavedBooking, 'savedAt'>) {
  const list = await loadBookings()
  await storage.set(KEY, JSON.stringify(upsertBooking(list, { ...booking, savedAt: Date.now() })))
}

export async function removeBooking(code: string) {
  const list = await loadBookings()
  await storage.set(KEY, JSON.stringify(list.filter((b) => b.code !== code)))
}
