import { request } from './client.ts'
import type { Resource, Slot } from './types.ts'

export function listResources(signal?: AbortSignal): Promise<Resource[]> {
  return request<Resource[]>('/api/resources', { signal })
}

/** Available slots for a resource; `from`/`to` are ISO UTC strings. */
export function listAvailableSlots(
  resourceId: string,
  range: { from?: string; to?: string } = {},
  signal?: AbortSignal,
): Promise<Slot[]> {
  const params = new URLSearchParams()
  if (range.from) params.set('from', range.from)
  if (range.to) params.set('to', range.to)
  const query = params.toString()
  return request<Slot[]>(
    `/api/resources/${encodeURIComponent(resourceId)}/slots${query ? `?${query}` : ''}`,
    { signal },
  )
}
