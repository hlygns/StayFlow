import { addDays, differenceInCalendarDays, format, parseISO } from 'date-fns'
import { tr } from 'date-fns/locale'
import type { IsoDate, ReservationStatus } from '../api/types'

const currency = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 0 })

export const formatMoney = (value: number) => currency.format(value)

/** Date -> "2027-07-10" (backend'in DateOnly formati). Saat dilimi kaymasin diye toISOString KULLANILMAZ. */
export const toIsoDate = (date: Date): IsoDate => format(date, 'yyyy-MM-dd')

export const today = (): IsoDate => toIsoDate(new Date())

export const addDaysIso = (date: IsoDate, days: number): IsoDate => toIsoDate(addDays(parseISO(date), days))

export const nightsBetween = (checkIn: IsoDate, checkOut: IsoDate) =>
  differenceInCalendarDays(parseISO(checkOut), parseISO(checkIn))

/** "10 Tem 2027" */
export const formatDate = (date: IsoDate) => format(parseISO(date), 'd MMM yyyy', { locale: tr })

/** "10 Tem – 13 Tem 2027" */
export const formatRange = (checkIn: IsoDate, checkOut: IsoDate) =>
  `${format(parseISO(checkIn), 'd MMM', { locale: tr })} – ${formatDate(checkOut)}`

export const statusLabels: Record<ReservationStatus, string> = {
  Pending: 'Ödeme bekliyor',
  Confirmed: 'Onaylandı',
  CheckedIn: 'Konaklıyor',
  CheckedOut: 'Ayrıldı',
  Cancelled: 'İptal edildi',
}

/** Durum rozetleri ve takvim bloklari ayni renkleri kullanir. */
export const statusStyles: Record<ReservationStatus, string> = {
  Pending: 'bg-amber-50 text-amber-800 ring-amber-200',
  Confirmed: 'bg-emerald-50 text-emerald-800 ring-emerald-200',
  CheckedIn: 'bg-sky-50 text-sky-800 ring-sky-200',
  CheckedOut: 'bg-slate-100 text-slate-600 ring-slate-200',
  Cancelled: 'bg-red-50 text-red-700 ring-red-200',
}
