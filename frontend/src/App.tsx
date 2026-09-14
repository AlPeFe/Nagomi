import { useEffect, useState } from 'react'
import {
  Ambulance,
  CaretRight,
  ClockCounterClockwise,
  DeviceMobile,
  FileText,
  Gear,
  Key,
  List,
  MapTrifold,
  Path,
  Plus,
  SidebarSimple,
  SignOut,
  Siren,
  UserList,
  X,
} from '@phosphor-icons/react'
import type { Icon } from '@phosphor-icons/react'
import { Link, NavLink, Navigate, Route, Routes, useLocation, useNavigate } from './router'
import { EmergencyPage } from './pages/EmergencyPage'
import { JourneyDetailPage } from './pages/JourneyDetailPage'
import { HistoryPage } from './pages/HistoryPage'
import { JourneysPage } from './pages/JourneysPage'
import { LandingPage } from './pages/LandingPage'
import { LoginPage } from './pages/LoginPage'
import { RequestDetailPage } from './pages/RequestDetailPage'
import { RequestFormPage } from './pages/RequestFormPage'
import { RequestsPage } from './pages/RequestsPage'
import { TenantConfigPage } from './pages/TenantConfigPage'
import { IdentityPage } from './pages/IdentityPage'

import { VehiclesPage } from './pages/VehiclesPage'
import { PatientsPage } from './pages/PatientsPage'
import { RoutesPage } from './pages/RoutesPage'
import { SetupAndroidPage } from './pages/SetupAndroidPage'
import { HelpChatWidget } from './components/HelpChatWidget'
import { isAuthenticated, logout, onboardingRequired } from './auth'
import { useIsMobile } from './hooks/useIsMobile'
import { OnboardingPage } from './pages/OnboardingPage'
import './App.css'

type NavItem = { to: string; label: string; icon: Icon; admin?: boolean }
type NavSection = { section: string; items: NavItem[] }

const NAV: NavSection[] = [
  {
    section: 'Operación diaria',
    items: [
      { to: '/trayectos', label: 'Operación', icon: MapTrifold },
      { to: '/rutas', label: 'Rutas', icon: Path },
      { to: '/urgencias', label: 'Urgencias', icon: Siren },
    ],
  },
  {
    section: 'Gestión',
    items: [
      { to: '/historico', label: 'Histórico', icon: ClockCounterClockwise },
      { to: '/solicitudes', label: 'Solicitudes', icon: FileText },
      { to: '/pacientes', label: 'Pacientes', icon: UserList, admin: true },
      { to: '/vehiculos', label: 'Vehículos', icon: Ambulance, admin: true },
    ],
  },
  {
    section: 'Administración',
    items: [
      { to: '/setup-android', label: 'App móvil', icon: DeviceMobile },
      { to: '/identidad', label: 'Identidad', icon: Key, admin: true },
      { to: '/configuracion', label: 'Configuración', icon: Gear, admin: true },
    ],
  },
]

/** Topbar breadcrumb: path prefix → [section, page]. Longest prefix wins. */
const CRUMBS: Record<string, [string, string]> = {
  '/trayectos': ['Operación diaria', 'Trayectos'],
  '/historico': ['Gestión', 'Histórico'],
  '/rutas': ['Operación diaria', 'Rutas colectivas'],
  '/urgencias': ['Operación diaria', 'Urgencias'],
  '/solicitudes/nueva': ['Gestión', 'Nueva solicitud'],
  '/solicitudes': ['Gestión', 'Solicitudes'],
  '/pacientes': ['Gestión', 'Pacientes'],
  '/vehiculos': ['Gestión', 'Vehículos'],
  '/setup-android': ['Administración', 'App móvil'],
  '/identidad': ['Administración', 'Identidad'],
  '/configuracion': ['Administración', 'Configuración'],
}

function crumbFor(location: string): [string, string] {
  const path = location.split(/[?#]/, 1)[0].replace(/\/$/, '') || '/'
  const exact = CRUMBS[path]
  if (exact) return exact
  const prefix = Object.keys(CRUMBS)
    .filter((key) => key !== '/solicitudes' && path.startsWith(`${key}/`))
    .sort((a, b) => b.length - a.length)[0]
  if (prefix === '/trayectos') return ['Operación diaria', 'Detalle del trayecto']
  if (prefix === '/solicitudes') return ['Gestión', 'Detalle de la solicitud']
  return prefix ? CRUMBS[prefix] : ['Nagomi', 'Panel']
}

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
  // Menú lateral minimizado a iconos (rail). Se recuerda entre sesiones.
  const [navCollapsed, setNavCollapsed] = useState(() => localStorage.getItem('nagomi_nav_collapsed') === '1')

  function toggleNav() {
    setNavCollapsed((current) => {
      localStorage.setItem('nagomi_nav_collapsed', current ? '0' : '1')
      return !current
    })
  }

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

  const userName = sessionStorage.getItem('nagomi_user') ?? ''
  const userInitials = (userName || 'Sesión').trim().slice(0, 1)
  const roleLabel = roles.includes('admin') ? 'Administrador' : 'Operador'

  const navItems = (
    <>
      {NAV.map((group) => {
        const visible = group.items.filter((item) => !item.admin || roles.includes('admin'))
        if (!visible.length) return null
        return (
          <div key={group.section}>
            <p className="sidebar-section">{group.section}</p>
            {visible.map((item) => (
              <NavLink key={item.to} to={item.to} title={item.label} aria-label={item.label}>
                <item.icon size={16} weight="regular" aria-hidden="true" />
                <span className="nav-label">{item.label}</span>
              </NavLink>
            ))}
          </div>
        )
      })}
    </>
  )

  const routes = (
    <Routes>
      <Route path="/" element={session ? <Navigate to="/trayectos" replace /> : <LandingPage />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/trayectos" element={<RequireAuth><JourneysPage /></RequireAuth>} />
      <Route path="/trayectos/:journeyId" element={<RequireAuth><JourneyDetailPage /></RequireAuth>} />
      <Route path="/historico" element={<RequireAuth><HistoryPage /></RequireAuth>} />
      <Route path="/rutas" element={<RequireAuth><RoutesPage /></RequireAuth>} />
      <Route path="/setup-android" element={<RequireAuth><SetupAndroidPage /></RequireAuth>} />
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
  )

  // ---------------------------------------------------------------- public UI
  if (!session) {
    const bare = location.split(/[?#]/, 1)[0].replace(/\/$/, '') === '/login'
    return (
      <div className="public-shell">
        <a className="skip-link" href="#contenido">Saltar al contenido</a>
        {!bare && (
          <header className="public-head">
            <Link className="brand" to="/" aria-label="Nagomi, inicio">
              <span className="brand-mark" aria-hidden="true">N</span>
              <span><strong>Nagomi</strong><small>Coordinación de transporte sanitario</small></span>
            </Link>
            <div className="public-head-actions">
              <Link className="button button-primary" to="/login">Entrar</Link>
            </div>
          </header>
        )}
        <main id="contenido">{routes}</main>
      </div>
    )
  }

  // --------------------------------------------------------------- app shell
  const [section, page] = crumbFor(location)

  return (
    <div className={`app-shell${navCollapsed ? " nav-collapsed" : ""}`}>
      <a className="skip-link" href="#contenido">Saltar al contenido</a>

      {/*
        The sidebar stays mounted on phones (hidden by CSS) so the current
        section keeps its aria-current link in the DOM — same contract the
        old top nav had. The drawer is the interactive mobile UI.
      */}
      <aside className="sidebar">
        <div className="sidebar-head">
          <NavLink className="brand" to="/trayectos" aria-label="Nagomi, inicio">
            <span className="brand-mark" aria-hidden="true">N</span>
            <span className="nav-label"><strong>Nagomi</strong><small>Transporte sanitario</small></span>
          </NavLink>
          <button
            type="button"
            className="icon-button nav-toggle"
            onClick={toggleNav}
            title={navCollapsed ? 'Expandir el menú' : 'Minimizar el menú'}
            aria-label={navCollapsed ? 'Expandir el menú' : 'Minimizar el menú'}
          >
            <SidebarSimple size={16} aria-hidden="true" />
          </button>
        </div>
        <div className="sidebar-cta">
          <NavLink className="button button-primary" to="/solicitudes/nueva" title="Nueva solicitud">
            <Plus size={14} weight="bold" aria-hidden="true" /><span className="nav-label">Nueva solicitud</span>
          </NavLink>
        </div>
        <nav className="sidebar-nav" aria-label="Navegación principal">{navItems}</nav>
        <div className="sidebar-foot">
          <span className="user-chip">
            <span className="user-avatar" aria-hidden="true">{userInitials}</span>
            <span className="nav-label">
              <strong>{userName || 'Sesión activa'}</strong>
              <small>{roleLabel}</small>
            </span>
          </span>
          <button className="icon-button" onClick={handleLogout} title="Salir" aria-label="Salir">
            <SignOut size={16} aria-hidden="true" />
          </button>
        </div>
      </aside>

      <div className="main-col">
        <header className="topbar">
          {isMobile && (
            <button
              type="button"
              className="mobile-menu-button"
              onClick={() => setMobileNavOpen((value) => !value)}
              aria-label={mobileNavOpen ? 'Cerrar menú' : 'Abrir menú'}
              aria-expanded={mobileNavOpen}
            >
              {mobileNavOpen ? <X size={18} weight="bold" /> : <List size={18} weight="bold" />}
            </button>
          )}
          {isMobile && (
            <NavLink className="brand" to="/trayectos" aria-label="Nagomi, inicio">
              <span className="brand-mark" aria-hidden="true">N</span>
              <strong>Nagomi</strong>
            </NavLink>
          )}
          <nav className="topbar-crumbs" aria-label="Ubicación">
            <span>{section}</span>
            <CaretRight size={11} weight="bold" className="sep" aria-hidden="true" />
            <strong>{page}</strong>
          </nav>
          <div className="topbar-actions">
            {/* On desktop the sidebar already carries the primary CTA. */}
            {isMobile && (
              <NavLink className="button button-primary" to="/solicitudes/nueva">
                <Plus size={14} weight="bold" aria-hidden="true" /> Nueva solicitud
              </NavLink>
            )}
          </div>
        </header>

        <main id="contenido">
          <div className="route-stage" key={location}>{routes}</div>
        </main>
      </div>

      {isMobile && mobileNavOpen && (
        <>
          <div className="mobile-scrim" onClick={() => setMobileNavOpen(false)} />
          <nav className="mobile-drawer open" aria-label="Navegación móvil">
            <div className="mobile-drawer-head">
              <span className="brand-mark" aria-hidden="true">N</span>
              <strong>Nagomi</strong>
            </div>
            <div className="sidebar-cta">
              <NavLink className="button button-primary" to="/solicitudes/nueva">
                <Plus size={14} weight="bold" aria-hidden="true" /> Nueva solicitud
              </NavLink>
            </div>
            {navItems}
            <div className="mobile-drawer-actions">
              <span className="user-chip">
                <span className="user-avatar" aria-hidden="true">{userInitials}</span>
                <span><strong>{userName || 'Sesión activa'}</strong><small>{roleLabel}</small></span>
              </span>
              <button className="button button-secondary" onClick={handleLogout}>Salir</button>
            </div>
          </nav>
        </>
      )}

      <HelpChatWidget />
    </div>
  )
}
