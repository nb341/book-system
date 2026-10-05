import { request } from './client.ts'
import type { Booking, CreateBookingRequest } from './types.ts'

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
