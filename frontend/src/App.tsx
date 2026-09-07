import { useEffect, useState } from 'react'
import { List, X } from '@phosphor-icons/react'
import { NavLink, Navigate, Route, Routes, useLocation, useNavigate } from './router'
import { EmergencyPage } from './pages/EmergencyPage'
import { JourneyDetailPage } from './pages/JourneyDetailPage'
import { JourneysPage } from './pages/JourneysPage'
import { LandingPage } from './pages/LandingPage'
import { LoginPage } from './pages/LoginPage'
import { RequestDetailPage } from './pages/RequestDetailPage'
import { RequestFormPage } from './pages/RequestFormPage'
import { RequestsPage } from './pages/RequestsPage'
import { TenantConfigPage } from './pages/TenantConfigPage'
import { IdentityPage } from './pages/IdentityPage'
import { CoordinationPage } from './pages/CoordinationPage'
import { VehiclesPage } from './pages/VehiclesPage'
import { PatientsPage } from './pages/PatientsPage'
import { HelpChatWidget } from './components/HelpChatWidget'
import { isAuthenticated, logout, onboardingRequired } from './auth'
import { useIsMobile } from './hooks/useIsMobile'
import { OnboardingPage } from './pages/OnboardingPage'
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
  const isMobile = useIsMobile()
  const [session, setSession] = useState(() => isAuthenticated())
  const [roles, setRoles] = useState<string[]>(() =>
    JSON.parse(sessionStorage.getItem('nagomi_roles') ?? '[]') as string[])
  const [mobileNavOpen, setMobileNavOpen] = useState(false)

  useEffect(() => {
    setSession(isAuthenticated())
    setRoles(JSON.parse(sessionStorage.getItem('nagomi_roles') ?? '[]') as string[])
    // Close the mobile drawer on navigation.
    setMobileNavOpen(false)
  }, [location])

  // Body scroll lock while the mobile drawer is open.
  useEffect(() => {
    if (!mobileNavOpen) return
    const previous = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => { document.body.style.overflow = previous }
  }, [mobileNavOpen])

  const handleLogout = () => {
    logout()
    setSession(false)
    setRoles([])
    setMobileNavOpen(false)
    navigate('/login', { replace: true })
  }

  // The bootstrap admin (admin / Admin) must complete onboarding before using the app.
  if (session && onboardingRequired()) {
    return <OnboardingPage />
  }

  const navItems = (
    <>
      <NavLink to="/trayectos">Operación</NavLink>
      <NavLink to="/coordinacion">Coordinación</NavLink>
      <NavLink to="/solicitudes">Solicitudes</NavLink>
      <NavLink to="/urgencias">Urgencias</NavLink>
      {roles.includes('admin') && <NavLink to="/vehiculos">Vehículos</NavLink>}
      {roles.includes('admin') && <NavLink to="/pacientes">Pacientes</NavLink>}
      {roles.includes('admin') && <NavLink to="/identidad">Identidad</NavLink>}
      {roles.includes('admin') && <NavLink to="/configuracion">Configuración</NavLink>}
    </>
  )

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
            <nav aria-label="Navegación principal">{navItems}</nav>
            {isMobile && (
              <button
                type="button"
                className="mobile-menu-button"
                onClick={() => setMobileNavOpen((value) => !value)}
                aria-label={mobileNavOpen ? 'Cerrar menú' : 'Abrir menú'}
                aria-expanded={mobileNavOpen}
              >
                {mobileNavOpen ? <X size={22} weight="bold" /> : <List size={22} weight="bold" />}
              </button>
            )}
            <NavLink className="button button-accent new-request" to="/solicitudes/nueva">Nueva solicitud</NavLink>
            <button className="button button-small logout-button" onClick={handleLogout}>Salir</button>
          </>
        )}
      </header>
      {session && mobileNavOpen && (
        <>
          <div className="mobile-scrim" onClick={() => setMobileNavOpen(false)} />
          <nav className={`mobile-drawer${mobileNavOpen ? ' open' : ''}`} aria-label="Navegación móvil">
            <div className="mobile-drawer-head">
              <span className="brand-mark" aria-hidden="true">N</span>
              <strong>Nagomi</strong>
            </div>
            {navItems}
            <div className="mobile-drawer-actions">
              <NavLink className="button button-accent" to="/solicitudes/nueva">Nueva solicitud</NavLink>
              <button className="button button-secondary" onClick={handleLogout}>Salir</button>
            </div>
          </nav>
        </>
      )}
      <main id="contenido">
        <div className="route-stage" key={location}>
          <Routes>
            <Route path="/" element={session ? <Navigate to="/trayectos" replace /> : <LandingPage />} />
            <Route path="/login" element={<LoginPage />} />
            <Route path="/trayectos" element={<RequireAuth><JourneysPage /></RequireAuth>} />
            <Route path="/trayectos/:journeyId" element={<RequireAuth><JourneyDetailPage /></RequireAuth>} />
            <Route path="/coordinacion" element={<RequireAuth><CoordinationPage /></RequireAuth>} />
            <Route path="/vehiculos" element={<RequireAuth><RequireAdmin><VehiclesPage /></RequireAdmin></RequireAuth>} />
            <Route path="/pacientes" element={<RequireAuth><RequireAdmin><PatientsPage /></RequireAdmin></RequireAuth>} />
            <Route path="/solicitudes" element={<RequireAuth><RequestsPage /></RequireAuth>} />
            <Route path="/solicitudes/nueva" element={<RequireAuth><RequestFormPage /></RequireAuth>} />
            <Route path="/solicitudes/:requestId" element={<RequireAuth><RequestDetailPage /></RequireAuth>} />
            <Route path="/urgencias" element={<RequireAuth><EmergencyPage /></RequireAuth>} />
            <Route path="/identidad" element={<RequireAuth><RequireAdmin><IdentityPage /></RequireAdmin></RequireAuth>} />
            <Route path="/configuracion" element={<RequireAuth><RequireAdmin><TenantConfigPage /></RequireAdmin></RequireAuth>} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </div>
      </main>
      {session && <HelpChatWidget />}
    </div>
  )
}
