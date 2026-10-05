import { request } from './client.ts'
import type { AuthResponse, LoginRequest, RegisterRequest, User } from './types.ts'

export function register(data: RegisterRequest): Promise<AuthResponse> {
  return request<AuthResponse>('/api/auth/register', { method: 'POST', body: data })
}

export function login(data: LoginRequest): Promise<AuthResponse> {
  return request<AuthResponse>('/api/auth/login', { method: 'POST', body: data })
}

export function getMe(signal?: AbortSignal): Promise<User> {
  return request<User>('/api/auth/me', { signal })
}
