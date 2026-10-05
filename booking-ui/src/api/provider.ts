import { request } from './client.ts'
import type { CreateResourceRequest, CreateSlotRequest, Resource, Slot } from './types.ts'

export function listMyResources(signal?: AbortSignal): Promise<Resource[]> {
  return request<Resource[]>('/api/provider/resources', { signal })
}

export function createResource(body: CreateResourceRequest): Promise<Resource> {
  return request<Resource>('/api/provider/resources', { method: 'POST', body })
}

export function listMySlots(resourceId: string, signal?: AbortSignal): Promise<Slot[]> {
  return request<Slot[]>(`/api/provider/resources/${encodeURIComponent(resourceId)}/slots`, { signal })
}

export function createSlot(resourceId: string, body: CreateSlotRequest): Promise<Slot> {
  return request<Slot>(`/api/provider/resources/${encodeURIComponent(resourceId)}/slots`, { method: 'POST', body })
}

export function deleteSlot(slotId: string): Promise<void> {
  return request<void>(`/api/provider/slots/${encodeURIComponent(slotId)}`, { method: 'DELETE' })
}
