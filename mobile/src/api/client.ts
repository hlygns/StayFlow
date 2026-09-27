import type { Availability, GuestReservation, HotelDetail, HotelSummary, IsoDate, ProblemDetails, PublicReview } from './types'

const BASE_URL = process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:8080/api'

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem: ProblemDetails,
  ) {
    super(ApiError.messageFrom(status, problem))
  }

  private static messageFrom(status: number, problem: ProblemDetails) {
    if (problem.detail) return problem.detail
    const first = problem.errors && Object.values(problem.errors).flat()[0]
    if (first) return first
    if (status === 0) return 'Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edin.'
    if (status === 429) return 'Çok fazla istek yapıldı. Lütfen biraz sonra tekrar deneyin.'
    return problem.title ?? 'Bir şeyler ters gitti. Lütfen tekrar deneyin.'
  }
}

async function request<T>(method: 'GET' | 'POST', path: string, body?: unknown): Promise<T> {
  let response: Response
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      method,
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, {})
  }

  if (!response.ok) {
    let problem: ProblemDetails = {}
    try {
      problem = (await response.json()) as ProblemDetails
    } catch {
      // govdesiz hata
    }
    throw new ApiError(response.status, problem)
  }
  return (await response.json()) as T
}

const q = (params: Record<string, string | number | undefined>) => {
  const s = new URLSearchParams()
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== '') s.set(k, String(v))
  const text = s.toString()
  return text ? `?${text}` : ''
}

export interface BookingInput {
  roomTypeId: string
  checkIn: IsoDate
  checkOut: IsoDate
  guestCount: number
  guest: { fullName: string; email: string; phone?: string }
}

export interface PaymentInput {
  email: string
  cardHolder: string
  cardNumber: string
  expiry: string
  cvc: string
}

// Tum uclar backend'deki /api/public altinda: oturum yok, rezervasyona "kod + e-posta" ile erisilir.
// E-posta adres satirinda degil govdede gider (sunucu ve proxy loglarina dusmesin).
export const publicApi = {
  hotels: (city?: string) => request<HotelSummary[]>('GET', `/public/hotels${q({ city })}`),
  hotel: (slug: string) => request<HotelDetail>('GET', `/public/hotels/${slug}`),
  reviews: (slug: string) => request<PublicReview[]>('GET', `/public/hotels/${slug}/reviews`),
  availability: (slug: string, checkIn: IsoDate, checkOut: IsoDate, guests: number) =>
    request<Availability[]>('GET', `/public/hotels/${slug}/availability${q({ checkIn, checkOut, guests })}`),
  book: (slug: string, input: BookingInput) => request<GuestReservation>('POST', `/public/hotels/${slug}/reservations`, input),
  reservation: (code: string, email: string) => request<GuestReservation>('POST', `/public/reservations/${code}`, { email }),
  pay: (code: string, input: PaymentInput) => request<GuestReservation>('POST', `/public/reservations/${code}/pay`, input),
  cancel: (code: string, email: string) => request<GuestReservation>('POST', `/public/reservations/${code}/cancel`, { email }),
  review: (code: string, email: string, rating: number, comment: string) =>
    request<GuestReservation>('POST', `/public/reservations/${code}/review`, { email, rating, comment }),
  registerPush: (code: string, email: string, token: string) =>
    request<unknown>('POST', `/public/reservations/${code}/push-token`, { email, token }),
}
