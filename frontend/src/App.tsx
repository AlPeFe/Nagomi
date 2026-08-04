import { useEffect, useState } from 'react'
import { NavLink, Navigate, Route, Routes, useLocation, useNavigate } from './router'
import { EmergencyPage } from './pages/EmergencyPage'
import { JourneyDetailPage } from './pages/JourneyDetailPage'
import { JourneysPage } from './pages/JourneysPage'
import { LandingPage } from './pages/LandingPage'
import { LoginPage } from './pages/LoginPage'
import { RequestDetailPage } from './pages/RequestDetailPage'
import { RequestFormPage } from './pages/RequestFormPage'
import { RequestsPage } from './pages/RequestsPage'
import { UsersPage } from './pages/UsersPage'
import { isAuthenticated, logout } from './auth'
import './App.css'

function RequireAuth({ children }: { children: React.ReactNode }) {
  if (!isAuthenticated()) return <Navigate to="/login" replace />
  return <>{children}</>
}

function RequireAdmin({ children }: { children: React.ReactNode }) {
  const roles = JSON.parse(sessionStorage.getItem('nagomi_roles') ?? '[]') as string[]
  if (!roles.includes('admin')) return <Navigate to="/trayectos" replace />
  return <>{children}</>
}

export default function App() {
  const navigate = useNavigate()
  const location = useLocation()
  const [session, setSession] = useState(() => isAuthenticated())
  const [roles, setRoles] = useState<string[]>(() =>
    JSON.parse(sessionStorage.getItem('nagomi_roles') ?? '[]') as string[])

  useEffect(() => {
    setSession(isAuthenticated())
    setRoles(JSON.parse(sessionStorage.getItem('nagomi_roles') ?? '[]') as string[])
  }, [location])

  const handleLogout = () => {
    logout()
    setSession(false)
    setRoles([])
    navigate('/login', { replace: true })
  }

  return (
    <div className="app-shell">
      <a className="skip-link" href="#contenido">Saltar al contenido</a>
      <header className="topbar">
        <NavLink className="brand" to="/" aria-label="Nagomi, inicio">
          <span className="brand-mark" aria-hidden="true">N</span>
          <span><strong>Nagomi</strong><small>Coordinación de transporte</small></span>
        </NavLink>
        {session && (
          <>
            <nav aria-label="Navegación principal">
              <NavLink to="/trayectos">Operación</NavLink>
              <NavLink to="/solicitudes">Solicitudes</NavLink>
              <NavLink to="/urgencias">Urgencias</NavLink>
              {roles.includes('admin') && <NavLink to="/usuarios">Usuarios</NavLink>}
            </nav>
            <NavLink className="button button-accent new-request" to="/solicitudes/nueva">Nueva solicitud</NavLink>
            <button className="button button-small logout-button" onClick={handleLogout}>Salir</button>
          </>
        )}
      </header>
      <main id="contenido">
        <Routes>
          <Route path="/" element={session ? <Navigate to="/trayectos" replace /> : <LandingPage />} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/trayectos" element={<RequireAuth><JourneysPage /></RequireAuth>} />
          <Route path="/trayectos/:journeyId" element={<RequireAuth><JourneyDetailPage /></RequireAuth>} />
          <Route path="/solicitudes" element={<RequireAuth><RequestsPage /></RequireAuth>} />
          <Route path="/solicitudes/nueva" element={<RequireAuth><RequestFormPage /></RequireAuth>} />
          <Route path="/solicitudes/:requestId" element={<RequireAuth><RequestDetailPage /></RequireAuth>} />
          <Route path="/urgencias" element={<RequireAuth><EmergencyPage /></RequireAuth>} />
          <Route path="/usuarios" element={<RequireAuth><RequireAdmin><UsersPage /></RequireAdmin></RequireAuth>} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
    </div>
  )
}
