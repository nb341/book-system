import { Navigate, Outlet, useLocation } from 'react-router-dom'
import type { Role } from '../api/types.ts'
import { homePathForRole } from './AuthContext.ts'
import { useAuth } from './useAuth.ts'

/** Route guard: unauthenticated -> /login, wrong role -> that role's home. */
export function RequireRole({ role }: { role: Role }) {
  const { user, isLoading } = useAuth()
  const location = useLocation()

  if (isLoading) return <p className="status" role="status">Loading...</p>
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  if (user.role !== role) return <Navigate to={homePathForRole(user.role)} replace />
  return <Outlet />
}
