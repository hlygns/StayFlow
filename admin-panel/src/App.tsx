import { Navigate, Route, Routes } from 'react-router'
import { RequireAuth, RequireOwner } from './auth/RequireAuth'
import { Layout } from './components/Layout'
import { LoginPage } from './pages/auth/LoginPage'
import { RegisterPage } from './pages/auth/RegisterPage'
import { CalendarPage } from './pages/calendar/CalendarPage'
import { DashboardPage } from './pages/DashboardPage'
import { NewReservationPage } from './pages/reservations/NewReservationPage'
import { ReservationsPage } from './pages/reservations/ReservationsPage'
import { RoomTypesPage } from './pages/room-types/RoomTypesPage'
import { RoomsPage } from './pages/rooms/RoomsPage'
import { SettingsPage } from './pages/settings/SettingsPage'
import { StaffPage } from './pages/staff/StaffPage'

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
        <Route index element={<DashboardPage />} />
        <Route path="calendar" element={<CalendarPage />} />
        <Route path="reservations" element={<ReservationsPage />} />
        <Route path="reservations/new" element={<NewReservationPage />} />
        <Route path="room-types" element={<RoomTypesPage />} />
        <Route path="rooms" element={<RoomsPage />} />
        <Route
          path="staff"
          element={
            <RequireOwner>
              <StaffPage />
            </RequireOwner>
          }
        />
        <Route
          path="settings"
          element={
            <RequireOwner>
              <SettingsPage />
            </RequireOwner>
          }
        />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
