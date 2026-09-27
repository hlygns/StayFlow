import { markChangedLocally } from '../realtime/localChanges'
import { api, qs } from './client'
import type {
  AuthResponse,
  Hotel,
  IsoDate,
  Reservation,
  ReservationStatus,
  Review,
  ReviewInsights,
  ReviewSummary,
  Room,
  RoomType,
  RoomTypeAvailability,
  SeasonalPrice,
  User,
  UserRole,
} from './types'

// Her backend endpoint'i icin tek bir tipli fonksiyon. Sayfalar URL bilmez, sadece bunlari cagirir.

export interface RegisterInput {
  hotelName: string
  city: string
  address?: string
  phone?: string
  hotelEmail?: string
  ownerFullName: string
  ownerEmail: string
  password: string
}

export const authApi = {
  register: (input: RegisterInput) => api.post<AuthResponse>('/auth/register', input),
  login: (email: string, password: string) => api.post<AuthResponse>('/auth/login', { email, password }),
  logout: () => api.post<void>('/auth/logout'),
}

// Bos e-posta null gitmeli: backend'deki [EmailAddress] "" degerini gecersiz sayar, null'i kabul eder.
export type HotelInput = Omit<Hotel, 'id' | 'slug' | 'email'> & { email: string | null }

export const hotelApi = {
  get: () => api.get<Hotel>('/hotel'),
  update: (input: HotelInput) => api.put<Hotel>('/hotel', input),
}

export interface UserInput {
  fullName: string
  email: string
  password: string
  role: UserRole
}

export const usersApi = {
  list: () => api.get<User[]>('/users'),
  create: (input: UserInput) => api.post<User>('/users', input),
}

export type RoomTypeInput = Pick<RoomType, 'name' | 'description' | 'capacity' | 'basePrice'>
export type SeasonalPriceInput = Pick<SeasonalPrice, 'startDate' | 'endDate' | 'pricePerNight'>

export const roomTypesApi = {
  list: () => api.get<RoomType[]>('/room-types'),
  create: (input: RoomTypeInput) => api.post<RoomType>('/room-types', input),
  update: (id: string, input: RoomTypeInput) => api.put<RoomType>(`/room-types/${id}`, input),
  remove: (id: string) => api.delete(`/room-types/${id}`),

  prices: (roomTypeId: string) => api.get<SeasonalPrice[]>(`/room-types/${roomTypeId}/seasonal-prices`),
  createPrice: (roomTypeId: string, input: SeasonalPriceInput) =>
    api.post<SeasonalPrice>(`/room-types/${roomTypeId}/seasonal-prices`, input),
  updatePrice: (roomTypeId: string, id: string, input: SeasonalPriceInput) =>
    api.put<SeasonalPrice>(`/room-types/${roomTypeId}/seasonal-prices/${id}`, input),
  removePrice: (roomTypeId: string, id: string) => api.delete(`/room-types/${roomTypeId}/seasonal-prices/${id}`),
}

export type RoomInput = Pick<Room, 'roomNumber' | 'floor' | 'roomTypeId' | 'isActive'>

export const roomsApi = {
  list: (includeInactive = false) => api.get<Room[]>(`/rooms${qs({ includeInactive: includeInactive ? 'true' : undefined })}`),
  create: (input: RoomInput) => api.post<Room>('/rooms', input),
  update: (id: string, input: RoomInput) => api.put<Room>(`/rooms/${id}`, input),
  deactivate: (id: string) => api.delete(`/rooms/${id}`),
}

export const availabilityApi = {
  search: (checkIn: IsoDate, checkOut: IsoDate, guests?: number) =>
    api.get<RoomTypeAvailability[]>(`/availability${qs({ checkIn, checkOut, guests })}`),
}

export interface ReservationInput {
  roomId: string
  checkIn: IsoDate
  checkOut: IsoDate
  guestCount: number
  guest: { fullName: string; email?: string; phone?: string; identityNumber?: string }
}

export const reservationsApi = {
  list: (filter: { from?: IsoDate; to?: IsoDate; status?: ReservationStatus } = {}) =>
    api.get<Reservation[]>(`/reservations${qs(filter)}`),
  get: (id: string) => api.get<Reservation>(`/reservations/${id}`),
  // Bu sekmedeki degisiklikler isaretlenir: sunucudan gelen canli bildirim ayni degisiklik icin tekrar gosterilmesin
  create: async (input: ReservationInput) => {
    const reservation = await api.post<Reservation>('/reservations', input)
    markChangedLocally(reservation.id)
    return reservation
  },
  confirm: (id: string) => changeStatus(id, 'confirm'),
  checkIn: (id: string) => changeStatus(id, 'check-in'),
  checkOut: (id: string) => changeStatus(id, 'check-out'),
  cancel: (id: string) => changeStatus(id, 'cancel'),
}

export const reviewsApi = {
  list: (filter: { rating?: number; unanswered?: boolean } = {}) =>
    api.get<Review[]>(`/reviews${qs({ rating: filter.rating, unanswered: filter.unanswered ? 'true' : undefined })}`),
  summary: () => api.get<ReviewSummary>('/reviews/summary'),
  reply: (id: string, reply: string) => api.put<Review>(`/reviews/${id}/reply`, { reply }),
  setVisibility: (id: string, isPublished: boolean) => api.put<Review>(`/reviews/${id}/visibility`, { isPublished }),
  aiStatus: () => api.get<{ available: boolean }>('/reviews/ai-status'),
  insights: () => api.get<ReviewInsights>('/reviews/insights'),
  suggestReply: (id: string) => api.post<{ reply: string }>(`/reviews/${id}/suggest-reply`),
}

function changeStatus(id: string, action: 'confirm' | 'check-in' | 'check-out' | 'cancel') {
  markChangedLocally(id)
  return api.post<Reservation>(`/reservations/${id}/${action}`)
}
