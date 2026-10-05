import { useState, type SyntheticEvent } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { ApiError, asApiError, fieldMessages } from '../api/client.ts'
import { homePathForRole } from '../auth/AuthContext.ts'
import { useAuth } from '../auth/useAuth.ts'
import { ErrorBanner } from '../components/ErrorBanner.tsx'
import { FieldErrors } from '../components/FieldErrors.tsx'

export function Login() {
  const { user, login } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<ApiError | null>(null)
  const [isPending, setIsPending] = useState(false)

  if (user) return <Navigate to={homePathForRole(user.role)} replace />

  async function handleSubmit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault()
    if (isPending) return
    setIsPending(true)
    setError(null)
    try {
      const signedIn = await login({ email: email.trim(), password })
      navigate(homePathForRole(signedIn.role), { replace: true })
    } catch (caught) {
      setError(asApiError(caught))
      setIsPending(false)
    }
  }

  const hasFieldErrors = error !== null && Object.keys(error.fieldErrors).length > 0
  const emailErrors = fieldMessages(error, 'email')
  const passwordErrors = fieldMessages(error, 'password')

  return (
    <div className="auth-card">
      <h1>Log in</h1>
      <form onSubmit={handleSubmit} noValidate>
        <ErrorBanner message={error && !hasFieldErrors ? error.message : null} />
        <label>
          Email
          <input
            type="email"
            autoComplete="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            aria-invalid={emailErrors.length > 0}
          />
        </label>
        <FieldErrors messages={emailErrors} />
        <label>
          Password
          <input
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            aria-invalid={passwordErrors.length > 0}
          />
        </label>
        <FieldErrors messages={passwordErrors} />
        <button type="submit" disabled={isPending}>{isPending ? 'Logging in...' : 'Log in'}</button>
      </form>
      <p>No account? <Link to="/register">Register</Link></p>
    </div>
  )
}
