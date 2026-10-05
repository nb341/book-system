import { request } from './client.ts'
import type { Booking, CreateBookingRequest, RescheduleRequest } from './types.ts'

export function createBooking(req: CreateBookingRequest, idempotencyKey: string): Promise<Booking> {
  return request<Booking>('/api/bookings', {
    method: 'POST',
    body: req,
    headers: { 'Idempotency-Key': idempotencyKey },
  })
}

export function listMyBookings(signal?: AbortSignal): Promise<Booking[]> {
  return request<Booking[]>('/api/bookings', { signal })
}

export function cancelBooking(id: string): Promise<Booking> {
  return request<Booking>(`/api/bookings/${encodeURIComponent(id)}/cancel`, { method: 'POST' })
}

/** Returns the NEW booking; the old one becomes Cancelled. */
export function rescheduleBooking(id: string, newSlotId: string): Promise<Booking> {
  const body: RescheduleRequest = { newSlotId }
  return request<Booking>(`/api/bookings/${encodeURIComponent(id)}/reschedule`, { method: 'POST', body })
}
