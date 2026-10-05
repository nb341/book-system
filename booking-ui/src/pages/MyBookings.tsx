import { useState } from 'react'
import { Link } from 'react-router-dom'
import { cancelBooking, listMyBookings } from '../api/bookings.ts'
import { asApiError } from '../api/client.ts'
import type { Booking } from '../api/types.ts'
import { ErrorBanner } from '../components/ErrorBanner.tsx'
import { ReschedulePanel } from '../components/ReschedulePanel.tsx'
import { useLoad } from '../hooks/useLoad.ts'
import { BOOKING_STATUS_LABELS, formatDateRange, formatMoney } from '../format.ts'

export function MyBookings() {
  const { data: bookings, error, isLoading, reload } = useLoad(listMyBookings)
  const [openId, setOpenId] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  // Fixed at mount so render stays pure; reload remounts nothing, which is fine for upcoming/past.
  const [now] = useState(() => new Date().toISOString())
  const upcoming = (bookings ?? [])
    .filter((b) => b.endUtc >= now)
    .sort((a, b) => a.startUtc.localeCompare(b.startUtc))
  const past = (bookings ?? [])
    .filter((b) => b.endUtc < now)
    .sort((a, b) => b.startUtc.localeCompare(a.startUtc))

  function handleDone(message: string) {
    setOpenId(null)
    setNotice(message)
    reload()
  }

  return (
    <section>
      <h1>My bookings</h1>
      {notice && <p className="success-banner" role="status">{notice}</p>}
      {isLoading && <p className="status" role="status">Loading...</p>}
      <ErrorBanner message={error?.message ?? null} />
      {error && <button type="button" className="secondary" onClick={reload}>Retry</button>}
      {bookings && bookings.length === 0 && (
        <p className="status">You have no bookings yet. <Link to="/resources">Browse resources</Link></p>
      )}
      <BookingGroup title="Upcoming" bookings={upcoming} now={now} openId={openId} setOpenId={setOpenId} onDone={handleDone} />
      <BookingGroup title="Past" bookings={past} now={now} openId={openId} setOpenId={setOpenId} onDone={handleDone} />
    </section>
  )
}

interface GroupProps {
  title: string
  bookings: Booking[]
  now: string
  openId: string | null
  setOpenId: (id: string | null) => void
  onDone: (message: string) => void
}

function BookingGroup({ title, bookings, now, openId, setOpenId, onDone }: GroupProps) {
  const [cancelling, setCancelling] = useState<string | null>(null)
  const [cancelError, setCancelError] = useState<{ id: string; message: string } | null>(null)

  async function handleCancel(booking: Booking) {
    if (cancelling) return
    if (!window.confirm('Cancel this booking? A refund will be issued.')) return
    setCancelError(null)
    setCancelling(booking.id)
    try {
      await cancelBooking(booking.id)
      onDone('Booking cancelled.')
    } catch (caught) {
      const err = asApiError(caught)
      setCancelError({ id: booking.id, message: err.message })
    } finally {
      setCancelling(null)
    }
  }

  if (bookings.length === 0) return null
  return (
    <>
      <h2>{title}</h2>
      <ul className="card-list">
        {bookings.map((booking) => {
          const canChange = booking.status === 'Confirmed' && booking.startUtc > now
          return (
            <li key={booking.id}>
              <h3>{booking.resourceName}</h3>
              <span>{formatDateRange(booking.startUtc, booking.endUtc)}</span>
              <strong>{formatMoney(booking.amountCents)}</strong>
              <span className={`badge badge-${booking.status.toLowerCase()}`}>{BOOKING_STATUS_LABELS[booking.status]}</span>
              <div className="booking-actions">
                {canChange && (
                  <>
                    <button
                      type="button"
                      className="secondary"
                      disabled={cancelling !== null}
                      onClick={() => handleCancel(booking)}
                    >
                      {cancelling === booking.id ? 'Cancelling...' : 'Cancel'}
                    </button>{' '}
                    <button
                      type="button"
                      className="secondary"
                      disabled={cancelling !== null}
                      aria-expanded={openId === booking.id}
                      onClick={() => setOpenId(openId === booking.id ? null : booking.id)}
                    >
                      Reschedule
                    </button>
                  </>
                )}
              </div>
              {cancelError?.id === booking.id && <ErrorBanner message={cancelError.message} />}
              {openId === booking.id && (
                <ReschedulePanel booking={booking} onDone={onDone} onClose={() => setOpenId(null)} />
              )}
            </li>
          )
        })}
      </ul>
    </>
  )
}
