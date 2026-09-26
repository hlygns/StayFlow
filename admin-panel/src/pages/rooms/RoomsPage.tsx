import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import clsx from 'clsx'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link } from 'react-router'
import { z } from 'zod'
import { roomsApi, roomTypesApi, type RoomInput } from '../../api/endpoints'
import type { Room } from '../../api/types'
import { useAuth } from '../../auth/AuthContext'
import { PlusIcon } from '../../components/icons'
import { Button } from '../../components/ui/Button'
import { Card, EmptyState, ErrorAlert, PageHeader } from '../../components/ui/Card'
import { Input, Select } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { PageLoader } from '../../components/ui/Spinner'
import { useApiMutation } from '../../lib/useApiMutation'

const schema = z.object({
  roomNumber: z.string().trim().min(1, 'Oda numarası gerekli.').max(20),
  floor: z.number('Kat girin.').int().min(-5).max(200),
  roomTypeId: z.string().min(1, 'Oda tipi seçin.'),
  isActive: z.boolean(),
})

// Odalar hem bu sayfada hem oda tipi kartlarinda (aktif oda sayisi) gorunuyor.
const invalidate = [['rooms'], ['room-types']]

function RoomFormModal({ room, onClose }: { room: Room | null; onClose: () => void }) {
  const { data: roomTypes = [] } = useQuery({ queryKey: ['room-types'], queryFn: roomTypesApi.list })
  const isEdit = room !== null

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<RoomInput>({
    resolver: zodResolver(schema),
    defaultValues: room ?? { roomNumber: '', floor: 1, roomTypeId: '', isActive: true },
  })

  const save = useApiMutation((input: RoomInput) => (isEdit ? roomsApi.update(room.id, input) : roomsApi.create(input)), {
    invalidate,
    successMessage: isEdit ? 'Oda güncellendi.' : 'Oda eklendi.',
    onSuccess: onClose,
    silentError: true,
  })

  return (
    <Modal
      open
      title={isEdit ? `Oda ${room.roomNumber}` : 'Yeni oda'}
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Vazgeç
          </Button>
          <Button type="submit" form="room-form" loading={save.isPending}>
            Kaydet
          </Button>
        </>
      }
    >
      <form id="room-form" onSubmit={handleSubmit((values) => save.mutate(values))} className="space-y-4" noValidate>
        <ErrorAlert error={save.error} />
        <div className="grid grid-cols-2 gap-4">
          <Input label="Oda numarası" placeholder="101" error={errors.roomNumber?.message} {...register('roomNumber')} />
          <Input label="Kat" type="number" error={errors.floor?.message} {...register('floor', { valueAsNumber: true })} />
        </div>
        <Select label="Oda tipi" error={errors.roomTypeId?.message} {...register('roomTypeId')}>
          <option value="">Seçin…</option>
          {roomTypes.map((rt) => (
            <option key={rt.id} value={rt.id}>
              {rt.name} ({rt.capacity} kişilik)
            </option>
          ))}
        </Select>
        {isEdit && (
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" className="size-4 rounded border-slate-300 accent-brand-600" {...register('isActive')} />
            Satışa açık (pasif odalar müsaitlikte görünmez)
          </label>
        )}
      </form>
    </Modal>
  )
}

export function RoomsPage() {
  const { isOwner } = useAuth()
  const [showInactive, setShowInactive] = useState(false)
  const [editing, setEditing] = useState<Room | null | undefined>(undefined)

  const { data: rooms, isLoading, error } = useQuery({
    queryKey: ['rooms', { showInactive }],
    queryFn: () => roomsApi.list(showInactive),
  })
  const { data: roomTypes } = useQuery({ queryKey: ['room-types'], queryFn: roomTypesApi.list })

  const deactivate = useApiMutation(roomsApi.deactivate, { invalidate, successMessage: 'Oda pasife alındı.' })

  const addButton = isOwner && (
    <Button onClick={() => setEditing(null)} disabled={roomTypes?.length === 0}>
      <PlusIcon className="size-4" />
      Oda ekle
    </Button>
  )

  // Kat kat gruplayarak goster - resepsiyon odalari boyle dusunur.
  const floors = new Map<number, Room[]>()
  for (const room of rooms ?? []) floors.set(room.floor, [...(floors.get(room.floor) ?? []), room])

  return (
    <>
      <PageHeader title="Odalar" description="Fiziksel odalar ve bağlı oldukları oda tipleri." actions={addButton} />

      <label className="mb-4 flex w-fit items-center gap-2 text-sm text-slate-600">
        <input type="checkbox" className="size-4 accent-brand-600" checked={showInactive} onChange={(e) => setShowInactive(e.target.checked)} />
        Pasif odaları da göster
      </label>

      {isLoading ? (
        <PageLoader />
      ) : error ? (
        <ErrorAlert error={error} />
      ) : rooms!.length === 0 ? (
        <Card>
          {roomTypes?.length === 0 ? (
            <EmptyState
              title="Önce oda tipi ekleyin"
              description="Her oda bir oda tipine bağlıdır (kapasite ve fiyat oradan gelir)."
              action={
                <Link to="/room-types" className="text-sm font-semibold text-brand-700 hover:underline">
                  Oda tiplerine git →
                </Link>
              }
            />
          ) : (
            <EmptyState title="Henüz oda yok" description="Otelinizdeki odaları numaralarıyla ekleyin." action={addButton} />
          )}
        </Card>
      ) : (
        <div className="space-y-6">
          {[...floors.entries()]
            .sort(([a], [b]) => a - b)
            .map(([floor, floorRooms]) => (
              <section key={floor}>
                <h2 className="mb-2 text-sm font-medium text-slate-500">{floor === 0 ? 'Zemin kat' : `${floor}. kat`}</h2>
                <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5">
                  {floorRooms.map((room) => (
                    <Card key={room.id} className={clsx('p-4', !room.isActive && 'opacity-60')}>
                      <div className="flex items-start justify-between gap-2">
                        <p className="text-xl font-semibold text-slate-900 tabular-nums">{room.roomNumber}</p>
                        {!room.isActive && <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-600">Pasif</span>}
                      </div>
                      <p className="mt-1 truncate text-sm text-slate-500">{room.roomTypeName}</p>
                      {isOwner && (
                        <div className="mt-3 flex gap-3 text-xs font-medium">
                          <button type="button" className="text-brand-700 hover:underline" onClick={() => setEditing(room)}>
                            Düzenle
                          </button>
                          {room.isActive && (
                            <button
                              type="button"
                              className="text-red-600 hover:underline"
                              onClick={() => {
                                if (confirm(`Oda ${room.roomNumber} pasife alınsın mı?`)) deactivate.mutate(room.id)
                              }}
                            >
                              Pasife al
                            </button>
                          )}
                        </div>
                      )}
                    </Card>
                  ))}
                </div>
              </section>
            ))}
        </div>
      )}

      {editing !== undefined && <RoomFormModal room={editing} onClose={() => setEditing(undefined)} />}
    </>
  )
}
