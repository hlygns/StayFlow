import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { roomTypesApi } from '../../api/endpoints'
import type { RoomType } from '../../api/types'
import { useAuth } from '../../auth/AuthContext'
import { PlusIcon } from '../../components/icons'
import { Button } from '../../components/ui/Button'
import { Card, EmptyState, ErrorAlert, PageHeader } from '../../components/ui/Card'
import { PageLoader } from '../../components/ui/Spinner'
import { formatMoney } from '../../lib/format'
import { useApiMutation } from '../../lib/useApiMutation'
import { RoomTypeFormModal } from './RoomTypeFormModal'
import { SeasonalPrices } from './SeasonalPrices'

export function RoomTypesPage() {
  const { isOwner } = useAuth()
  const { data: roomTypes, isLoading, error } = useQuery({ queryKey: ['room-types'], queryFn: roomTypesApi.list })

  // undefined: modal kapali, null: yeni kayit, RoomType: duzenleme
  const [editing, setEditing] = useState<RoomType | null | undefined>(undefined)
  const [openPrices, setOpenPrices] = useState<string | null>(null)

  const remove = useApiMutation(roomTypesApi.remove, { invalidate: [['room-types']], successMessage: 'Oda tipi silindi.' })

  const addButton = isOwner && (
    <Button onClick={() => setEditing(null)}>
      <PlusIcon className="size-4" />
      Oda tipi ekle
    </Button>
  )

  return (
    <>
      <PageHeader
        title="Oda tipleri ve fiyatlar"
        description="Kapasite, gecelik baz fiyat ve dönemsel (sezon) fiyatlar."
        actions={addButton}
      />

      {isLoading ? (
        <PageLoader />
      ) : error ? (
        <ErrorAlert error={error} />
      ) : roomTypes!.length === 0 ? (
        <Card>
          <EmptyState
            title="Henüz oda tipi yok"
            description="Önce “Standart”, “Deniz Manzaralı” gibi oda tiplerini ekleyin, ardından odalarınızı bu tiplere bağlayın."
            action={addButton}
          />
        </Card>
      ) : (
        <div className="grid items-start gap-4 lg:grid-cols-2">
          {roomTypes!.map((roomType) => (
            <Card key={roomType.id} className="flex flex-col">
              <div className="flex flex-1 flex-col gap-4 p-5">
                <div className="flex items-start justify-between gap-4">
                  <div className="min-w-0">
                    <h2 className="font-semibold text-slate-900">{roomType.name}</h2>
                    {roomType.description && <p className="mt-0.5 text-sm text-slate-500">{roomType.description}</p>}
                  </div>
                  <div className="shrink-0 text-right">
                    <p className="text-lg font-semibold text-slate-900 tabular-nums">{formatMoney(roomType.basePrice)}</p>
                    <p className="text-xs text-slate-500">gecelik baz fiyat</p>
                  </div>
                </div>
                <dl className="flex gap-6 text-sm">
                  <div>
                    <dt className="text-slate-500">Kapasite</dt>
                    <dd className="font-medium text-slate-900">{roomType.capacity} kişi</dd>
                  </div>
                  <div>
                    <dt className="text-slate-500">Aktif oda</dt>
                    <dd className="font-medium text-slate-900">{roomType.roomCount}</dd>
                  </div>
                </dl>

                {openPrices === roomType.id && <SeasonalPrices roomType={roomType} canEdit={isOwner} />}
              </div>

              <div className="flex flex-wrap items-center gap-2 border-t border-slate-100 px-5 py-3">
                <Button variant="ghost" size="sm" onClick={() => setOpenPrices(openPrices === roomType.id ? null : roomType.id)} aria-expanded={openPrices === roomType.id}>
                  {openPrices === roomType.id ? 'Sezon fiyatlarını gizle' : 'Sezon fiyatları'}
                </Button>
                {isOwner && (
                  <div className="ml-auto flex gap-2">
                    <Button variant="secondary" size="sm" onClick={() => setEditing(roomType)}>
                      Düzenle
                    </Button>
                    <Button
                      variant="danger"
                      size="sm"
                      loading={remove.isPending && remove.variables === roomType.id}
                      onClick={() => {
                        if (confirm(`“${roomType.name}” silinsin mi?`)) remove.mutate(roomType.id)
                      }}
                    >
                      Sil
                    </Button>
                  </div>
                )}
              </div>
            </Card>
          ))}
        </div>
      )}

      {editing !== undefined && <RoomTypeFormModal roomType={editing} onClose={() => setEditing(undefined)} />}
    </>
  )
}
