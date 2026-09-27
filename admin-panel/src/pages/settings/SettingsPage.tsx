import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { hotelApi } from '../../api/endpoints'
import type { Hotel } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Card, ErrorAlert, PageHeader } from '../../components/ui/Card'
import { Input, Textarea } from '../../components/ui/Field'
import { PageLoader } from '../../components/ui/Spinner'
import { useApiMutation } from '../../lib/useApiMutation'

const schema = z.object({
  name: z.string().trim().min(1, 'Otel adı gerekli.').max(200),
  city: z.string().trim().min(1, 'Şehir gerekli.').max(100),
  address: z.string().trim().max(500),
  phone: z.string().trim().max(30),
  email: z.union([z.literal(''), z.email('Geçerli bir e-posta girin.')]),
  description: z.string().trim().max(2000, 'En fazla 2000 karakter.'),
})
type FormValues = z.infer<typeof schema>

function HotelForm({ hotel }: { hotel: Hotel }) {
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isDirty },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: hotel })

  const save = useApiMutation((values: FormValues) => hotelApi.update({ ...values, email: values.email || null }), {
    invalidate: [['hotel']],
    successMessage: 'Otel bilgileri kaydedildi.',
    onSuccess: (updated) => reset(updated), // "degisiklik var" durumunu sifirla
    silentError: true,
  })

  return (
    <Card className="max-w-2xl p-5 sm:p-6">
      <form onSubmit={handleSubmit((values) => save.mutate(values))} className="space-y-4" noValidate>
        <ErrorAlert error={save.error} />
        <div className="grid gap-4 sm:grid-cols-2">
          <Input label="Otel adı" error={errors.name?.message} {...register('name')} />
          <Input label="Şehir" error={errors.city?.message} {...register('city')} />
        </div>
        <Input label="Adres" error={errors.address?.message} {...register('address')} />
        <div className="grid gap-4 sm:grid-cols-2">
          <Input label="Telefon" type="tel" error={errors.phone?.message} {...register('phone')} />
          <Input label="E-posta" type="email" error={errors.email?.message} {...register('email')} />
        </div>
        <Textarea
          label="Tanıtım metni"
          placeholder="Misafir uygulamasında otelinizin sayfasında görünür. Ör. denize uzaklık, kahvaltı, otopark…"
          error={errors.description?.message}
          {...register('description')}
        />
        <div className="rounded-lg bg-slate-50 px-3 py-2 text-sm text-slate-600">
          Otel kısa adı (slug): <span className="font-mono text-slate-900">{hotel.slug}</span>
          <span className="block text-xs text-slate-500">Paylaşılmış bağlantılar bozulmasın diye değiştirilemez.</span>
        </div>
        <div className="flex justify-end">
          <Button type="submit" loading={save.isPending} disabled={!isDirty}>
            Kaydet
          </Button>
        </div>
      </form>
    </Card>
  )
}

export function SettingsPage() {
  const { data: hotel, isLoading, error } = useQuery({ queryKey: ['hotel'], queryFn: hotelApi.get })

  return (
    <>
      <PageHeader title="Otel ayarları" description="Otelinizin iletişim bilgileri." />
      {isLoading ? <PageLoader /> : error ? <ErrorAlert error={error} /> : <HotelForm hotel={hotel!} />}
    </>
  )
}
