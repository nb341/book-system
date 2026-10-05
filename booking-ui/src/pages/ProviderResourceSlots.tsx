import { useCallback, useState, type SyntheticEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ApiError, asApiError, fieldMessages } from '../api/client.ts'
import { createSlot, deleteSlot, listMyResources, listMySlots } from '../api/provider.ts'
import { ErrorBanner } from '../components/ErrorBanner.tsx'
import { FieldErrors } from '../components/FieldErrors.tsx'
import { formatDateRange, formatMoney } from '../format.ts'
import { useLoad } from '../hooks/useLoad.ts'

export function ProviderResourceSlots() {
  const { id = '' } = useParams()
  // Remount per resource so form/load state never leaks between resources.
  return <SlotsView key={id} resourceId={id} />
}

function SlotsView({ resourceId }: { resourceId: string }) {
  const loadSlots = useCallback((signal: AbortSignal) => listMySlots(resourceId, signal), [resourceId])
  const slots = useLoad(loadSlots)
  const resources = useLoad(listMyResources)
  const resource = resources.data?.find((r) => r.id === resourceId)

  const [start, setStart] = useState('')
  const [end, setEnd] = useState('')
  const [price, setPrice] = useState('')
  const [formError, setFormError] = useState<ApiError | null>(null)
  const [localError, setLocalError] = useState<string | null>(null)
  const [isPending, setIsPending] = useState(false)
  const [deleteError, setDeleteError] = useState<string | null>(null)
  const [deletingId, setDeletingId] = useState<string | null>(null)

  if (slots.error?.status === 404) {
    return (
      <section>
        <h1>Resource not found</h1>
        <Link to="/provider/resources">Back to my resources</Link>
      </section>
    )
  }

  async function handleSubmit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault()
    if (isPending) return
    setFormError(null)
    setLocalError(null)
    const startDate = new Date(start)
    const endDate = new Date(end)
    const dollars = Number(price)
    if (Number.isNaN(startDate.getTime()) || Number.isNaN(endDate.getTime())) {
      setLocalError('Start and end times are required.')
      return
    }
    if (endDate <= startDate) {
      setLocalError('End time must be after start time.')
      return
    }
    if (price.trim() === '' || !Number.isFinite(dollars) || dollars < 0) {
      setLocalError('Price must be zero or more.')
      return
    }
    setIsPending(true)
    try {
      await createSlot(resourceId, {
        startUtc: startDate.toISOString(),
        endUtc: endDate.toISOString(),
        priceCents: Math.round(dollars * 100),
      })
      setStart('')
      setEnd('')
      setPrice('')
      slots.reload()
    } catch (caught) {
      setFormError(asApiError(caught))
    } finally {
      setIsPending(false)
    }
  }

  async function handleDelete(slotId: string) {
    if (deletingId) return
    if (!window.confirm('Delete this slot?')) return
    setDeleteError(null)
    setDeletingId(slotId)
    try {
      await deleteSlot(slotId)
      slots.reload()
    } catch (caught) {
      const error = asApiError(caught)
      setDeleteError(error.status === 409 ? 'This slot has bookings and cannot be deleted.' : error.message)
    } finally {
      setDeletingId(null)
    }
  }

  const isOverlap = formError?.status === 409
  const hasFieldErrors = formError !== null && Object.keys(formError.fieldErrors).length > 0
  const startErrors = fieldMessages(formError, 'startUtc')
  const endErrors = fieldMessages(formError, 'endUtc')
  const priceErrors = fieldMessages(formError, 'priceCents')
  const banner =
    localError ??
    (isOverlap ? 'This slot overlaps an existing slot.' : null) ??
    (formError && !hasFieldErrors ? formError.message : null)

  return (
    <section>
      <p><Link to="/provider/resources">&larr; My resources</Link></p>
      <h1>{resource?.name ?? 'Resource'}</h1>
      {resource?.description && <p>{resource.description}</p>}

      <h2>Slots</h2>
      {slots.isLoading && <p className="status" role="status">Loading...</p>}
      <ErrorBanner message={slots.error?.message ?? null} />
      <ErrorBanner message={deleteError} />
      {slots.data && slots.data.length === 0 && <p className="status">No slots yet. Add one below.</p>}
      {slots.data && slots.data.length > 0 && (
        <ul className="card-list">
          {slots.data.map((slot) => (
            <li key={slot.id}>
              <strong>{formatDateRange(slot.startUtc, slot.endUtc)}</strong>
              <span>{formatMoney(slot.priceCents)}</span>
              {slot.isBooked && <span className="role-badge">Booked</span>}
              <button
                type="button"
                className="secondary"
                disabled={slot.isBooked || deletingId !== null}
                onClick={() => handleDelete(slot.id)}
              >
                {deletingId === slot.id ? 'Deleting...' : 'Delete'}
              </button>
            </li>
          ))}
        </ul>
      )}

      <h2>Add slot</h2>
      <form onSubmit={handleSubmit} noValidate>
        <ErrorBanner message={banner} />
        <label>
          Start
          <input
            type="datetime-local"
            required
            value={start}
            onChange={(e) => setStart(e.target.value)}
            aria-invalid={startErrors.length > 0}
          />
        </label>
        <FieldErrors messages={startErrors} />
        <label>
          End
          <input
            type="datetime-local"
            required
            value={end}
            onChange={(e) => setEnd(e.target.value)}
            aria-invalid={endErrors.length > 0}
          />
        </label>
        <FieldErrors messages={endErrors} />
        <label>
          Price (USD)
          <input
            type="number"
            required
            min="0"
            step="0.01"
            inputMode="decimal"
            value={price}
            onChange={(e) => setPrice(e.target.value)}
            aria-invalid={priceErrors.length > 0}
          />
        </label>
        <FieldErrors messages={priceErrors} />
        <button type="submit" disabled={isPending}>{isPending ? 'Adding...' : 'Add slot'}</button>
      </form>
    </section>
  )
}
