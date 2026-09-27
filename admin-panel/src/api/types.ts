// Backend DTO'larinin TypeScript karsiliklari (StayFlow.Application/*).
// Enum'lar backend'de JsonStringEnumConverter ile isim olarak geliyor.

export type UserRole = 'Owner' | 'Reception'

export type ReservationStatus = 'Pending' | 'Confirmed' | 'CheckedIn' | 'CheckedOut' | 'Cancelled'

/** "yyyy-MM-dd" - backend'deki DateOnly */
export type IsoDate = string

export interface User {
  id: string
  fullName: string
  email: string
  role: UserRole
  hotelId: string
}

/** Refresh token govdede yok: backend onu httpOnly cookie olarak gonderir. */
export interface AuthResponse {
  accessToken: string
  accessTokenExpiresAt: string
  user: User
}

export interface Hotel {
  id: string
  name: string
  slug: string
  city: string
  address: string
  phone: string
  email: string
  description: string
}

export interface RoomType {
  id: string
  name: string
  description: string
  capacity: number
  basePrice: number
  roomCount: number
}

export interface Room {
  id: string
  roomNumber: string
  floor: number
  isActive: boolean
  roomTypeId: string
  roomTypeName: string
}

export interface SeasonalPrice {
  id: string
  roomTypeId: string
  startDate: IsoDate
  endDate: IsoDate
  pricePerNight: number
}

export interface AvailableRoom {
  id: string
  roomNumber: string
  floor: number
}

export interface RoomTypeAvailability {
  roomTypeId: string
  roomTypeName: string
  capacity: number
  nights: number
  totalPrice: number
  availableRooms: number
  rooms: AvailableRoom[]
}

export interface Guest {
  id: string
  fullName: string
  email: string | null
  phone: string | null
}

export interface Reservation {
  id: string
  roomId: string
  roomNumber: string
  roomTypeName: string
  checkIn: IsoDate
  checkOut: IsoDate
  nights: number
  guestCount: number
  status: ReservationStatus
  totalPrice: number
  paymentDeadline: string | null
  createdAt: string
  /** Misafirin mobil uygulamada rezervasyonuna eristigi kod */
  confirmationCode: string | null
  guest: Guest
}

/** RFC 7807 - backend'in tum hata cevaplari bu formatta */
export interface ProblemDetails {
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}
