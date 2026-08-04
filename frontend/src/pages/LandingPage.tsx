import { Link } from '../router'

export function LandingPage() {
  return (
    <div className="landing">
      <section className="landing-hero">
        <div className="landing-hero-inner">
          <span className="brand-mark brand-mark-large" aria-hidden="true">N</span>
          <h1>Nagomi</h1>
          <p className="landing-tagline">
            Coordinación de transporte sanitario: solicitudes, trayectos y urgencias
            en un solo sitio, con seguimiento de principio a fin.
          </p>
          <div className="landing-actions">
            {localStorage.getItem('nagomi_token') ? (
              <Link className="button button-accent" to="/trayectos">Ir a la operación</Link>
            ) : (
              <>
                <Link className="button button-accent" to="/login">Entrar</Link>
                <Link className="button" to="/trayectos">Ver operación</Link>
              </>
            )}
          </div>
        </div>
      </section>
      <section className="landing-features">
        <div className="landing-feature">
          <h2>Solicitudes</h2>
          <p>Crea solicitudes de transporte con origen, destino, necesidades del paciente y recurrencia.</p>
        </div>
        <div className="landing-feature">
          <h2>Trayectos</h2>
          <p>Consulta el estado de cada trayecto, su historial y la coordinación con el proveedor.</p>
        </div>
        <div className="landing-feature">
          <h2>Urgencias</h2>
          <p>Registra y sigue transportes urgentes con prioridad sobre el resto de la operación.</p>
        </div>
      </section>
    </div>
  )
}
