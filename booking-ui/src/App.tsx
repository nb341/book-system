import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthProvider.tsx'
import { homePathForRole } from './auth/AuthContext.ts'
import { RequireRole } from './auth/RequireRole.tsx'
import { useAuth } from './auth/useAuth.ts'
import { Layout } from './components/Layout.tsx'
import { Login } from './pages/Login.tsx'
import { MyBookings } from './pages/MyBookings.tsx'
import { Placeholder } from './pages/Placeholder.tsx'
import { ProviderResources } from './pages/ProviderResources.tsx'
import { ProviderResourceSlots } from './pages/ProviderResourceSlots.tsx'
import { Register } from './pages/Register.tsx'
import { ResourceSlots } from './pages/ResourceSlots.tsx'
import { Resources } from './pages/Resources.tsx'

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
              <Route path="/resources" element={<Resources />} />
              <Route path="/resources/:id" element={<ResourceSlots />} />
              <Route path="/bookings" element={<MyBookings />} />
            </Route>
            <Route element={<RequireRole role="Provider" />}>
              <Route path="/provider/resources" element={<ProviderResources />} />
              <Route path="/provider/resources/:id" element={<ProviderResourceSlots />} />
              <Route path="/provider/bookings" element={<Placeholder title="Bookings" />} />
            </Route>
          </Route>
          <Route path="*" element={<HomeRedirect />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}
