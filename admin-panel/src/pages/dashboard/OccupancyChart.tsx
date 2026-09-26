import clsx from 'clsx'
import { format, parseISO } from 'date-fns'
import { tr } from 'date-fns/locale'
import type { NightOccupancy } from './dashboardStats'

const TICKS = [1, 0.5, 0]
const percent = (rate: number) => `%${Math.round(rate * 100)}`

/**
 * Onumuzdeki gecelerin doluluk orani - tek seri sutun grafik.
 * Tek seri oldugu icin lejant yok (baslik seriyi adlandiriyor); degerler hover/odakta
 * tooltip'te, ekran okuyucular icin ayrica gizli bir tabloda.
 */
export function OccupancyChart({ nights, roomCount }: { nights: NightOccupancy[]; roomCount: number }) {
  return (
    <figure>
      <div className="flex gap-3">
        {/* Y ekseni etiketleri: her biri kendi izgara cizgisinin tam hizasinda */}
        <div className="relative h-44 w-9 shrink-0 text-right text-xs text-slate-500 tabular-nums" aria-hidden="true">
          <div className="absolute inset-x-0 top-0 bottom-6">
            {TICKS.map((tick) => (
              <span key={tick} className="absolute right-0 -translate-y-1/2" style={{ top: `${(1 - tick) * 100}%` }}>
                {percent(tick)}
              </span>
            ))}
          </div>
        </div>

        <div className="relative h-44 flex-1" aria-hidden="true">
          {/* Izgara cizgileri: ince, sade */}
          <div className="absolute inset-x-0 top-0 bottom-6 flex flex-col justify-between">
            {TICKS.map((tick) => (
              <div key={tick} className={clsx('h-px', tick === 0 ? 'bg-slate-300' : 'bg-slate-100')} />
            ))}
          </div>

          <div className="absolute inset-0 grid" style={{ gridTemplateColumns: `repeat(${nights.length}, 1fr)` }}>
            {nights.map((night, i) => (
              <div key={night.date} className="group relative flex flex-col items-center">
                {/* Hover alani sutunun tamami: ince sutunu hedeflemek zorunda kalmasin */}
                <div className="relative flex w-full flex-1 items-end justify-center pb-px" tabIndex={0}>
                  <div
                    className={clsx('w-full max-w-6 rounded-t transition-opacity', i === 0 ? 'bg-brand-600' : 'bg-brand-500', 'group-hover:opacity-80')}
                    style={{ height: `${night.rate * 100}%`, minHeight: night.booked > 0 ? 2 : 0 }}
                  />
                  <div className="pointer-events-none absolute bottom-full left-1/2 z-10 mb-1 hidden -translate-x-1/2 rounded-md bg-slate-900 px-2.5 py-1.5 text-xs whitespace-nowrap text-white shadow-lg group-focus-within:block group-hover:block">
                    <p className="font-medium">{format(parseISO(night.date), 'd MMMM EEEE', { locale: tr })}</p>
                    <p className="text-slate-300">
                      {percent(night.rate)} · {night.booked}/{roomCount} oda dolu
                    </p>
                  </div>
                </div>
                <span className={clsx('h-6 pt-1.5 text-xs whitespace-nowrap', i === 0 ? 'font-semibold text-slate-900' : 'text-slate-500')}>
                  {i === 0 ? 'Bugün' : format(parseISO(night.date), 'EEE', { locale: tr })}
                </span>
              </div>
            ))}
          </div>
        </div>
      </div>

      <table className="sr-only">
        <caption>Önümüzdeki gecelerin doluluk oranı</caption>
        <thead>
          <tr>
            <th>Gece</th>
            <th>Doluluk</th>
            <th>Dolu oda</th>
          </tr>
        </thead>
        <tbody>
          {nights.map((night) => (
            <tr key={night.date}>
              <td>{format(parseISO(night.date), 'd MMMM', { locale: tr })}</td>
              <td>{percent(night.rate)}</td>
              <td>
                {night.booked}/{roomCount}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  )
}
