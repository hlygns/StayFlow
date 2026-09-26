import { PageHeader } from '../components/ui/Card'
import { useAuth } from '../auth/AuthContext'

export function DashboardPage() {
  const { user } = useAuth()
  return <PageHeader title={`Merhaba, ${user?.fullName.split(' ')[0] ?? ''}`} description="Otelinizin bugünkü durumu" />
}
