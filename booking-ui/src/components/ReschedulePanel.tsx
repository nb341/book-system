import { useCallback, useState } from 'react'
import { rescheduleBooking } from '../api/bookings.ts'
import { asApiError } from '../api/client.ts'
import { listAvailableSlots } from '../api/resources.ts'
import type { Booking } from '../api/types.ts'
import { useLoad } from '../hooks/useLoad.ts'
import { formatDay, formatMoney, formatTimeRange, localDayKey } from '../format.ts'
import { ErrorBanner } from './ErrorBanner.tsx'

interface Props {
  booking: Booking
  onDone: (message: string) => void
  onClose: () => void
}

export function ReschedulePanel({ booking, onDone, onClose }: Props) {
  const load = useCallback(
    (signal: AbortSignal) => listAvailableSlots(booking.resourceId, { from: new Date().toISOString() }, signal),
    [booking.resourceId],
  )
  const { data, error, isLoading, reload } = useLoad(load)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [confirming, setConfirming] = useState(false)
  const [isPending, setIsPending] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  const slots = (data ?? [])
    .filter((s) => s.id !== booking.slotId)
    .sort((a, b) => a.startUtc.localeCompare(b.startUtc))
  const selected = slots.find((s) => s.id === selectedId) ?? null

  const days = new Map<string, typeof slots>()
  for (const slot of slots) {
    const key = localDayKey(slot.startUtc)
    days.set(key, [...(days.get(key) ?? []), slot])
  }

  async function submitReschedule() {
    if (!selected || isPending) return
    setMessage(null)
    setIsPending(true)
    try {
      await rescheduleBooking(booking.id, selected.id)
      onDone('Booking rescheduled.')
    } catch (caught) {
      const err = asApiError(caught)
      setConfirming(false)
      if (err.status === 409) {
        setMessage('That slot was just taken — pick another.')
        setSelectedId(null)
        reload()
      } else {
        setMessage(err.message)
      }
      setIsPending(false)
    }
  }

  return (
    <div className="reschedule-panel" role="region" aria-label="Reschedule booking">
      <strong>Pick a new time for {booking.resourceName}</strong>
      <p className="status">Price differences aren't charged or refunded.</p>
      <ErrorBanner message={message} />
      {isLoading && <p className="status" role="status">Loading slots...</p>}
      <ErrorBanner message={error?.message ?? null} />
      {data && slots.length === 0 && <p className="status">No other available slots.</p>}
      {[...days.entries()].map(([key, daySlots]) => (
        <div key={key}>
          <h4>{formatDay(daySlots[0].startUtc)}</h4>
          <ul>
            {daySlots.map((slot) => (
              <li key={slot.id} className="slot-row">
                <span>{formatTimeRange(slot.startUtc, slot.endUtc)}</span>
                <strong>{formatMoney(slot.priceCents)}</strong>
                <button
                  type="button"
                  className="secondary"
                  disabled={isPending}
                  aria-pressed={selectedId === slot.id}
                  onClick={() => {
                    setSelectedId(slot.id)
                    setConfirming(false)
                  }}
                >
                  {selectedId === slot.id ? 'Selected' : 'Select'}
                </button>
              </li>
            ))}
          </ul>
        </div>
      ))}
      <div className="slot-row">
        {selected && !confirming && (
          <button type="button" onClick={() => setConfirming(true)} disabled={isPending}>Reschedule…</button>
        )}
        {selected && confirming && (
          <>
            <span>Move to {formatDay(selected.startUtc)}, {formatTimeRange(selected.startUtc, selected.endUtc)}?</span>
            <button type="button" onClick={submitReschedule} disabled={isPending}>
              {isPending ? 'Rescheduling...' : 'Confirm'}
            </button>
          </>
        )}
        <button type="button" className="secondary" onClick={onClose} disabled={isPending}>Close</button>
      </div>
    </div>
  )
}
