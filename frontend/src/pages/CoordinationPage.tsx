import { useEffect, useEffectEvent, useState } from 'react'
import { Link } from '../router'
import { api } from '../api'
import { StatusMap } from '../components/StatusMap'
import { StatusBadge } from '../components/Badges'
import { ErrorState, LoadingState, PageHeader } from '../components/States'
import type { CoordinationRow, Vehicle } from '../types'
import { directionLabel, formatDateTime } from '../utils'

function bucketOf(row: CoordinationRow, today: string): 'now' | 'today' | 'tomorrow' {
  const day = row.operationalAt.slice(0, 10)
  const tomorrow = new Date(`${today}T00:00:00`)
  tomorrow.setDate(tomorrow.getDate() + 1)
  const tomorrowStr = tomorrow.toISOString().slice(0, 10)
  if (day === today) return 'today'
  if (day === tomorrowStr) return 'tomorrow'
  return 'now'
}

export function CoordinationPage() {
  const [rows, setRows] = useState<CoordinationRow[]>([])
  const [vehicles, setVehicles] = useState<Vehicle[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [refreshedAt, setRefreshedAt] = useState<Date>()

  async function load(silent = false) {
    if (!silent) setLoading(true)
    try {
      const [coordination, fleet] = await Promise.all([api.listCoordination(), api.listVehicles()])
      setRows(coordination); setVehicles(fleet); setError(''); setRefreshedAt(new Date())
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'Error desconocido.') }
    finally { setLoading(false) }
  }
  const loadEffect = useEffectEvent(load)
  useEffect(() => {
    void loadEffect()
    const timer = window.setInterval(() => { void loadEffect(true) }, 30_000)
    return () => window.clearInterval(timer)
  }, [])

  async function assign(journeyId: string, vehicleId: string) {
    await api.assignJourneyVehicle(journeyId, vehicleId || undefined)
    await load(true)
  }

  const today = new Date().toISOString().slice(0, 10)
  const bands: Array<{ key: 'now' | 'today' | 'tomorrow'; title: string }> = [
    { key: 'now', title: 'Trabajo actual' },
    { key: 'today', title: 'Hoy' },
    { key: 'tomorrow', title: 'Mañana' },
  ]

  if (loading && rows.length === 0) return <div className="page wide-page"><LoadingState label="Cargando coordinación" /></div>
  if (error && rows.length === 0) return <div className="page wide-page"><ErrorState message={error} retry={() => void load()} /></div>

  return <div className="page wide-page">
    <PageHeader eyebrow="Coordinación de flota" title="Panel de coordinación" description="Asigna vehículos a los trayectos y sigue su estado. Actualización automática cada 30 segundos." actions={<span className="refresh-note">{refreshedAt ? `Actualizado ${refreshedAt.toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })}` : 'Sin actualizar'}</span>} />
    <div className="coordination-board">
      {bands.map((band) => {
        const items = rows.filter((row) => bucketOf(row, today) === band.key)
        return <section key={band.key} className="coordination-column">
          <h2>{band.title}<span className="count-badge">{items.length}</span></h2>
          <div className="coordination-list">
            {items.length === 0 ? <p className="empty-hint">Sin trayectos.</p> : items.map((row) => (
              <article key={row.journeyId} className="coordination-card">
                <header>
                  <StatusBadge status={row.status} />
                  <span className="coord-direction">{directionLabel(row.direction)}</span>
                </header>
                <div className="coord-route"><strong>{row.patientName || 'Sin identificar'}</strong><span>{row.origin} → {row.destination}</span><small>{formatDateTime(row.operationalAt)} · {row.journeyPublicId}</small></div>
                <StatusMap points={row.statusPoints} />
                <label className="vehicle-picker"><span>Vehículo</span>
                  <select value={row.vehicleId ?? ''} onChange={(e) => void assign(row.journeyId, e.target.value)}>
                    <option value="">Sin asignar</option>
                    {vehicles.map((vehicle) => <option key={vehicle.id} value={vehicle.id}>{vehicle.name} ({vehicle.publicId})</option>)}
                  </select>
                </label>
                <footer><Link to={`/trayectos/${row.journeyId}`}>Ver detalle →</Link></footer>
              </article>
            ))}
          </div>
        </section>
      })}
    </div>
  </div>
}
