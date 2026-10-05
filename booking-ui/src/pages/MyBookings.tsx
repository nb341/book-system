import { useState } from 'react'
import { Link } from 'react-router-dom'
import { listMyBookings } from '../api/bookings.ts'
import type { Booking, BookingStatus } from '../api/types.ts'
import { ErrorBanner } from '../components/ErrorBanner.tsx'
import { useLoad } from '../hooks/useLoad.ts'
import { formatDateRange, formatMoney } from '../format.ts'

const STATUS_LABELS: Record<BookingStatus, string> = {
  Confirmed: 'Confirmed',
  Pending: 'Pending',
  PaymentFailed: 'Payment failed',
  Cancelled: 'Cancelled',
}

export function MyBookings() {
  const { data: bookings, error, isLoading, reload } = useLoad(listMyBookings)

  // Fixed at mount so render stays pure; reload remounts nothing, which is fine for upcoming/past.
  const [now] = useState(() => new Date().toISOString())
  const upcoming = (bookings ?? [])
    .filter((b) => b.endUtc >= now)
    .sort((a, b) => a.startUtc.localeCompare(b.startUtc))
  const past = (bookings ?? [])
    .filter((b) => b.endUtc < now)
    .sort((a, b) => b.startUtc.localeCompare(a.startUtc))

  return (
    <section>
      <h1>My bookings</h1>
      {isLoading && <p className="status" role="status">Loading...</p>}
      <ErrorBanner message={error?.message ?? null} />
      {error && <button type="button" className="secondary" onClick={reload}>Retry</button>}
      {bookings && bookings.length === 0 && (
        <p className="status">You have no bookings yet. <Link to="/resources">Browse resources</Link></p>
      )}
      <BookingGroup title="Upcoming" bookings={upcoming} />
      <BookingGroup title="Past" bookings={past} />
    </section>
  )
}

function BookingGroup({ title, bookings }: { title: string; bookings: Booking[] }) {
  if (bookings.length === 0) return null
  return (
    <>
      <h2>{title}</h2>
      <ul className="card-list">
        {bookings.map((booking) => (
          <li key={booking.id}>
            <h3>{booking.resourceName}</h3>
            <span>{formatDateRange(booking.startUtc, booking.endUtc)}</span>
            <strong>{formatMoney(booking.amountCents)}</strong>
            <span className={`badge badge-${booking.status.toLowerCase()}`}>{STATUS_LABELS[booking.status]}</span>
            {/* Cancel/reschedule buttons go in this container later. */}
            <div className="booking-actions" />
          </li>
        ))}
      </ul>
    </>
  )
}
