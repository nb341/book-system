import { useEffect, useState, type ReactNode } from 'react'
import * as authApi from '../api/auth.ts'
import {
  clearStoredToken,
  getStoredToken,
  setUnauthorizedHandler,
  storeToken,
} from '../api/client.ts'
import type { AuthResponse, LoginRequest, RegisterRequest, User } from '../api/types.ts'
import { AuthContext } from './AuthContext.ts'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [isLoading, setIsLoading] = useState(() => getStoredToken() !== null)

  // Validate a stored token on load; drop it if the server rejects it.
  useEffect(() => {
    if (getStoredToken() === null) return
    const controller = new AbortController()
    authApi
      .getMe(controller.signal)
      .then(setUser)
      .catch(() => {
        // Any failure (401, server down) leaves the user logged out.
        if (!controller.signal.aborted) clearStoredToken()
      })
      .finally(() => {
        if (!controller.signal.aborted) setIsLoading(false)
      })
    return () => controller.abort()
  }, [])

  // Any later 401 on an authenticated request ends the session.
  useEffect(() => {
    setUnauthorizedHandler(() => {
      clearStoredToken()
      setUser(null)
    })
    return () => setUnauthorizedHandler(null)
  }, [])

  function startSession({ token, user: authUser }: AuthResponse): User {
    storeToken(token)
    setUser(authUser)
    return authUser
  }

  async function login(data: LoginRequest): Promise<User> {
    return startSession(await authApi.login(data))
  }

  async function register(data: RegisterRequest): Promise<User> {
    return startSession(await authApi.register(data))
  }

  function logout() {
    clearStoredToken()
    setUser(null)
  }

  return (
    <AuthContext.Provider value={{ user, isLoading, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  )
}
