import { NavLink, Navigate, Route, Routes } from './router'
import { EmergencyPage } from './pages/EmergencyPage'
import { JourneyDetailPage } from './pages/JourneyDetailPage'
import { JourneysPage } from './pages/JourneysPage'
import { RequestDetailPage } from './pages/RequestDetailPage'
import { RequestFormPage } from './pages/RequestFormPage'
import { RequestsPage } from './pages/RequestsPage'
import './App.css'

export default function App() {
  return (
    <div className="app-shell">
      <a className="skip-link" href="#contenido">Saltar al contenido</a>
      <header className="topbar">
        <NavLink className="brand" to="/trayectos" aria-label="Nagomi, inicio">
          <span className="brand-mark" aria-hidden="true">N</span>
          <span><strong>Nagomi</strong><small>Coordinación de transporte</small></span>
        </NavLink>
        <nav aria-label="Navegación principal">
          <NavLink to="/trayectos">Operación</NavLink>
          <NavLink to="/solicitudes">Solicitudes</NavLink>
          <NavLink to="/urgencias">Urgencias</NavLink>
        </nav>
        <NavLink className="button button-accent new-request" to="/solicitudes/nueva">Nueva solicitud</NavLink>
      </header>
      <main id="contenido">
        <Routes>
          <Route path="/" element={<Navigate to="/trayectos" replace />} />
          <Route path="/trayectos" element={<JourneysPage />} />
          <Route path="/trayectos/:journeyId" element={<JourneyDetailPage />} />
          <Route path="/solicitudes" element={<RequestsPage />} />
          <Route path="/solicitudes/nueva" element={<RequestFormPage />} />
          <Route path="/solicitudes/:requestId" element={<RequestDetailPage />} />
          <Route path="/urgencias" element={<EmergencyPage />} />
          <Route path="*" element={<Navigate to="/trayectos" replace />} />
        </Routes>
      </main>
    </div>
  )
}
