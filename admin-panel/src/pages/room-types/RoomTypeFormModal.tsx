import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { roomTypesApi, type RoomTypeInput } from '../../api/endpoints'
import type { RoomType } from '../../api/types'
import { Button } from '../../components/ui/Button'
import { ErrorAlert } from '../../components/ui/Card'
import { Input, Textarea } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { useApiMutation } from '../../lib/useApiMutation'

const schema = z.object({
  name: z.string().trim().min(1, 'Ad gerekli.').max(100),
  description: z.string().trim().max(1000),
  capacity: z.number('Kişi sayısı girin.').int().min(1, 'En az 1 kişi.').max(20, 'En fazla 20 kişi.'),
  basePrice: z.number('Fiyat girin.').positive('Fiyat 0\'dan büyük olmalı.').max(1_000_000),
})

interface Props {
  /** null: yeni kayit, dolu: duzenleme */
  roomType: RoomType | null
  onClose: () => void
}

export function RoomTypeFormModal({ roomType, onClose }: Props) {
  const isEdit = roomType !== null
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<RoomTypeInput>({
    resolver: zodResolver(schema),
    defaultValues: roomType ?? { name: '', description: '', capacity: 2, basePrice: undefined },
  })

  const save = useApiMutation(
    (input: RoomTypeInput) => (isEdit ? roomTypesApi.update(roomType.id, input) : roomTypesApi.create(input)),
    {
      invalidate: [['room-types']],
      successMessage: isEdit ? 'Oda tipi güncellendi.' : 'Oda tipi eklendi.',
      onSuccess: onClose,
      silentError: true,
    },
  )

  return (
    <Modal
      open
      title={isEdit ? 'Oda tipini düzenle' : 'Yeni oda tipi'}
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Vazgeç
          </Button>
          <Button type="submit" form="room-type-form" loading={save.isPending}>
            Kaydet
          </Button>
        </>
      }
    >
      <form id="room-type-form" onSubmit={handleSubmit((values) => save.mutate(values))} className="space-y-4" noValidate>
        <ErrorAlert error={save.error} />
        <Input label="Ad" placeholder="Deniz Manzaralı" error={errors.name?.message} {...register('name')} />
        <Textarea label="Açıklama" placeholder="Balkonlu, deniz manzaralı oda" error={errors.description?.message} {...register('description')} />
        <div className="grid grid-cols-2 gap-4">
          <Input label="Kapasite (kişi)" type="number" min={1} max={20} error={errors.capacity?.message} {...register('capacity', { valueAsNumber: true })} />
          <Input
            label="Gecelik baz fiyat (₺)"
            type="number"
            min={1}
            step="0.01"
            hint="Sezon fiyatı yoksa bu kullanılır."
            error={errors.basePrice?.message}
            {...register('basePrice', { valueAsNumber: true })}
          />
        </div>
      </form>
    </Modal>
  )
}
