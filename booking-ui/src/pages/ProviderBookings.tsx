import { useState } from 'react'
import { listProviderBookings } from '../api/provider.ts'
import type { BookingStatus } from '../api/types.ts'
import { ErrorBanner } from '../components/ErrorBanner.tsx'
import { useLoad } from '../hooks/useLoad.ts'
import { formatDateRange, formatMoney } from '../format.ts'

const STATUS_LABELS: Record<BookingStatus, string> = {
  Confirmed: 'Confirmed',
  Pending: 'Pending',
  PaymentFailed: 'Payment failed',
  Cancelled: 'Cancelled',
}

type Filter = 'upcoming' | 'past' | 'all'

export function ProviderBookings() {
  const { data, error, isLoading, reload } = useLoad(listProviderBookings)
  const [filter, setFilter] = useState<Filter>('upcoming')
  const [now] = useState(() => new Date().toISOString())

  const visible = (data ?? [])
    .filter((b) => filter === 'all' || (filter === 'upcoming' ? b.endUtc >= now : b.endUtc < now))
    .sort((a, b) => (filter === 'past' ? b.startUtc.localeCompare(a.startUtc) : a.startUtc.localeCompare(b.startUtc)))

  return (
    <section>
      <h1>Bookings</h1>
      <div className="tabs" role="group" aria-label="Filter bookings">
        {(['upcoming', 'past', 'all'] as const).map((f) => (
          <button key={f} type="button" className="secondary" aria-pressed={filter === f} onClick={() => setFilter(f)}>
            {f[0].toUpperCase() + f.slice(1)}
          </button>
        ))}
      </div>
      {isLoading && <p className="status" role="status">Loading...</p>}
      <ErrorBanner message={error?.message ?? null} />
      {error && <button type="button" className="secondary" onClick={reload}>Retry</button>}
      {data && visible.length === 0 && <p className="status">No {filter === 'all' ? '' : `${filter} `}bookings.</p>}
      {visible.length > 0 && (
        <table className="data-table">
          <thead>
            <tr><th>Resource</th><th>Customer</th><th>When</th><th>Amount</th><th>Status</th></tr>
          </thead>
          <tbody>
            {visible.map((b) => (
              <tr key={b.id}>
                <td>{b.resourceName}</td>
                <td>{b.customerName ?? '-'}</td>
                <td>{formatDateRange(b.startUtc, b.endUtc)}</td>
                <td>{formatMoney(b.amountCents)}</td>
                <td><span className={`badge badge-${b.status.toLowerCase()}`}>{STATUS_LABELS[b.status]}</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}
