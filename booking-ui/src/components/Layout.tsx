import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/useAuth.ts'

const NAV_LINKS = {
  Customer: [
    { to: '/resources', label: 'Resources' },
    { to: '/bookings', label: 'My Bookings' },
  ],
  Provider: [
    { to: '/provider/resources', label: 'My Resources' },
    { to: '/provider/bookings', label: 'Bookings' },
  ],
}

export function Layout() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()

  function handleLogout() {
    logout()
    navigate('/login', { replace: true })
  }

  return (
    <>
      <header className="app-header">
        <strong className="brand">Booking</strong>
        {user && (
          <nav aria-label="Main">
            {NAV_LINKS[user.role].map((link) => (
              <NavLink key={link.to} to={link.to}>{link.label}</NavLink>
            ))}
          </nav>
        )}
        {user && (
          <div className="user-box">
            <span>{user.name} <span className="role-badge">{user.role}</span></span>
            <button type="button" className="secondary" onClick={handleLogout}>Log out</button>
          </div>
        )}
      </header>
      <main className="container">
        <Outlet />
      </main>
    </>
  )
}
