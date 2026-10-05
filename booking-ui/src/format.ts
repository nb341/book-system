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
