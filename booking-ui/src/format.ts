export function formatMoney(cents: number): string {
  return new Intl.NumberFormat(undefined, { style: 'currency', currency: 'USD' }).format(cents / 100)
}

const dateFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' })
const timeFormat = new Intl.DateTimeFormat(undefined, { timeStyle: 'short' })

/** Formats a UTC ISO string in the viewer's local time zone. */
export function formatDateTime(utcIso: string): string {
  return dateFormat.format(new Date(utcIso))
}

export function formatDateRange(startUtc: string, endUtc: string): string {
  const start = new Date(startUtc)
  const end = new Date(endUtc)
  const sameDay = start.toDateString() === end.toDateString()
  return `${dateFormat.format(start)} - ${sameDay ? timeFormat.format(end) : dateFormat.format(end)}`
}

const dayFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'full' })

/** Local-day heading, e.g. "Monday, October 5, 2026". */
export function formatDay(utcIso: string): string {
  return dayFormat.format(new Date(utcIso))
}

/** Stable key for the viewer's local calendar day. */
export function localDayKey(utcIso: string): string {
  const d = new Date(utcIso)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

export function formatTimeRange(startUtc: string, endUtc: string): string {
  return `${timeFormat.format(new Date(startUtc))} - ${timeFormat.format(new Date(endUtc))}`
}
