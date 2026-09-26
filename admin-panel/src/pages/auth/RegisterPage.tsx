import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Link, Navigate, useNavigate } from 'react-router'
import { z } from 'zod'
import { useAuth } from '../../auth/AuthContext'
import { Button } from '../../components/ui/Button'
import { ErrorAlert } from '../../components/ui/Card'
import { Input } from '../../components/ui/Field'
import { AuthShell } from './AuthShell'

// Kurallar backend'deki RegisterHotelRequest ile ayni: kullanici hatayi sunucuya gitmeden gorsun.
const schema = z.object({
  hotelName: z.string().trim().min(1, 'Otel adı gerekli.').max(200),
  city: z.string().trim().min(1, 'Şehir gerekli.').max(100),
  ownerFullName: z.string().trim().min(1, 'Ad soyad gerekli.').max(200),
  ownerEmail: z.email('Geçerli bir e-posta girin.'),
  password: z.string().min(8, 'Şifre en az 8 karakter olmalı.'),
})
type FormValues = z.infer<typeof schema>

export function RegisterPage() {
  const { user, register: registerHotel } = useAuth()
  const navigate = useNavigate()

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  if (user) return <Navigate to="/" replace />

  const onSubmit = handleSubmit(async (values) => {
    try {
      await registerHotel(values)
      // Yeni otel bos: ilk is oda tiplerini tanimlamak.
      navigate('/room-types', { replace: true })
    } catch (error) {
      setError('root', { message: error instanceof Error ? error.message : 'Kayıt yapılamadı.' })
    }
  })

  return (
    <AuthShell
      title="Otelinizi kaydedin"
      subtitle="Birkaç dakikada odalarınızı ve rezervasyonlarınızı yönetmeye başlayın."
      footer={
        <>
          Zaten hesabınız var mı?{' '}
          <Link to="/login" className="font-semibold text-white underline-offset-2 hover:underline">
            Giriş yapın
          </Link>
        </>
      }
    >
      <form onSubmit={onSubmit} className="space-y-4" noValidate>
        {errors.root && <ErrorAlert error={new Error(errors.root.message)} />}
        <div className="grid gap-4 sm:grid-cols-2">
          <Input label="Otel adı" error={errors.hotelName?.message} {...register('hotelName')} />
          <Input label="Şehir" placeholder="Antalya" error={errors.city?.message} {...register('city')} />
        </div>
        <Input label="Adınız soyadınız" autoComplete="name" error={errors.ownerFullName?.message} {...register('ownerFullName')} />
        <Input label="E-posta" type="email" autoComplete="email" error={errors.ownerEmail?.message} {...register('ownerEmail')} />
        <Input
          label="Şifre"
          type="password"
          autoComplete="new-password"
          hint="En az 8 karakter."
          error={errors.password?.message}
          {...register('password')}
        />
        <Button type="submit" loading={isSubmitting} className="w-full">
          Kaydol ve başla
        </Button>
      </form>
    </AuthShell>
  )
}
