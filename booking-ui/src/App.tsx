import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthProvider.tsx'
import { homePathForRole } from './auth/AuthContext.ts'
import { RequireRole } from './auth/RequireRole.tsx'
import { useAuth } from './auth/useAuth.ts'
import { Layout } from './components/Layout.tsx'
import { Login } from './pages/Login.tsx'
import { Placeholder } from './pages/Placeholder.tsx'
import { Register } from './pages/Register.tsx'

function HomeRedirect() {
  const { user, isLoading } = useAuth()
  if (isLoading) return <p className="status" role="status">Loading...</p>
  return <Navigate to={user ? homePathForRole(user.role) : '/login'} replace />
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/register" element={<Register />} />
          <Route element={<Layout />}>
            <Route element={<RequireRole role="Customer" />}>
              <Route path="/resources" element={<Placeholder title="Resources" />} />
              <Route path="/bookings" element={<Placeholder title="My Bookings" />} />
            </Route>
            <Route element={<RequireRole role="Provider" />}>
              <Route path="/provider/resources" element={<Placeholder title="My Resources" />} />
              <Route path="/provider/bookings" element={<Placeholder title="Bookings" />} />
            </Route>
          </Route>
          <Route path="*" element={<HomeRedirect />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}
