// Bu sekmede kullanicinin az once degistirdigi rezervasyonlar. Sunucudan ayni degisiklik icin canli
// bildirim geldiginde ikinci kez "Yeni rezervasyon" gostermemek icin kullanilir.

const WINDOW_MS = 15_000
const recent = new Map<string, number>()

export function markChangedLocally(reservationId: string) {
  recent.set(reservationId, Date.now())
}

export function wasChangedLocally(reservationId: string, now = Date.now()): boolean {
  for (const [id, at] of recent) if (now - at > WINDOW_MS) recent.delete(id)
  return recent.has(reservationId)
}
