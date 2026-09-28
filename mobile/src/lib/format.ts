import type { IsoDate, ReservationStatus } from '../api/types'

const money = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 0 })
export const formatMoney = (value: number) => money.format(value)

const pad = (n: number) => String(n).padStart(2, '0')

/** Yerel tarih -> "2027-07-10" (toISOString KULLANILMAZ: UTC'ye cevirip gunu kaydirabilir) */
export const toIsoDate = (d: Date): IsoDate => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`

export const parseIso = (iso: IsoDate) => {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(y, m - 1, d)
}

export const addDays = (iso: IsoDate, days: number): IsoDate => {
  const d = parseIso(iso)
  d.setDate(d.getDate() + days)
  return toIsoDate(d)
}

export const today = (): IsoDate => toIsoDate(new Date())

export const nightsBetween = (a: IsoDate, b: IsoDate) => Math.round((parseIso(b).getTime() - parseIso(a).getTime()) / 86_400_000)

const shortDate = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'short' })
const longDate = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long', year: 'numeric' })
const weekday = new Intl.DateTimeFormat('tr-TR', { weekday: 'short' })

export const formatShort = (iso: IsoDate) => shortDate.format(parseIso(iso))
export const formatLong = (iso: IsoDate) => longDate.format(parseIso(iso))
export const formatWeekday = (iso: IsoDate) => weekday.format(parseIso(iso))
export const formatRange = (a: IsoDate, b: IsoDate) => `${formatShort(a)} – ${formatLong(b)}`

export const statusLabels: Record<ReservationStatus, string> = {
  // Yonetim paneliyle ayni kelimeler: misafir otelde durumunu sorarken ikisi ayni seyi soylesin
  Pending: 'Ödeme bekliyor',
  Confirmed: 'Onaylandı',
  CheckedIn: 'Konaklıyor',
  CheckedOut: 'Ayrıldı',
  Cancelled: 'İptal edildi',
}

/** Kart numarasini 4'erli gruplar: "4242424242424242" -> "4242 4242 4242 4242" */
export const formatCardNumber = (raw: string) =>
  raw
    .replace(/\D/g, '')
    .slice(0, 16)
    .replace(/(\d{4})(?=\d)/g, '$1 ')

/** "1230" -> "12/30" */
export const formatExpiry = (raw: string) => {
  const digits = raw.replace(/\D/g, '').slice(0, 4)
  return digits.length > 2 ? `${digits.slice(0, 2)}/${digits.slice(2)}` : digits
}
