import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { z } from 'zod'
import { ApiError } from '../../api/client'
import { availabilityApi, reservationsApi, type ReservationInput } from '../../api/endpoints'
import type { IsoDate } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { Card, EmptyState, ErrorAlert, PageHeader } from '../../components/ui/Card'
import { Input } from '../../components/ui/Field'
import { PageLoader } from '../../components/ui/Spinner'
import { addDaysIso, formatMoney, formatRange, nightsBetween, today } from '../../lib/format'
import { useApiMutation } from '../../lib/useApiMutation'

const MAX_NIGHTS = 30 // backend: StayDates.MaxNights

const guestSchema = z.object({
  fullName: z.string().trim().min(1, 'Misafir adı gerekli.').max(200),
  phone: z.string().trim().max(30),
  email: z.union([z.literal(''), z.email('Geçerli bir e-posta girin.')]),
  identityNumber: z.string().trim().max(20),
})
type GuestForm = z.infer<typeof guestSchema>

function validateDates(checkIn: IsoDate, checkOut: IsoDate): string | null {
  if (!checkIn || !checkOut) return 'Giriş ve çıkış tarihlerini seçin.'
  if (checkIn < today()) return 'Geçmiş bir tarih seçilemez.'
  if (checkOut <= checkIn) return 'Çıkış, girişten sonra olmalı.'
  if (nightsBetween(checkIn, checkOut) > MAX_NIGHTS) return `En fazla ${MAX_NIGHTS} gece seçilebilir.`
  return null
}

export function NewReservationPage() {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  // Takvimden gelindiyse oda ve giris tarihi hazir gelir.
  const initialCheckIn = params.get('checkIn') ?? today()
  const [checkIn, setCheckIn] = useState(initialCheckIn)
  const [checkOut, setCheckOut] = useState(addDaysIso(initialCheckIn, 1))
  const [guests, setGuests] = useState(1)
  const [roomId, setRoomId] = useState<string | null>(params.get('roomId'))

  const dateError = validateDates(checkIn, checkOut)
  const availabilityKey = ['availability', { checkIn, checkOut, guests }]
  const availability = useQuery({
    queryKey: availabilityKey,
    queryFn: () => availabilityApi.search(checkIn, checkOut, guests),
    enabled: dateError === null,
  })

  const selectedType = availability.data?.find((type) => type.rooms.some((room) => room.id === roomId))
  const selectedRoom = selectedType?.rooms.find((room) => room.id === roomId)
  // Secili oda (ör. takvimden gelen) yeni tarihlerde dolu cikarsa kullaniciya soyle.
  const roomNoLongerAvailable = roomId !== null && availability.isSuccess && !selectedRoom

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<GuestForm>({ resolver: zodResolver(guestSchema), defaultValues: { fullName: '', phone: '', email: '', identityNumber: '' } })

  const create = useApiMutation((input: ReservationInput) => reservationsApi.create(input), {
    invalidate: [['reservations'], ['availability']],
    successMessage: (r) => `Oda ${r.roomNumber} için rezervasyon oluşturuldu.`,
    onSuccess: () => navigate('/calendar'),
    silentError: true,
  })

  const onSubmit = handleSubmit(async (guest) => {
    if (!selectedRoom) return
    try {
      await create.mutateAsync({
        roomId: selectedRoom.id,
        checkIn,
        checkOut,
        guestCount: guests,
        guest: {
          fullName: guest.fullName,
          phone: guest.phone || undefined,
          email: guest.email || undefined,
          identityNumber: guest.identityNumber || undefined,
        },
      })
    } catch (error) {
      // 409: biz formu doldururken oda baskasina satildi (cifte rezervasyon korumasi devrede).
      // Musaitligi yenile ki kullanici guncel listeden secsin.
      if (error instanceof ApiError && error.status === 409) {
        setRoomId(null)
        await queryClient.invalidateQueries({ queryKey: ['availability'] })
      }
    }
  })

  const nights = dateError ? 0 : nightsBetween(checkIn, checkOut)

  return (
    <>
      <PageHeader title="Yeni rezervasyon" description="Tarihleri seçin, müsait odalardan birini seçin, misafir bilgilerini girin." />

      <div className="grid gap-6 lg:grid-cols-[1fr_22rem]">
        <div className="space-y-6">
          {/* 1. Tarihler */}
          <Card className="p-5">
            <h2 className="mb-4 font-semibold text-slate-900">1. Tarihler ve kişi sayısı</h2>
            <div className="grid gap-4 sm:grid-cols-3">
              <Input
                label="Giriş"
                type="date"
                min={today()}
                value={checkIn}
                onChange={(e) => {
                  setCheckIn(e.target.value)
                  // Cikis tarihi girisin onunde kalmasin
                  if (e.target.value && checkOut <= e.target.value) setCheckOut(addDaysIso(e.target.value, 1))
                }}
              />
              <Input label="Çıkış" type="date" min={checkIn ? addDaysIso(checkIn, 1) : undefined} value={checkOut} onChange={(e) => setCheckOut(e.target.value)} />
              <Input label="Kişi sayısı" type="number" min={1} max={20} value={guests} onChange={(e) => setGuests(Math.max(1, Number(e.target.value) || 1))} />
            </div>
            {dateError && <p className="mt-3 text-sm text-red-600">{dateError}</p>}
          </Card>

          {/* 2. Oda secimi */}
          <Card className="p-5">
            <h2 className="mb-4 font-semibold text-slate-900">2. Oda seçimi</h2>
            {dateError ? (
              <p className="text-sm text-slate-500">Müsait odaları görmek için geçerli tarihler seçin.</p>
            ) : availability.isLoading ? (
              <PageLoader />
            ) : availability.error ? (
              <ErrorAlert error={availability.error} />
            ) : availability.data!.length === 0 ? (
              <EmptyState title="Bu tarihlerde müsait oda yok" description={`${guests} kişilik ve boş oda bulunamadı. Farklı tarihler deneyin.`} />
            ) : (
              <div className="space-y-4">
                {roomNoLongerAvailable && (
                  <p role="alert" className="rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800 ring-1 ring-amber-200">
                    Seçtiğiniz oda bu tarihlerde müsait değil ya da kişi sayısına uygun değil. Lütfen aşağıdan başka bir oda seçin.
                  </p>
                )}
                {availability.data!.map((type) => (
                  <fieldset key={type.roomTypeId} className="rounded-lg p-4 ring-1 ring-slate-200">
                    <legend className="sr-only">{type.roomTypeName}</legend>
                    <div className="mb-3 flex flex-wrap items-baseline justify-between gap-2">
                      <div>
                        <p className="font-medium text-slate-900">{type.roomTypeName}</p>
                        <p className="text-xs text-slate-500">
                          {type.capacity} kişilik · {type.availableRooms} oda boş
                        </p>
                      </div>
                      <p className="text-right">
                        <span className="font-semibold text-slate-900 tabular-nums">{formatMoney(type.totalPrice)}</span>
                        <span className="text-xs text-slate-500"> / {type.nights} gece</span>
                      </p>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      {type.rooms.map((room) => (
                        <label
                          key={room.id}
                          className={clsx(
                            'cursor-pointer rounded-lg px-3 py-1.5 text-sm font-medium tabular-nums ring-1 transition-colors has-focus-visible:outline-2 has-focus-visible:outline-brand-500',
                            room.id === roomId ? 'bg-brand-600 text-white ring-brand-600' : 'bg-white text-slate-700 ring-slate-300 hover:bg-slate-50',
                          )}
                        >
                          <input type="radio" name="room" className="sr-only" checked={room.id === roomId} onChange={() => setRoomId(room.id)} />
                          {room.roomNumber}
                        </label>
                      ))}
                    </div>
                  </fieldset>
                ))}
              </div>
            )}
          </Card>

          {/* 3. Misafir */}
          <Card className="p-5">
            <h2 className="mb-4 font-semibold text-slate-900">3. Misafir bilgileri</h2>
            <form id="reservation-form" onSubmit={onSubmit} className="grid gap-4 sm:grid-cols-2" noValidate>
              <div className="sm:col-span-2">
                <Input label="Ad soyad" autoComplete="off" error={errors.fullName?.message} {...register('fullName')} />
              </div>
              <Input label="Telefon" type="tel" autoComplete="off" error={errors.phone?.message} {...register('phone')} />
              <Input label="E-posta" type="email" autoComplete="off" error={errors.email?.message} {...register('email')} />
              <Input label="T.C. / Pasaport no" hint="İsteğe bağlı" autoComplete="off" error={errors.identityNumber?.message} {...register('identityNumber')} />
            </form>
          </Card>
        </div>

        {/* Ozet - masaustunde sagda sabit */}
        <aside className="lg:sticky lg:top-8 lg:self-start">
          <Card className="p-5">
            <h2 className="font-semibold text-slate-900">Özet</h2>
            <dl className="mt-4 space-y-2 text-sm">
              <div className="flex justify-between gap-4">
                <dt className="text-slate-500">Tarihler</dt>
                <dd className="text-right font-medium text-slate-900">{dateError ? '—' : formatRange(checkIn, checkOut)}</dd>
              </div>
              <div className="flex justify-between gap-4">
                <dt className="text-slate-500">Süre</dt>
                <dd className="font-medium text-slate-900">{nights ? `${nights} gece · ${guests} kişi` : '—'}</dd>
              </div>
              <div className="flex justify-between gap-4">
                <dt className="text-slate-500">Oda</dt>
                <dd className="text-right font-medium text-slate-900">
                  {selectedRoom && selectedType ? `${selectedRoom.roomNumber} · ${selectedType.roomTypeName}` : '—'}
                </dd>
              </div>
            </dl>
            <div className="mt-4 flex items-baseline justify-between border-t border-slate-100 pt-4">
              <span className="text-sm text-slate-500">Toplam</span>
              <span className="text-2xl font-semibold text-slate-900 tabular-nums">{selectedType ? formatMoney(selectedType.totalPrice) : '—'}</span>
            </div>
            <div className="mt-4 space-y-3">
              <ErrorAlert error={create.error} />
              <Button type="submit" form="reservation-form" className="w-full" disabled={!selectedRoom} loading={create.isPending}>
                Rezervasyonu oluştur
              </Button>
              <p className="text-center text-xs text-slate-500">Rezervasyon “Ödeme bekliyor” olarak oluşur. 15 dakika içinde onaylanmazsa otomatik iptal edilir ve oda tekrar satışa açılır.</p>
              <Link to="/calendar" className="block text-center text-sm font-medium text-slate-600 hover:underline">
                Vazgeç
              </Link>
            </div>
          </Card>
        </aside>
      </div>
    </>
  )
}
