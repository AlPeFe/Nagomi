import { useEffect, useEffectEvent, useState } from 'react'
import { Link } from '../router'
import { api } from '../api'
import { EmptyState, ErrorState, LoadingState, PageHeader } from '../components/States'
import type { TransportRequest } from '../types'
import { formatDateTime } from '../utils'

export function RequestsPage() {
  const [requests, setRequests] = useState<TransportRequest[]>([])
  const [search, setSearch] = useState('')
  const [applied, setApplied] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  async function load() { setLoading(true); try { const result = await api.listRequests(applied); setRequests(result.items); setError('') } catch (e) { setError(e instanceof Error ? e.message : 'Error desconocido.') } finally { setLoading(false) } }
  const loadEffect = useEffectEvent(load)
  useEffect(() => { void loadEffect() }, [applied])
  return <div className="page">
    <PageHeader eyebrow="Gestión" title="Solicitudes de transporte" description="Borradores y solicitudes enviadas, con sus trayectos e historial." actions={<Link className="button button-accent" to="/solicitudes/nueva">Nueva solicitud</Link>} />
    <form className="list-search" onSubmit={(e) => { e.preventDefault(); setApplied(search) }}><label><span className="sr-only">Buscar solicitudes</span><input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Buscar por identificador o paciente" /></label><button className="button button-primary">Buscar</button></form>
    {loading ? <LoadingState label="Cargando solicitudes" /> : error ? <ErrorState message={error} retry={() => void load()} /> : !requests.length ? <EmptyState title="No hay solicitudes" message="Crea una solicitud o modifica la búsqueda." /> : <div className="request-list">{requests.map((request) => <Link className="request-row" to={`/solicitudes/${request.id}`} key={request.id}><span className={`request-state state-${request.status.toLowerCase()}`}>{request.status === 'Draft' ? 'Borrador' : request.status === 'Active' ? 'Activa' : request.status === 'Completed' ? 'Completada' : 'Cancelada'}</span><div><strong>{request.publicId ?? 'Borrador sin identificador'}</strong><span>{request.patientName || 'Paciente sin identificar'} · {request.reason || 'Motivo pendiente'}</span></div><div><strong>{request.origin?.name || 'Origen pendiente'} → {request.destination?.name || 'Destino pendiente'}</strong><span>{request.journeys?.length ?? 0} trayectos · {formatDateTime(request.updatedAt)}</span></div><span aria-hidden="true">→</span></Link>)}</div>}
  </div>
}
