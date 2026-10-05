import { useState, type SyntheticEvent } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { ApiError, fieldMessages } from '../api/client.ts'
import type { Role } from '../api/types.ts'
import { homePathForRole } from '../auth/AuthContext.ts'
import { useAuth } from '../auth/useAuth.ts'
import { ErrorBanner } from '../components/ErrorBanner.tsx'
import { FieldErrors } from '../components/FieldErrors.tsx'

const MIN_PASSWORD_LENGTH = 8

export function Register() {
  const { user, register } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [name, setName] = useState('')
  const [role, setRole] = useState<Role>('Customer')
  const [error, setError] = useState<ApiError | null>(null)
  const [localPasswordError, setLocalPasswordError] = useState<string | null>(null)
  const [isPending, setIsPending] = useState(false)

  if (user) return <Navigate to={homePathForRole(user.role)} replace />

  async function handleSubmit(event: SyntheticEvent<HTMLFormElement>) {
    event.preventDefault()
    if (isPending) return
    setError(null)
    if (password.length < MIN_PASSWORD_LENGTH) {
      setLocalPasswordError(`Password must be at least ${MIN_PASSWORD_LENGTH} characters.`)
      return
    }
    setLocalPasswordError(null)
    setIsPending(true)
    try {
      const created = await register({ email: email.trim(), password, name: name.trim(), role })
      navigate(homePathForRole(created.role), { replace: true })
    } catch (caught) {
      setError(caught instanceof ApiError ? caught : new ApiError(0, 'Unexpected error', 'Something went wrong.'))
      setIsPending(false)
    }
  }

  const hasFieldErrors = error !== null && Object.keys(error.fieldErrors).length > 0
  // A 409 means the email is taken; show it next to the email field.
  const emailErrors = [
    ...fieldMessages(error, 'email'),
    ...(error?.status === 409 ? [error.message] : []),
  ]
  const passwordErrors = [
    ...(localPasswordError ? [localPasswordError] : []),
    ...fieldMessages(error, 'password'),
  ]
  const nameErrors = fieldMessages(error, 'name')
  const roleErrors = fieldMessages(error, 'role')
  const bannerMessage = error && !hasFieldErrors && error.status !== 409 ? error.message : null

  return (
    <div className="auth-card">
      <h1>Create account</h1>
      <form onSubmit={handleSubmit} noValidate>
        <ErrorBanner message={bannerMessage} />
        <label>
          Name
          <input
            type="text"
            autoComplete="name"
            required
            value={name}
            onChange={(e) => setName(e.target.value)}
            aria-invalid={nameErrors.length > 0}
          />
        </label>
        <FieldErrors messages={nameErrors} />
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
            autoComplete="new-password"
            required
            minLength={MIN_PASSWORD_LENGTH}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            aria-invalid={passwordErrors.length > 0}
          />
        </label>
        <FieldErrors messages={passwordErrors} />
        <label>
          I am a
          <select value={role} onChange={(e) => setRole(e.target.value as Role)}>
            <option value="Customer">Customer</option>
            <option value="Provider">Provider</option>
          </select>
        </label>
        <FieldErrors messages={roleErrors} />
        <button type="submit" disabled={isPending}>{isPending ? 'Creating account...' : 'Register'}</button>
      </form>
      <p>Already registered? <Link to="/login">Log in</Link></p>
    </div>
  )
}
