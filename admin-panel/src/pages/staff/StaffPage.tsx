import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { usersApi, type UserInput } from '../../api/endpoints'
import { useAuth } from '../../auth/AuthContext'
import { PlusIcon } from '../../components/icons'
import { Button } from '../../components/ui/Button'
import { Card, ErrorAlert, PageHeader } from '../../components/ui/Card'
import { Input, Select } from '../../components/ui/Field'
import { Modal } from '../../components/ui/Modal'
import { PageLoader } from '../../components/ui/Spinner'
import { useApiMutation } from '../../lib/useApiMutation'

const schema = z.object({
  fullName: z.string().trim().min(1, 'Ad soyad gerekli.').max(200),
  email: z.email('Geçerli bir e-posta girin.'),
  password: z.string().min(8, 'Şifre en az 8 karakter olmalı.'),
  role: z.enum(['Owner', 'Reception']),
})

const roleLabels = { Owner: 'Otel sahibi', Reception: 'Resepsiyon' } as const

function StaffFormModal({ onClose }: { onClose: () => void }) {
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<UserInput>({ resolver: zodResolver(schema), defaultValues: { fullName: '', email: '', password: '', role: 'Reception' } })

  const create = useApiMutation(usersApi.create, {
    invalidate: [['users']],
    successMessage: (u) => `${u.fullName} eklendi. Giriş bilgilerini kendisiyle paylaşabilirsiniz.`,
    onSuccess: onClose,
    silentError: true,
  })

  return (
    <Modal
      open
      title="Personel ekle"
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Vazgeç
          </Button>
          <Button type="submit" form="staff-form" loading={create.isPending}>
            Ekle
          </Button>
        </>
      }
    >
      <form id="staff-form" onSubmit={handleSubmit((values) => create.mutate(values))} className="space-y-4" noValidate>
        <ErrorAlert error={create.error} />
        <Input label="Ad soyad" autoComplete="off" error={errors.fullName?.message} {...register('fullName')} />
        <Input label="E-posta" type="email" autoComplete="off" error={errors.email?.message} {...register('email')} />
        <Input
          label="Geçici şifre"
          type="text"
          autoComplete="new-password"
          hint="En az 8 karakter. Personel ilk girişte bu şifreyi kullanır."
          error={errors.password?.message}
          {...register('password')}
        />
        <Select label="Rol" error={errors.role?.message} {...register('role')}>
          <option value="Reception">Resepsiyon: rezervasyon işlemleri</option>
          <option value="Owner">Otel sahibi: tüm yetkiler</option>
        </Select>
      </form>
    </Modal>
  )
}

export function StaffPage() {
  const { user } = useAuth()
  const [adding, setAdding] = useState(false)
  const { data: users, isLoading, error } = useQuery({ queryKey: ['users'], queryFn: usersApi.list })

  return (
    <>
      <PageHeader
        title="Personel"
        description="Panele giriş yapabilen kişiler. Resepsiyon rezervasyonları yönetir; oda, fiyat ve personel ayarlarını yalnızca otel sahibi değiştirebilir."
        actions={
          <Button onClick={() => setAdding(true)}>
            <PlusIcon className="size-4" />
            Personel ekle
          </Button>
        }
      />

      {isLoading ? (
        <PageLoader />
      ) : error ? (
        <ErrorAlert error={error} />
      ) : (
        <Card>
          <ul className="divide-y divide-slate-100">
            {users!.map((u) => (
              <li key={u.id} className="flex items-center gap-4 px-5 py-4">
                <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-brand-100 text-sm font-semibold text-brand-800" aria-hidden="true">
                  {u.fullName
                    .split(' ')
                    .map((part) => part[0])
                    .slice(0, 2)
                    .join('')
                    .toLocaleUpperCase('tr')}
                </span>
                <div className="min-w-0 flex-1">
                  <p className="truncate font-medium text-slate-900">
                    {u.fullName}
                    {u.id === user?.id && <span className="ml-2 text-xs font-normal text-slate-500">(siz)</span>}
                  </p>
                  <p className="truncate text-sm text-slate-500">{u.email}</p>
                </div>
                <span className="shrink-0 rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-medium text-slate-700">{roleLabels[u.role]}</span>
              </li>
            ))}
          </ul>
        </Card>
      )}

      {adding && <StaffFormModal onClose={() => setAdding(false)} />}
    </>
  )
}
