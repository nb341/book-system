import { useState, type SyntheticEvent } from 'react'
import { Link } from 'react-router-dom'
import { createBooking } from '../api/bookings.ts'
import { asApiError } from '../api/client.ts'
import type { Booking, Slot } from '../api/types.ts'
import { formatDateRange, formatMoney } from '../format.ts'
import { ErrorBanner } from './ErrorBanner.tsx'

interface CheckoutProps {
  slot: Slot
  resourceName: string
  onClose: () => void
  onSlotsChanged: () => void
}

type Outcome =
  | { kind: 'none' }
  | { kind: 'taken' }
  | { kind: 'declined'; detail: string }
  | { kind: 'error'; message: string }

/** Mounted per slot (keyed by slot id), so the idempotency key is created when checkout opens. */
export function Checkout({ slot, resourceName, onClose, onSlotsChanged }: CheckoutProps) {
  const [cardToken, setCardToken] = useState('tok_visa')
  const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID())
  const [isPending, setIsPending] = useState(false)
  const [outcome, setOutcome] = useState<Outcome>({ kind: 'none' })
  const [booking, setBooking] = useState<Booking | null>(null)

  async function handleSubmit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault()
    if (isPending) return
    setOutcome({ kind: 'none' })
    setIsPending(true)
    try {
      // Same key is reused on network-error retries; only a 402 rotates it.
      const created = await createBooking({ slotId: slot.id, cardToken: cardToken.trim() }, idempotencyKey)
      setBooking(created)
      onSlotsChanged()
    } catch (caught) {
      const error = asApiError(caught)
      if (error.status === 409) {
        setOutcome({ kind: 'taken' })
        onSlotsChanged()
      } else if (error.status === 402) {
        setOutcome({ kind: 'declined', detail: error.detail || error.title })
        setIdempotencyKey(crypto.randomUUID())
      } else {
        setOutcome({ kind: 'error', message: error.message })
      }
    } finally {
      setIsPending(false)
    }
  }

  if (booking) {
    return (
      <div className="checkout" role="status">
        <p>
          Booked! {booking.resourceName}, {formatDateRange(booking.startUtc, booking.endUtc)}.
        </p>
        <Link to="/bookings">View my bookings</Link>{' '}
        <button type="button" className="secondary" onClick={onClose}>Close</button>
      </div>
    )
  }

  if (outcome.kind === 'taken') {
    return (
      <div className="checkout">
        <ErrorBanner message="Sorry, this slot was just taken. Please pick another slot." />
        <button type="button" className="secondary" onClick={onClose}>Close</button>
      </div>
    )
  }

  return (
    <form className="checkout" onSubmit={handleSubmit} aria-label="Checkout">
      <h2>Checkout</h2>
      <p>
        {resourceName}
        <br />
        {formatDateRange(slot.startUtc, slot.endUtc)}
        <br />
        <strong>{formatMoney(slot.priceCents)}</strong>
      </p>
      {outcome.kind === 'declined' && <ErrorBanner message={`Payment failed: ${outcome.detail}`} />}
      {outcome.kind === 'error' && <ErrorBanner message={outcome.message} />}
      <label>
        Card token
        <input
          type="text"
          required
          maxLength={100}
          autoComplete="off"
          value={cardToken}
          onChange={(e) => setCardToken(e.target.value)}
          aria-describedby="card-help"
        />
      </label>
      <p id="card-help" className="status">Use <code>fail</code> to simulate a declined card</p>
      <button type="submit" disabled={isPending || cardToken.trim() === ''}>
        {isPending ? 'Processing...' : `Pay ${formatMoney(slot.priceCents)}`}
      </button>{' '}
      <button type="button" className="secondary" onClick={onClose} disabled={isPending}>Cancel</button>
    </form>
  )
}
