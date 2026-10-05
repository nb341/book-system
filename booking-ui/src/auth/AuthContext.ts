import { createContext } from 'react'
import type { LoginRequest, RegisterRequest, Role, User } from '../api/types.ts'

export interface AuthContextValue {
  user: User | null
  /** True while a stored token is being validated on app load. */
  isLoading: boolean
  login: (data: LoginRequest) => Promise<User>
  register: (data: RegisterRequest) => Promise<User>
  logout: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function homePathForRole(role: Role): string {
  return role === 'Provider' ? '/provider/resources' : '/resources'
}
