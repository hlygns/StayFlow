// Backend'deki StayFlow.Application.Public DTO'larinin karsiliklari

export type IsoDate = string

export type ReservationStatus = 'Pending' | 'Confirmed' | 'CheckedIn' | 'CheckedOut' | 'Cancelled'

export interface HotelSummary {
  slug: string
  name: string
  city: string
  description: string
  minPricePerNight: number | null
  averageRating: number | null
  reviewCount: number
}

export interface RoomTypeInfo {
  id: string
  name: string
  description: string
  capacity: number
  basePrice: number
}

export interface HotelDetail {
  slug: string
  name: string
  city: string
  address: string
  phone: string
  description: string
  averageRating: number | null
  reviewCount: number
  roomTypes: RoomTypeInfo[]
}

export interface Availability {
  roomTypeId: string
  name: string
  description: string
  capacity: number
  nights: number
  totalPrice: number
  roomsLeft: number
}

export interface PublicReview {
  rating: number
  comment: string
  guestName: string
  createdAt: string
  ownerReply: string | null
}

export interface GuestReservation {
  confirmationCode: string
  status: ReservationStatus
  hotelSlug: string
  hotelName: string
  hotelAddress: string
  hotelPhone: string
  roomTypeName: string
  checkIn: IsoDate
  checkOut: IsoDate
  nights: number
  guestCount: number
  totalPrice: number
  paymentDeadline: string | null
  guestName: string
  canPay: boolean
  canCancel: boolean
  paymentMethod: string | null
  canReview: boolean
  myRating: number | null
}

export interface ProblemDetails {
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}
