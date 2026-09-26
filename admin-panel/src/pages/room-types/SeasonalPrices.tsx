import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { roomTypesApi, type SeasonalPriceInput } from '../../api/endpoints'
import type { RoomType } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { ErrorAlert } from '../../components/ui/Card'
import { Input } from '../../components/ui/Field'
import { Spinner } from '../../components/ui/Spinner'
import { formatDate, formatMoney } from '../../lib/format'
import { useApiMutation } from '../../lib/useApiMutation'

const schema = z
  .object({
    startDate: z.string().min(1, 'Tarih seçin.'),
    endDate: z.string().min(1, 'Tarih seçin.'),
    pricePerNight: z.number('Fiyat girin.').positive('Fiyat 0\'dan büyük olmalı.'),
  })
  .refine((v) => v.endDate > v.startDate, { path: ['endDate'], message: 'Bitiş, başlangıçtan sonra olmalı.' })

/** Bir oda tipinin sezon fiyatlari: liste + ekleme formu. Oda tipi kartinin icinde acilir. */
export function SeasonalPrices({ roomType, canEdit }: { roomType: RoomType; canEdit: boolean }) {
  const queryKey = ['room-types', roomType.id, 'prices']
  const { data: prices, isLoading, error } = useQuery({ queryKey, queryFn: () => roomTypesApi.prices(roomType.id) })

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<SeasonalPriceInput>({ resolver: zodResolver(schema) })

  const create = useApiMutation((input: SeasonalPriceInput) => roomTypesApi.createPrice(roomType.id, input), {
    invalidate: [queryKey],
    successMessage: 'Sezon fiyatı eklendi.',
    onSuccess: () => reset(),
    silentError: true,
  })
  const remove = useApiMutation((id: string) => roomTypesApi.removePrice(roomType.id, id), {
    invalidate: [queryKey],
    successMessage: 'Sezon fiyatı silindi.',
  })

  return (
    <div className="space-y-3">
      {isLoading ? (
        <Spinner className="size-5 text-brand-600" />
      ) : error ? (
        <ErrorAlert error={error} />
      ) : prices!.length === 0 ? (
        <p className="text-sm text-slate-500">
          Sezon fiyatı yok. Her gece için baz fiyat ({formatMoney(roomType.basePrice)}) uygulanır.
        </p>
      ) : (
        <ul className="divide-y divide-slate-100 rounded-lg ring-1 ring-slate-200">
          {prices!.map((price) => (
            <li key={price.id} className="flex items-center justify-between gap-3 px-3 py-2 text-sm">
              <span className="text-slate-600">
                {formatDate(price.startDate)} – {formatDate(price.endDate)}
              </span>
              <span className="flex items-center gap-3">
                <span className="font-medium text-slate-900 tabular-nums">{formatMoney(price.pricePerNight)}</span>
                {canEdit && (
                  <button
                    type="button"
                    className="text-xs font-medium text-red-600 hover:underline disabled:opacity-50"
                    disabled={remove.isPending}
                    onClick={() => remove.mutate(price.id)}
                  >
                    Sil
                  </button>
                )}
              </span>
            </li>
          ))}
        </ul>
      )}

      {canEdit && (
        <form onSubmit={handleSubmit((values) => create.mutate(values))} className="space-y-3 rounded-lg bg-slate-50 p-3" noValidate>
          <ErrorAlert error={create.error} />
          <div className="grid gap-3 sm:grid-cols-3">
            <Input label="Başlangıç" type="date" error={errors.startDate?.message} {...register('startDate')} />
            <Input label="Bitiş (dahil)" type="date" error={errors.endDate?.message} {...register('endDate')} />
            <Input label="Gecelik (₺)" type="number" min={1} error={errors.pricePerNight?.message} {...register('pricePerNight', { valueAsNumber: true })} />
          </div>
          <Button type="submit" size="sm" variant="secondary" loading={create.isPending}>
            Sezon fiyatı ekle
          </Button>
        </form>
      )}
    </div>
  )
}
