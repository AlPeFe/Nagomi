import { useEffect, useEffectEvent, useState } from 'react'
import { api } from '../api'
import { EmptyState, ErrorState, LoadingState, PageHeader } from '../components/States'
import { VEHICLE_TYPE_LABELS, type CollectiveRoute, type CoordinationRow, type Vehicle } from '../types'
import { directionLabel, formatDateTime } from '../utils'

const statusLabel: Record<string, string> = {
  Planned: 'Planificada',
  InProgress: 'En curso',
  Completed: 'Completada',
  Cancelled: 'Cancelada',
}

/** Página de rutas colectivas: agrupa varios trayectos del día en un mismo vehículo. */
export function RoutesPage() {
  const [routes, setRoutes] = useState<CollectiveRoute[]>([])
  const [coordination, setCoordination] = useState<CoordinationRow[]>([])
  const [vehicles, setVehicles] = useState<Vehicle[]>([])
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10))
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [notes, setNotes] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [creating, setCreating] = useState(false)

  async function load(silent = false) {
    if (!silent) setLoading(true)
    try {
      const [routeList, coord, fleet] = await Promise.all([
        api.listRoutes(date),
        api.listCoordination(),
        api.listVehicles(),
      ])
      setRoutes(routeList)
      setCoordination(coord.filter((row) => row.operationalAt.slice(0, 10) === date))
      setVehicles(fleet)
      setError('')
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Error desconocido.')
    } finally {
      setLoading(false)
    }
  }
  const loadEffect = useEffectEvent(load)
  useEffect(() => { void loadEffect() }, [date])

  function toggleJourney(journeyId: string) {
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(journeyId)) next.delete(journeyId)
      else next.add(journeyId)
      return next
    })
  }

  async function createRoute() {
    if (selected.size < 2) {
      setMessage('Selecciona al menos dos trayectos del día para crear una ruta colectiva.')
      return
    }
    setCreating(true)
    setMessage('')
    try {
      await api.createRoute({ serviceDate: date, journeyIds: [...selected], notes: notes.trim() || undefined })
      setSelected(new Set())
      setNotes('')
      setMessage('Ruta colectiva creada.')
      await load(true)
    } catch (caught) {
      setMessage(caught instanceof Error ? caught.message : 'No se pudo crear la ruta.')
    } finally {
      setCreating(false)
    }
  }

  async function completeRoute(id: string) {
    try { await api.completeRoute(id); setMessage('Ruta completada.'); await load(true) }
    catch (caught) { setMessage(caught instanceof Error ? caught.message : 'No se pudo completar.') }
  }

  async function assignVehicle(routeId: string, vehicleId: string) {
    try {
      await api.assignRouteVehicle(routeId, vehicleId || undefined)
      await load(true)
    } catch (caught) {
      setMessage(caught instanceof Error ? caught.message : 'No se pudo asignar el vehículo.')
    }
  }

  const groupedIds = new Set(routes.filter((r) => r.status !== 'Cancelled').flatMap((r) => r.stops.map((s) => s.journeyId)))
  const available = coordination.filter((row) => !groupedIds.has(row.journeyId))

  if (loading && routes.length === 0) return <div className="page wide-page"><LoadingState label="Cargando rutas" /></div>
  if (error && routes.length === 0) return <div className="page wide-page"><ErrorState message={error} retry={() => void load()} /></div>

  return <div className="page wide-page">
    <PageHeader eyebrow="Colectivos" title="Rutas del día" description="Agrupa varios trayectos en un mismo vehículo (traslado colectivo). Los trayectos individuales siguen funcionando igual y no necesitan ruta." />
    {message && <div className="inline-message" role="status">{message}</div>}

    <section className="card config-card route-create">
      <h2>Nueva ruta colectiva</h2>
      <div className="route-create-row">
        <label><span>Fecha</span><input type="date" value={date} onChange={(e) => setDate(e.target.value)} /></label>
        <label><span>Notas</span><input value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Ej.: recogidas zona norte (opcional)" /></label>
        <button className="button button-accent" onClick={() => void createRoute()} disabled={creating || selected.size < 2}>
          {creating ? 'Creando…' : `Crear ruta (${selected.size} seleccionados)`}
        </button>
      </div>
      {available.length === 0
        ? <p className="empty-hint">No hay trayectos sin agrupar para esta fecha. Crea solicitudes para hoy o revisa otra fecha.</p>
        : <ul className="route-journey-pick">
            {available.map((row) => (
              <li key={row.journeyId}>
                <label className="check-line">
                  <input type="checkbox" checked={selected.has(row.journeyId)} onChange={() => toggleJourney(row.journeyId)} />
                  <span><strong>{row.patientName || 'Sin identificar'}</strong> · {row.origin} → {row.destination} · {directionLabel(row.direction)} · {formatDateTime(row.operationalAt)}</span>
                </label>
              </li>
            ))}
          </ul>}
    </section>

    {routes.length === 0
      ? <EmptyState title="Sin rutas" message="Crea una ruta agrupando trayectos del día para el traslado colectivo." />
      : <div className="route-list">
          {routes.map((route) => (
            <article key={route.id} className="card config-card route-card">
              <header>
                <strong>{route.publicId}</strong>
                <span className={`route-status route-status-${route.status.toLowerCase()}`}>{statusLabel[route.status]}</span>
              </header>
              <div className="route-meta">
                <label><span>Vehículo</span>
                  <select value={route.vehicleId ?? ''} onChange={(e) => void assignVehicle(route.id, e.target.value)} disabled={route.status === 'Completed' || route.status === 'Cancelled'}>
                    <option value="">Sin asignar</option>
                    {vehicles.map((vehicle) => <option key={vehicle.id} value={vehicle.id}>{vehicle.name} ({VEHICLE_TYPE_LABELS[vehicle.vehicleType]})</option>)}
                  </select>
                </label>
                <span>{route.stops.length} pacientes · {route.driverName ? `Conductor: ${route.driverName}` : 'Sin conductor'}</span>
                {route.status !== 'Completed' && route.status !== 'Cancelled' && (
                  <button className="button button-small" onClick={() => void completeRoute(route.id)}>Completar ruta</button>
                )}
              </div>
              <ol className="route-stops">
                {route.stops.map((stop) => (
                  <li key={stop.journeyId}><span className="route-stop-order">{stop.order}</span><span><strong>{stop.patientName || 'Sin identificar'}</strong><small>{stop.origin} → {stop.destination} · {stop.journeyPublicId}</small></span></li>
                ))}
              </ol>
            </article>
          ))}
        </div>}
  </div>
}
