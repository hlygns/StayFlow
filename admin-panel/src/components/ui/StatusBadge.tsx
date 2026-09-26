import clsx from 'clsx'
import type { ReservationStatus } from '../../api/types'
import { statusLabels, statusStyles } from '../../lib/format'

export function StatusBadge({ status }: { status: ReservationStatus }) {
  return (
    <span className={clsx('inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset', statusStyles[status])}>
      {statusLabels[status]}
    </span>
  )
}
