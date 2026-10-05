export type Role = 'Customer' | 'Provider'
export type BookingStatus = 'Pending' | 'Confirmed' | 'PaymentFailed' | 'Cancelled'

export interface User { id: string; email: string; name: string; role: Role }
export interface AuthResponse { token: string; user: User }
export interface RegisterRequest { email: string; password: string; name: string; role: Role }
export interface LoginRequest { email: string; password: string }

export interface Resource { id: string; name: string; description: string; providerName?: string }
export interface CreateResourceRequest { name: string; description: string }

export interface Slot { id: string; startUtc: string; endUtc: string; priceCents: number }
export interface CreateSlotRequest { startUtc: string; endUtc: string; priceCents: number }

export interface Booking {
  id: string
  slotId: string
  resourceName: string
  startUtc: string
  endUtc: string
  status: BookingStatus
  amountCents: number
  customerName?: string // provider view only
}
export interface CreateBookingRequest { slotId: string; cardToken: string }
export interface RescheduleRequest { newSlotId: string }

export interface ProblemDetails {
  type?: string
  title: string
  status: number
  detail?: string
  traceId?: string
  errors?: Record<string, string[]>
}
