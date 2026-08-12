import { useEffect, useState } from 'react'
import { api } from '../api'
import { IncidentMap } from '../components/IncidentMap'
import { geocodeAddress } from '../geocode'
import { EmptyState, ErrorState, LoadingState, PageHeader } from '../components/States'
import type { EmergencyStatus, EmergencyTransport } from '../types'
import { formatDateTime } from '../utils'

const statusLabel: Record<string, string> = { Active: 'Activa', Completed: 'Completada', Cancelled: 'Cancelada' }

type Tab = 'active' | 'history'

export function EmergencyPage() {
  const [records, setRecords] = useState<EmergencyTransport[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [creating, setCreating] = useState(false)
  const [selectedId, setSelectedId] = useState<string>()
  const [tab, setTab] = useState<Tab>('active')
  const [statusFilter, setStatusFilter] = useState<string>('')
  const [fromFilter, setFromFilter] = useState('')
  const [toFilter, setToFilter] = useState('')

  const [reason, setReason] = useState('')
  const [contactPhone, setContactPhone] = useState('')
  const [observations, setObservations] = useState('')
  const [addressQuery, setAddressQuery] = useState('')
  const [incident, setIncident] = useState<EmergencyTransport['incident']>()
  const [searching, setSearching] = useState(false)

  async function load() {
    try {
      setLoading(true)
      if (tab === 'active') {
        setRecords(await api.listEmergencies({ status: 'Active' }))
      } else {
        const filters: { status?: EmergencyStatus; from?: string; to?: string } = {}
        if (statusFilter) filters.status = statusFilter as EmergencyStatus
        if (fromFilter) filters.from = `${fromFilter}T00:00:00Z`
        if (toFilter) filters.to = `${toFilter}T23:59:59Z`
        setRecords(await api.listEmergencies(filters))
      }
      setError('')
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Error desconocido.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { void load() }, [tab])

  function resetForm() {
    setReason(''); setContactPhone(''); setObservations(''); setAddressQuery(''); setIncident(undefined)
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    if (!incident) { setMessage('Marca el punto de incidencia en el mapa.'); return }
    if (!reason.trim()) { setMessage('Indica el motivo de la urgencia.'); return }
    try {
      const created = await api.createEmergency({
        reason: reason.trim(),
        incident,
        contactPhone: contactPhone.trim() || undefined,
        observations: observations.trim() || undefined,
      })
      setRecords(await api.listEmergencies({ status: 'Active' }))
      setSelectedId(created.id)
      resetForm()
      setCreating(false)
      setMessage(`Urgencia ${created.publicId} registrada.`)
    } catch (caught) {
      setMessage(caught instanceof Error ? caught.message : 'No se pudo registrar la urgencia.')
    }
  }

  async function searchAddress(event: React.FormEvent) {
    event.preventDefault()
    if (!addressQuery.trim()) return
    setSearching(true)
    try {
      const location = await geocodeAddress(addressQuery)
      if (location) { setIncident(location); setMessage('') }
      else setMessage('No se encontró la dirección. Prueba con otro texto o marca el punto en el mapa.')
    } finally { setSearching(false) }
  }

  async function cancel(id: string) {
    if (!window.confirm('¿Cancelar esta urgencia?')) return
    try { await api.cancelEmergency(id); setRecords(await api.listEmergencies({ status: 'Active' })) }
    catch (caught) { setMessage(caught instanceof Error ? caught.message : 'No se pudo cancelar.') }
  }

  return <div className="page wide-page">
    <PageHeader eyebrow="Emergencias" title="Traslados de urgencia" description="Registro mínimo de incidentes geolocalizados: punto de incidencia, motivo y contacto." actions={<button className="button button-primary" onClick={() => { setCreating((value) => !value); setMessage('') }}>{creating ? 'Cerrar formulario' : 'Nueva urgencia'}</button>} />
    {message && <div className="inline-message" role="status">{message}</div>}

    {creating && <section className="detail-card emergency-form">
      <h2>Registrar traslado de urgencia</h2>
      <form onSubmit={(event) => void submit(event)}>
        <div className="form-grid">
          <label><span>Motivo / tipo de incidencia</span><input value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Ej. Atropello, caída, dolor torácico" /></label>
          <label><span>Teléfono de contacto</span><input type="tel" value={contactPhone} onChange={(e) => setContactPhone(e.target.value)} placeholder="Opcional" /></label>
        </div>
        <label><span>Punto de incidencia (geolocalizado)</span>
          <div className="map-search-row">
            <input value={addressQuery} onChange={(e) => setAddressQuery(e.target.value)} placeholder="Buscar dirección…" />
            <button className="button button-secondary" type="button" onClick={(event) => void searchAddress(event)} disabled={searching}>{searching ? 'Buscando…' : 'Buscar'}</button>
          </div>
          <IncidentMap value={incident} onChange={(location) => setIncident(location ?? undefined)} />
          {incident && <small className="map-result">Marcado: {incident.address ?? `${incident.latitude.toFixed(5)}, ${incident.longitude.toFixed(5)}`}{incident.municipality ? ` · ${incident.municipality}` : ''}</small>}
        </label>
        <label><span>Observaciones</span><textarea value={observations} onChange={(e) => setObservations(e.target.value)} rows={3} placeholder="Accesos, referencias, prioridad…" /></label>
        <button className="button button-accent" type="submit">Registrar urgencia</button>
      </form>
    </section>}

    <div className="tabs" role="tablist" aria-label="Filtro de urgencias">
      <button className={`tab ${tab === 'active' ? 'active' : ''}`} role="tab" aria-selected={tab === 'active'} onClick={() => setTab('active')}>Activas</button>
      <button className={`tab ${tab === 'history' ? 'active' : ''}`} role="tab" aria-selected={tab === 'history'} onClick={() => setTab('history')}>Historial</button>
    </div>

    {tab === 'history' && (
      <form className="filter-panel emergency-filters" onSubmit={(event) => { event.preventDefault(); void load() }}>
        <label><span>Estado</span><select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}><option value="">Todos</option><option value="Active">Activa</option><option value="Completed">Completada</option><option value="Cancelled">Cancelada</option></select></label>
        <label><span>Desde</span><input type="date" value={fromFilter} onChange={(e) => setFromFilter(e.target.value)} /></label>
        <label><span>Hasta</span><input type="date" value={toFilter} onChange={(e) => setToFilter(e.target.value)} /></label>
        <button className="button button-accent" type="submit">Filtrar</button>
      </form>
    )}

    {loading ? <LoadingState label="Cargando urgencias" /> : error ? <ErrorState message={error} retry={() => void load()} /> : records.length === 0 ? <EmptyState title={tab === 'active' ? 'No hay urgencias activas' : 'No hay urgencias en este historial'} message={tab === 'active' ? 'Crea la primera para geolocalizar el punto de incidencia.' : 'Ajusta los filtros para ver más registros.'} /> : (
      <div className="emergency-list">
        {records.map((record) => (
          <article key={record.id} className={`emergency-card ${record.id === selectedId ? 'is-selected' : ''}`}>
            <button className="emergency-card-main" onClick={() => setSelectedId(record.id === selectedId ? undefined : record.id)}>
              <div><strong>{record.publicId}</strong><small>{formatDateTime(record.createdAt)}</small></div>
              <div className="emergency-reason">{record.reason}</div>
              <div className="emergency-location">{record.incident.address ?? `${record.incident.latitude.toFixed(5)}, ${record.incident.longitude.toFixed(5)}`}{record.incident.municipality ? ` · ${record.incident.municipality}` : ''}</div>
              <span className={`status-pill status-${record.status.toLowerCase()}`}>{statusLabel[record.status] ?? record.status}</span>
            </button>
            {record.id === selectedId && <div className="emergency-detail">
              <p>{record.observations || 'Sin observaciones.'}</p>
              {record.contactPhone && <p>Contacto: {record.contactPhone}</p>}
              <p>Coordenadas: {record.incident.latitude.toFixed(6)}, {record.incident.longitude.toFixed(6)} · <a href={`https://www.openstreetmap.org/?mlat=${record.incident.latitude}&mlon=${record.incident.longitude}#map=17/${record.incident.latitude}/${record.incident.longitude}`} target="_blank" rel="noreferrer">Ver en OpenStreetMap</a></p>
              {record.status === 'Active' && <button className="button button-danger" onClick={() => void cancel(record.id)}>Cancelar urgencia</button>}
            </div>}
          </article>
        ))}
      </div>
    )}
  </div>
}
