import { useCallback, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { listAvailableSlots, listResources } from '../api/resources.ts'
import type { Slot } from '../api/types.ts'
import { Checkout } from '../components/Checkout.tsx'
import { ErrorBanner } from '../components/ErrorBanner.tsx'
import { useLoad } from '../hooks/useLoad.ts'
import { formatDay, formatMoney, formatTimeRange, localDayKey } from '../format.ts'

export function ResourceSlots() {
  const { id } = useParams()
  if (!id) return <p className="status">Resource not found.</p>
  return <SlotsView key={id} resourceId={id} />
}

interface DayGroup { key: string; label: string; slots: Slot[] }

function groupByDay(slots: Slot[]): DayGroup[] {
  const sorted = [...slots].sort((a, b) => a.startUtc.localeCompare(b.startUtc))
  const groups: DayGroup[] = []
  for (const slot of sorted) {
    const key = localDayKey(slot.startUtc)
    const last = groups[groups.length - 1]
    if (last && last.key === key) last.slots.push(slot)
    else groups.push({ key, label: formatDay(slot.startUtc), slots: [slot] })
  }
  return groups
}

function SlotsView({ resourceId }: { resourceId: string }) {
  const loadSlots = useCallback((signal: AbortSignal) => listAvailableSlots(resourceId, {}, signal), [resourceId])
  const slots = useLoad(loadSlots)
  const resources = useLoad(listResources)
  const [selected, setSelected] = useState<Slot | null>(null)

  const resource = resources.data?.find((r) => r.id === resourceId)

  if (slots.error?.status === 404) {
    return (
      <section>
        <h1>Resource not found</h1>
        <Link to="/resources">Back to resources</Link>
      </section>
    )
  }

  const groups = slots.data ? groupByDay(slots.data) : []

  return (
    <section>
      <p><Link to="/resources">&larr; All resources</Link></p>
      <h1>{resource?.name ?? 'Resource'}</h1>
      {resource?.description && <p>{resource.description}</p>}
      {resource?.providerName && <p className="status">Provided by {resource.providerName}</p>}

      {selected && (
        <Checkout
          key={selected.id}
          slot={selected}
          resourceName={resource?.name ?? 'Resource'}
          onClose={() => setSelected(null)}
          onSlotsChanged={slots.reload}
        />
      )}

      <h2>Available slots</h2>
      {slots.isLoading && <p className="status" role="status">Loading...</p>}
      <ErrorBanner message={slots.error?.message ?? null} />
      {slots.data && groups.length === 0 && <p className="status">No available slots right now.</p>}
      {groups.map((group) => (
        <div key={group.key}>
          <h3 className="day-heading">{group.label}</h3>
          <ul className="card-list">
            {group.slots.map((slot) => (
              <li key={slot.id}>
                <span>{formatTimeRange(slot.startUtc, slot.endUtc)}</span>
                <strong>{formatMoney(slot.priceCents)}</strong>
                <button
                  type="button"
                  aria-pressed={selected?.id === slot.id}
                  onClick={() => setSelected(slot)}
                >
                  Book
                </button>
              </li>
            ))}
          </ul>
        </div>
      ))}
    </section>
  )
}
