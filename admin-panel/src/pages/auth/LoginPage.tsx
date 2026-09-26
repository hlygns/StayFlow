import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Link, Navigate, useLocation, useNavigate } from 'react-router'
import { z } from 'zod'
import { useAuth } from '../../auth/AuthContext'
import { Button } from '../../components/ui/Button'
import { ErrorAlert } from '../../components/ui/Card'
import { Input } from '../../components/ui/Field'
import { AuthShell } from './AuthShell'

const schema = z.object({
  email: z.email('Geçerli bir e-posta girin.'),
  password: z.string().min(1, 'Şifre gerekli.'),
})
type FormValues = z.infer<typeof schema>

export function LoginPage() {
  const { user, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const from = (location.state as { from?: string } | null)?.from ?? '/'

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  if (user) return <Navigate to={from} replace />

  const onSubmit = handleSubmit(async ({ email, password }) => {
    try {
      await login(email, password)
      navigate(from, { replace: true })
    } catch (error) {
      setError('root', { message: error instanceof Error ? error.message : 'Giriş yapılamadı.' })
    }
  })

  return (
    <AuthShell
      title="Giriş yap"
      subtitle="Otelinizin yönetim paneline hoş geldiniz."
      footer={
        <>
          Oteliniz henüz kayıtlı değil mi?{' '}
          <Link to="/register" className="font-semibold text-white underline-offset-2 hover:underline">
            Ücretsiz kaydolun
          </Link>
        </>
      }
    >
      <form onSubmit={onSubmit} className="space-y-4" noValidate>
        {errors.root && <ErrorAlert error={new Error(errors.root.message)} />}
        <Input label="E-posta" type="email" autoComplete="email" error={errors.email?.message} {...register('email')} />
        <Input label="Şifre" type="password" autoComplete="current-password" error={errors.password?.message} {...register('password')} />
        <Button type="submit" loading={isSubmitting} className="w-full">
          Giriş yap
        </Button>
      </form>
    </AuthShell>
  )
}
