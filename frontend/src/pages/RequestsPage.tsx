import { useEffect, useEffectEvent, useState } from 'react'
import { ArrowRight, CalendarBlank, Phone, Repeat } from '@phosphor-icons/react'
import { Link } from '../router'
import { api } from '../api'
import { EmptyState, ErrorState, LoadingState, PageHeader } from '../components/States'
import type { TransportRequest } from '../types'
import { formatDateTime } from '../utils'

/**
 * Solicitudes = las CABECERAS. Cada solicitud materializa uno o varios traslados
 * (hijos, ida y vuelta) que se ven en su detalle. Esta pantalla sólo lista las
 * cabeceras con los datos que le pertenecen (paciente, motivo, periodicidad,
 * horarios y rango) y no los de cada traslado suelto.
 */
const STATUS_LABEL: Record<TransportRequest['status'], string> = {
  Draft: 'Borrador', Active: 'Activa', Completed: 'Completada', Cancelled: 'Cancelada',
}
const STATUS_FILTERS = [
  { value: 'all', label: 'Todas' },
  { value: 'Active', label: 'Activas' },
  { value: 'Draft', label: 'Borradores' },
  { value: 'Completed', label: 'Completadas' },
  { value: 'Cancelled', label: 'Canceladas' },
] as const

const WEEK_LETTERS = ['D', 'L', 'M', 'X', 'J', 'V', 'S']

function hhmm(value?: string) { return value ? value.slice(0, 5) : '' }

/** '2026-09-01' → '01/09' */
function shortDate(value?: string) {
  if (!value) return ''
  const [, month, day] = value.split('-')
  return day && month ? `${day}/${month}` : (value ?? '')
}

/** Periodicidad de la cabecera: días, horas de salida/vuelta y rango de vigencia. */
function periodicity(request: TransportRequest) {
  const recurrence = request.recurring
  const schedules = recurrence?.weekdaySchedules ?? []
  if (!recurrence || !schedules.length) return 'Traslado puntual'
  const days = [...new Set(schedules.map((schedule) => WEEK_LETTERS[schedule.dayOfWeek] ?? '?'))].join(' · ')
  const first = schedules[0]
  const outbound = hhmm(first.outboundPickupTime) || hhmm(first.outboundStartTime) || hhmm(first.outboundAppointmentTime)
  const back = hhmm(first.returnPickupTime)
  const range = `${shortDate(recurrence.startDate)} → ${shortDate(recurrence.endDate)}`
  return `${days} · salida ${outbound || '—'}${back ? ` · vuelta ${back}` : ''} · ${range}`
}

/** Resumen de los hijos: total, ida/vuelta y el próximo traslado no terminal. */
function childrenSummary(request: TransportRequest) {
  const journeys = request.journeys ?? []
  const upcoming = journeys
    .filter((journey) => journey.status !== 'Completed' && journey.status !== 'Cancelled')
    .map((journey) => journey.scheduledStartAt ?? journey.scheduledPickupAt ?? journey.appointmentAt)
    .filter((value): value is string => Boolean(value))
    .sort()
  return {
    total: journeys.length,
    outbound: journeys.filter((journey) => journey.direction === 'Outbound').length,
    returns: journeys.filter((journey) => journey.direction === 'Return').length,
    next: upcoming[0],
  }
}

export function RequestsPage() {
  const [requests, setRequests] = useState<TransportRequest[]>([])
  const [search, setSearch] = useState('')
  const [applied, setApplied] = useState('')
  const [statusFilter, setStatusFilter] = useState<string>('all')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  async function load() {
    setLoading(true)
    try { const result = await api.listRequests(applied); setRequests(result.items); setError('') }
    catch (caught) { setError(caught instanceof Error ? caught.message : 'Error desconocido.') }
    finally { setLoading(false) }
  }
  const loadEffect = useEffectEvent(load)
  useEffect(() => { void loadEffect() }, [applied])

  const visible = statusFilter === 'all' ? requests : requests.filter((request) => request.status === statusFilter)

  return <div className="page">
    <PageHeader
      eyebrow="Gestión"
      title="Solicitudes de transporte"
      description="Cada solicitud agrupa sus traslados (ida y vuelta): aquí ves la cabecera, y desde ella entras al listado de traslados."
      actions={<Link className="button button-accent" to="/solicitudes/nueva">Nueva solicitud</Link>}
    />

    <div className="requests-toolbar">
      <form className="list-search" onSubmit={(event) => { event.preventDefault(); setApplied(search) }}>
        <label><span className="sr-only">Buscar solicitudes</span>
          <input type="search" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Buscar por identificador, paciente o motivo" />
        </label>
        <button className="button button-primary">Buscar</button>
      </form>
      <div className="requests-filters" role="group" aria-label="Filtrar por estado">
        {STATUS_FILTERS.map((filter) => <button key={filter.value} type="button"
          className={`filter-chip ${statusFilter === filter.value ? 'filter-chip-active' : ''}`}
          onClick={() => setStatusFilter(filter.value)}>{filter.label}</button>)}
      </div>
    </div>

    {loading ? <LoadingState label="Cargando solicitudes" />
      : error ? <ErrorState message={error} retry={() => void load()} />
        : !visible.length ? <EmptyState title="No hay solicitudes" message="Crea una solicitud o cambia la búsqueda y el filtro." />
          : <div className="requests-list">{visible.map((request) => {
            const summary = childrenSummary(request)
            return <Link className="request-card" to={`/solicitudes/${request.id}`} key={request.id}>
              <div className="request-card-main">
                <div className="request-card-line">
                  <span className={`request-status request-status-${request.status.toLowerCase()}`}>{STATUS_LABEL[request.status]}</span>
                  <strong className="request-card-id">{request.publicId ?? 'Borrador sin identificador'}</strong>
                  <span className="request-card-updated">{formatDateTime(request.updatedAt)}</span>
                </div>
                <div className="request-card-line request-card-patient">
                  <strong>{request.patientName || 'Paciente sin identificar'}</strong>
                  {request.patientPhone && <span className="muted"><Phone size={12} aria-hidden="true" /> {request.patientPhone}</span>}
                  {request.reason && <span className="muted">· {request.reason}</span>}
                </div>
                <div className="request-card-route">
                  <span>{request.origin?.name || 'Origen pendiente'}</span>
                  <ArrowRight size={12} aria-hidden="true" />
                  <span>{request.destination?.name || 'Destino pendiente'}</span>
                </div>
              </div>

              <div className="request-card-side">
                <div className="request-card-line">
                  <Repeat size={13} aria-hidden="true" />
                  <span className="request-card-periodicity">{periodicity(request)}</span>
                </div>
                <div className="request-card-line request-card-children">
                  <span className="count-badge">{summary.total} traslados</span>
                  {summary.outbound > 0 && <span className="direction-chip">ida {summary.outbound}</span>}
                  {summary.returns > 0 && <span className="direction-chip">vuelta {summary.returns}</span>}
                </div>
                <div className="request-card-line request-card-next">
                  <CalendarBlank size={13} aria-hidden="true" />
                  <span>{summary.next ? `Próximo: ${formatDateTime(summary.next)}` : 'Sin traslados pendientes'}</span>
                </div>
              </div>

              <div className="request-card-foot">
                <span>{summary.total} {summary.total === 1 ? 'traslado' : 'traslados'}</span>
                {request.contract && <span>Contrato {request.contract}</span>}
                {request.provider && <span>Proveedor {request.provider}</span>}
                <span className="request-card-go">Ver traslados <ArrowRight size={12} aria-hidden="true" /></span>
              </div>
            </Link>
          })}</div>}
  </div>
}
