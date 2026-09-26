import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
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

// Sadece demo kurulumunda (build sirasinda VITE_DEMO_LOGIN=true) gorunur. Hesap backend'deki
// DemoDataSeeder'in olusturdugu ornek otel; gercek otellerin panelinde bu buton hic derlenmez.
const DEMO_ENABLED = import.meta.env.VITE_DEMO_LOGIN === 'true'
const DEMO_EMAIL = 'demo@stayflow.app'
const DEMO_PASSWORD = 'Demo1234!'

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
  const [demoLoading, setDemoLoading] = useState(false)

  if (user) return <Navigate to={from} replace />

  const signIn = async (email: string, password: string) => {
    try {
      await login(email, password)
      navigate(from, { replace: true })
    } catch (error) {
      setError('root', { message: error instanceof Error ? error.message : 'Giriş yapılamadı.' })
    }
  }

  const onSubmit = handleSubmit(({ email, password }) => signIn(email, password))

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
      {DEMO_ENABLED && (
        <div className="mt-6 rounded-lg bg-brand-50 p-4 text-sm ring-1 ring-brand-100">
          <p className="font-medium text-brand-900">Projeyi incelemek mi istiyorsunuz?</p>
          <p className="mt-1 text-brand-800">Örnek odalar ve rezervasyonlarla dolu bir demo otel hazır.</p>
          <Button variant="secondary" className="mt-3 w-full" disabled={isSubmitting}
            loading={demoLoading}
            onClick={async () => {
              setDemoLoading(true)
              await signIn(DEMO_EMAIL, DEMO_PASSWORD)
              setDemoLoading(false)
            }}
          >
            Demo hesabıyla dene
          </Button>
        </div>
      )}
    </AuthShell>
  )
}
