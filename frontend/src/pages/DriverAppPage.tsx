import { useEffect, useEffectEvent, useState } from 'react'
import { ArrowRight, BellRinging, Car, CheckCircle, MapPin, SignOut, UserPlus, X } from '@phosphor-icons/react'
import { api } from '../api'
import { login } from '../auth'
import type { Journey, JourneyStatus, Vehicle } from '../types'
import { directionLabel, formatDateTime, statusLabel } from '../utils'
import { Link } from '../router'
import { StatusBadge } from '../components/Badges'

/**
 * EMULADOR WEB DE LA APP DE CONDUCTOR (placeholder de pruebas).
 *
 * Reproduce el modelo de sesión real: 1 vehículo por dispositivo + N trabajadores a
 * bordo. Al marcar un estado, el `actor` es el primer trabajador activo y el vehículo
 * viaja en `externalResourceCode`, igual que en la app Android. Cerrar el vehículo
 * cierra TODO (trabajadores y sesión), como en la app.
 *
 * Diferencias conscientes con la app real: no hay SignalR (aquí se refresca por
 * sondeo cada 15 s; la app usa /hubs/dispatch con SubscribeVehicle y el sondeo como
 * respaldo) ni almacenamiento seguro de credenciales: es un cliente de pruebas.
 */

type Worker = { code: string; name: string }
type DriverSession = { token: string; user: string; vehicleId?: string; vehicleCode?: string; workers: Worker[] }

const SESSION_KEY = 'nagomi_driver_session'
const POLL_MS = 15_000

function readSession(): DriverSession | null {
  try {
    const raw = localStorage.getItem(SESSION_KEY)
    return raw ? JSON.parse(raw) as DriverSession : null
  } catch { return null }
}

function writeSession(session: DriverSession | null) {
  if (session) localStorage.setItem(SESSION_KEY, JSON.stringify(session))
  else localStorage.removeItem(SESSION_KEY)
}

export function DriverAppPage() {
  const [session, setSession] = useState<DriverSession | null>(() => readSession())
  const [user, setUser] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const [vehicles, setVehicles] = useState<Vehicle[]>([])
  const [journeys, setJourneys] = useState<Journey[]>([])
  const [message, setMessage] = useState('')
  const [refreshedAt, setRefreshedAt] = useState<Date>()
  const [workerCode, setWorkerCode] = useState('')
  const [workerName, setWorkerName] = useState('')

  const activeVehicle = vehicles.find((vehicle) => vehicle.id === session?.vehicleId)
  const actor = session?.workers[0]?.code ?? 'driver-web'

  async function signIn(event: React.FormEvent) {
    event.preventDefault()
    setBusy(true); setError('')
    try {
      await login(user.trim(), password)
      // El token del password grant es el mismo que usa la app para la API y el hub.
      const token = localStorage.getItem('nagomi_token') ?? ''
      const next: DriverSession = { token, user: user.trim(), workers: [] }
      writeSession(next); setSession(next)
      setVehicles(await api.listVehicles())
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo iniciar sesión.')
    } finally { setBusy(false) }
  }

  function startVehicle(vehicleId: string) {
    if (!session) return
    const vehicle = vehicles.find((item) => item.id === vehicleId)
    const next = { ...session, vehicleId: vehicle?.id, vehicleCode: vehicle?.publicId ?? vehicle?.name }
    writeSession(next); setSession(next)
    setMessage(`Vehículo ${vehicle?.name ?? ''} iniciado.`)
  }

  /** Cerrar el vehículo cierra todo: trabajadores incluidos (modelo de la app). */
  function closeVehicle() {
    if (!session) return
    const next = { ...session, vehicleId: undefined, vehicleCode: undefined, workers: [] }
    writeSession(next); setSession(next)
    setJourneys([]); setMessage('Vehículo cerrado: sesión y trabajadores liberados.')
  }

  function addWorker(event: React.FormEvent) {
    event.preventDefault()
    if (!session || !workerCode.trim()) return
    const next = { ...session, workers: [...session.workers, { code: workerCode.trim(), name: workerName.trim() || workerCode.trim() }] }
    writeSession(next); setSession(next)
    setWorkerCode(''); setWorkerName('')
  }

  function removeWorker(code: string) {
    if (!session) return
    const next = { ...session, workers: session.workers.filter((worker) => worker.code !== code) }
    writeSession(next); setSession(next)
  }

  function signOut() {
    writeSession(null)
    localStorage.removeItem('nagomi_token')
    setSession(null); setVehicles([]); setJourneys([])
  }

  /** Traslados de hoy asignados a ESTE vehículo (se filtran en cliente: la API no tiene
   *  endpoint por vehículo y la fila de operaciones ya trae vehicleId). */
  async function load(silent = false) {
    if (!session?.vehicleId) return
    if (!silent) setBusy(true)
    try {
      const today = new Date()
      const iso = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
      const result = await api.listJourneys({ from: iso(today), to: iso(today), status: 'active', provider: '', contract: '', direction: '', reason: '', originMunicipality: '', destinationMunicipality: '', deliveryState: '', search: '' })
      setJourneys(result.items.filter((journey) => journey.vehicleId === session.vehicleId))
      setRefreshedAt(new Date()); setMessage('')
    } catch (e) { setMessage(e instanceof Error ? e.message : 'No se pudieron cargar los traslados.') }
    finally { setBusy(false) }
  }
  const loadEffect = useEffectEvent(load)

  const sessionUser = session?.user ?? ''
  useEffect(() => {
    if (!sessionUser) return
    void api.listVehicles().then(setVehicles).catch(() => setVehicles([]))
  }, [sessionUser])

  useEffect(() => {
    if (!session?.vehicleId) return
    void loadEffect()
    const timer = window.setInterval(() => void loadEffect(true), POLL_MS)
    return () => window.clearInterval(timer)
  }, [session?.vehicleId])

  async function mark(journey: Journey, status: JourneyStatus, withPosition: boolean) {
    setBusy(true)
    try {
      let latitude: number | undefined
      let longitude: number | undefined
      if (withPosition && navigator.geolocation) {
        const position = await new Promise<GeolocationPosition | null>((resolve) => {
          navigator.geolocation.getCurrentPosition(resolve, () => resolve(null), { timeout: 4000 })
        })
        latitude = position?.coords.latitude
        longitude = position?.coords.longitude
      }
      await api.addJourneyStatus(journey.id, status, new Date().toISOString(), crypto.randomUUID(), {
        actor, externalResourceCode: session?.vehicleCode, latitude, longitude,
      })
      setMessage(`${journey.publicId}: ${statusLabel(status)}${latitude ? ' (con posición)' : ''}.`)
      await load(true)
    } catch (e) { setMessage(e instanceof Error ? e.message : 'No se pudo marcar el estado.') }
    finally { setBusy(false) }
  }

  // ------------------------------------------------------------------ login
  if (!session) {
    return <div className="driver-app driver-app-login">
      <form className="driver-login-card" onSubmit={signIn}>
        <span className="brand-mark" aria-hidden="true">N</span>
        <h1>Nagomi Driver <span className="driver-tag">emulador web</span></h1>
        <p className="muted">Inicia sesión con tu cuenta de operador y luego activa el vehículo. La app Android hace lo mismo: el QR de emparejamiento sólo rellena la URL del API.</p>
        {error && <div className="alert alert-error" role="alert">{error}</div>}
        <label className="field"><span>Usuario</span><input value={user} onChange={(e) => setUser(e.target.value)} autoComplete="username" required autoFocus /></label>
        <label className="field"><span>Contraseña</span><input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" required /></label>
        <button className="button button-primary" type="submit" disabled={busy}>{busy ? 'Entrando…' : 'Entrar'}</button>
        <Link className="driver-back" to="/setup-android">Ver el QR de emparejamiento →</Link>
      </form>
    </div>
  }

  // ------------------------------------------------------------------ sesión de vehículo
  if (!session.vehicleId) {
    return <div className="driver-app">
      <header className="driver-head">
        <span className="brand-mark" aria-hidden="true">N</span>
        <div><strong>Nagomi Driver</strong><small>{session.user} · sin vehículo activo</small></div>
        <button className="icon-button" onClick={signOut} aria-label="Salir"><SignOut size={16} aria-hidden="true" /></button>
      </header>
      <main className="driver-body">
        <section className="driver-card">
          <h2><Car size={16} aria-hidden="true" /> Iniciar vehículo</h2>
          <p className="muted">Un dispositivo, un vehículo. Al cerrarlo se cierran también los trabajadores.</p>
          {vehicles.length === 0 && <p className="muted">No hay vehículos activos. Créalos en la web (Vehículos).</p>}
          <ul className="driver-vehicles">
            {vehicles.filter((vehicle) => vehicle.isActive).map((vehicle) => (
              <li key={vehicle.id}>
                <div><strong>{vehicle.name}</strong><small>{vehicle.publicId}{vehicle.externalCode ? ` · ${vehicle.externalCode}` : ''}</small></div>
                <button className="button button-primary button-small" onClick={() => startVehicle(vehicle.id)}>Activar</button>
              </li>
            ))}
          </ul>
        </section>
      </main>
    </div>
  }

  // ------------------------------------------------------------------ trabajo
  return <div className="driver-app">
    <header className="driver-head">
      <span className="brand-mark" aria-hidden="true">N</span>
      <div>
        <strong>{activeVehicle?.name ?? session.vehicleCode}</strong>
        <small>{session.workers.length ? `${session.workers.length} trabajador${session.workers.length > 1 ? 'es' : ''} a bordo · marca como ${actor}` : 'sin trabajadores: marca como driver-web'}</small>
      </div>
      <button className="icon-button" onClick={closeVehicle} aria-label="Cerrar vehículo" title="Cerrar vehículo (libera trabajadores)"><X size={16} aria-hidden="true" /></button>
    </header>

    <main className="driver-body">
      {message && <div className="inline-message" role="status">{message}</div>}

      <section className="driver-card">
        <h2><UserPlus size={16} aria-hidden="true" /> Trabajadores a bordo</h2>
        <ul className="driver-workers">
          {session.workers.map((worker) => (
            <li key={worker.code}><strong>{worker.code}</strong><span>{worker.name}</span>
              <button className="icon-button" onClick={() => removeWorker(worker.code)} aria-label={`Quitar ${worker.code}`}><X size={13} aria-hidden="true" /></button>
            </li>
          ))}
          {!session.workers.length && <li className="muted">Ninguno. Añade al menos uno para atribuir el trabajo.</li>}
        </ul>
        <form className="driver-worker-form" onSubmit={addWorker}>
          <label className="field"><span>Nº trabajador</span><input value={workerCode} onChange={(e) => setWorkerCode(e.target.value)} placeholder="CON-001" /></label>
          <label className="field"><span>Nombre</span><input value={workerName} onChange={(e) => setWorkerName(e.target.value)} placeholder="Jordi R." /></label>
          <button className="button button-secondary button-small" type="submit">Añadir</button>
        </form>
      </section>

      <section className="driver-card">
        <div className="driver-card-head">
          <h2><BellRinging size={16} aria-hidden="true" /> Traslados de hoy</h2>
          <span className="muted">{refreshedAt ? `Actualizado ${refreshedAt.toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })}` : 'Sin actualizar'}</span>
        </div>
        <p className="muted driver-note">
          Sondeo cada 15 s. La app real usa SignalR (<code>/hubs/dispatch</code>, <code>SubscribeVehicle</code>) y este sondeo como respaldo.
        </p>
        {!journeys.length && <p className="muted">No hay traslados activos asignados a este vehículo hoy.</p>}
        <ul className="driver-jobs">
          {journeys.map((journey) => {
            const next: JourneyStatus = journey.status === 'Scheduled' ? 'Activated'
              : journey.status === 'Activated' ? 'EnRouteToOrigin'
              : journey.status === 'EnRouteToOrigin' ? 'ArrivedAtOrigin'
              : journey.status === 'ArrivedAtOrigin' ? 'PatientOnBoard'
              : journey.status === 'PatientOnBoard' ? 'EnRouteToDestination'
              : journey.status === 'EnRouteToDestination' ? 'ArrivedAtDestination' : 'Completed'
            return <li key={journey.id} className="driver-job">
              <header>
                <strong>{journey.publicId}</strong>
                <StatusBadge status={journey.status} />
              </header>
              <p><strong>{directionLabel(journey.direction)}</strong> {journey.origin.name} <ArrowRight size={12} aria-hidden="true" /> {journey.destination.name}</p>
              <p className="muted">{formatDateTime(journey.direction === 'Return' ? journey.scheduledPickupAt : journey.scheduledStartAt)} · {journey.patientName || 'Paciente sin identificar'}</p>
              <div className="driver-job-actions">
                <button className="button button-primary button-small" disabled={busy} onClick={() => void mark(journey, next, true)}>
                  <MapPin size={13} aria-hidden="true" /> {statusLabel(next)} (con posición)
                </button>
                <button className="button button-secondary button-small" disabled={busy} onClick={() => void mark(journey, next, false)}>
                  <CheckCircle size={13} aria-hidden="true" /> Sin posición
                </button>
              </div>
            </li>
          })}
        </ul>
      </section>
    </main>
  </div>
}