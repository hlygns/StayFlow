import { lazy, Suspense, type ReactNode } from 'react'
import { Navigate, Route, Routes } from 'react-router'
import { RequireAuth, RequireOwner } from './auth/RequireAuth'
import { Layout } from './components/Layout'
import { PageLoader } from './components/ui/Spinner'
import { LoginPage } from './pages/auth/LoginPage'
import { RegisterPage } from './pages/auth/RegisterPage'

// Panel sayfalari ayri parcalar (chunk) halinde, ilk girildiklerinde yuklenir:
// giris ekrani tum paneli indirmeden acilir.
const DashboardPage = lazy(() => import('./pages/DashboardPage').then((m) => ({ default: m.DashboardPage })))
const CalendarPage = lazy(() => import('./pages/calendar/CalendarPage').then((m) => ({ default: m.CalendarPage })))
const ReservationsPage = lazy(() => import('./pages/reservations/ReservationsPage').then((m) => ({ default: m.ReservationsPage })))
const NewReservationPage = lazy(() => import('./pages/reservations/NewReservationPage').then((m) => ({ default: m.NewReservationPage })))
const RoomTypesPage = lazy(() => import('./pages/room-types/RoomTypesPage').then((m) => ({ default: m.RoomTypesPage })))
const RoomsPage = lazy(() => import('./pages/rooms/RoomsPage').then((m) => ({ default: m.RoomsPage })))
const ReviewsPage = lazy(() => import('./pages/reviews/ReviewsPage').then((m) => ({ default: m.ReviewsPage })))
const StaffPage = lazy(() => import('./pages/staff/StaffPage').then((m) => ({ default: m.StaffPage })))
const SettingsPage = lazy(() => import('./pages/settings/SettingsPage').then((m) => ({ default: m.SettingsPage })))

const page = (element: ReactNode) => <Suspense fallback={<PageLoader />}>{element}</Suspense>

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route
        element={
          <RequireAuth>
            <Layout />
          </RequireAuth>
        }
      >
        <Route index element={page(<DashboardPage />)} />
        <Route path="calendar" element={page(<CalendarPage />)} />
        <Route path="reservations" element={page(<ReservationsPage />)} />
        <Route path="reservations/new" element={page(<NewReservationPage />)} />
        <Route path="reviews" element={page(<ReviewsPage />)} />
        <Route path="room-types" element={page(<RoomTypesPage />)} />
        <Route path="rooms" element={page(<RoomsPage />)} />
        <Route path="staff" element={<RequireOwner>{page(<StaffPage />)}</RequireOwner>} />
        <Route path="settings" element={<RequireOwner>{page(<SettingsPage />)}</RequireOwner>} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
