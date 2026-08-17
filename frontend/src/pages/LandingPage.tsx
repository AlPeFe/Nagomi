import { Link } from '../router'

const features = [
  {
    icon: '🚑',
    title: 'Solicitudes de transporte',
    text: 'Crea traslados puntuales o recurrentes con paciente, ruta, necesidades y programación. Catálogo nacional de hospitales y geografía de España.',
  },
  {
    icon: '🗺️',
    title: 'Trayectos y seguimiento',
    text: 'Cada ida y vuelta es un trayecto independiente con su estado, historial y geolocalización. Ventana operativa con actualización automática.',
  },
  {
    icon: '🚚',
    title: 'Coordinación de flota',
    text: 'Panel de trabajo actual, hoy y mañana. Asigna vehículos a cada trayecto y sigue su evolución en un mapa en tiempo real.',
  },
  {
    icon: '🆘',
    title: 'Urgencias geolocalizadas',
    text: 'Registra traslados de emergencia con el punto de incidencia marcado en el mapa, prioridad y seguimiento sobre la operación diaria.',
  },
  {
    icon: '🤝',
    title: 'Integración con proveedores',
    text: 'Publica a contratos externos por RabbitMQ + REST autenticado, con outbox transaccional y reintentos. Datos sensibles nunca van al broker.',
  },
  {
    icon: '🧾',
    title: 'Clientes facturables',
    text: 'Factura a organizaciones o particulares (p. ej. mutuas) de forma independiente de quién ejecuta el traslado. La ejecución y la facturación son ortogonales.',
  },
  {
    icon: '💬',
    title: 'Ayuda integrada',
    text: 'Asistente de ayuda en la aplicación para que el equipo resuelva dudas sin salir del panel.',
  },
  {
    icon: '🔐',
    title: 'Identidad y acceso',
    text: 'Usuarios y clientes de API sobre OpenIddict, con roles, rotación de secretos y revocación. Solo administradores.',
  },
]

const steps = [
  { n: '01', title: 'Crea la solicitud', text: 'Rellena paciente, ruta, necesidades y programación, o guárdala como borrador.' },
  { n: '02', title: 'Coordina la flota', text: 'Asigna vehículos en el panel de coordinación y sigue cada trayecto en el mapa.' },
  { n: '03', title: 'Ejecuta y factura', text: 'Marca estados, integra proveedores o tu propia flota, y factura a quien corresponda.' },
]

const audiences = [
  { title: 'Hospitales y clínicas', text: 'Solicitan y coordinan traslados para citas, transferencias entre centros y tratamientos.' },
  { title: 'Residencias y centros geriátricos', text: 'Gestionan la movilidad frecuente y recurrente de sus residentes con rutinas de diálisis y rehabilitación.' },
  { title: 'Empresas de ambulancias', text: 'Ejecutan sus propios traslados con flota propia, coordinación y app de conductor.' },
]

export function LandingPage() {
  const logged = !!localStorage.getItem('nagomi_token')
  return (
    <div className="landing">
      {/* HERO */}
      <section className="landing-hero">
        <div className="landing-hero-inner">
          <span className="brand-mark brand-mark-large" aria-hidden="true">N</span>
          <h1>Coordinación de transporte sanitario, de principio a fin</h1>
          <p className="landing-tagline">
            Nagomi planifica, coordina y sigue los traslados de pacientes de tu
            organización: solicitudes, trayectos, urgencias y flota en un solo sitio.
          </p>
          <div className="landing-actions">
            {logged ? (
              <Link className="button button-accent" to="/trayectos">Ir a la operación</Link>
            ) : (
              <>
                <Link className="button button-accent" to="/login">Entrar</Link>
                <Link className="button button-landing-ghost" to="/trayectos">Ver la operación</Link>
              </>
            )}
          </div>
        </div>
      </section>

      {/* AUDIENCES */}
      <section className="landing-section landing-audiences">
        <div className="landing-section-inner">
          <p className="eyebrow">A quién va dirigido</p>
          <h2>Diseñado para equipos sanitarios que mueven pacientes cada día</h2>
          <div className="landing-grid landing-grid-3">
            {audiences.map((a) => (
              <article key={a.title} className="landing-card">
                <h3>{a.title}</h3>
                <p>{a.text}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      {/* FEATURES */}
      <section className="landing-section landing-features-section">
        <div className="landing-section-inner">
          <p className="eyebrow">Funcionalidades</p>
          <h2>Todo lo que necesitas para la operación diaria</h2>
          <div className="landing-grid landing-grid-4">
            {features.map((f) => (
              <article key={f.title} className="landing-card landing-feature-card">
                <span className="landing-feature-icon" aria-hidden="true">{f.icon}</span>
                <h3>{f.title}</h3>
                <p>{f.text}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      {/* HOW IT WORKS */}
      <section className="landing-section landing-how">
        <div className="landing-section-inner">
          <p className="eyebrow">Cómo funciona</p>
          <h2>Del aviso a la factura en tres pasos</h2>
          <div className="landing-grid landing-grid-3">
            {steps.map((s) => (
              <article key={s.n} className="landing-step">
                <span className="landing-step-n">{s.n}</span>
                <h3>{s.title}</h3>
                <p>{s.text}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      {/* CTA */}
      <section className="landing-cta">
        <div className="landing-cta-inner">
          <h2>Listo para coordinar tu transporte sanitario</h2>
          <p>Entra con tu cuenta para empezar a operar hoy mismo.</p>
          <div className="landing-actions">
            {logged ? (
              <Link className="button button-accent" to="/trayectos">Abrir Nagomi</Link>
            ) : (
              <Link className="button button-accent" to="/login">Entrar</Link>
            )}
          </div>
        </div>
      </section>

      {/* FOOTER */}
      <footer className="landing-footer">
        <span>Nagomi · Coordinación de transporte sanitario de código abierto</span>
        <a href="https://github.com/AlPeFe/Nagomi" target="_blank" rel="noreferrer">GitHub</a>
      </footer>
    </div>
  )
}
