import { useEffect, useEffectEvent, useState } from 'react'
import {
  BellRinging,
  CaretLeft,
  CaretRight,
  CheckCircle,
  Clock,
  Eye,
  MapPin,
  MapPinLine,
  NavigationArrow,
  Truck,
  UserCheck,
  User,
  Phone,
  X,
  XCircle,
} from '@phosphor-icons/react'
import type { Icon } from '@phosphor-icons/react'
import { api } from '../api'
import type { Journey, JourneyStatus, Vehicle } from '../types'
import { CANCELLATION_REASON_LABELS } from '../types'
import { directionLabel, formatDateTime, operationalTime, statusLabel } from '../utils'
import { Indicators, StatusBadge } from './Badges'
import { Link } from '../router'
import { AssignmentCell } from './JourneyTable'
import { RequirementsIcons } from './RequirementsIcons'

/** Icono y color por fase: ayuda a leer el estado de un vistazo. */
const PHASE_ICON: Record<JourneyStatus, Icon> = {
  Scheduled: Clock,
  Activated: BellRinging,
  EnRouteToOrigin: NavigationArrow,
  ArrivedAtOrigin: MapPin,
  PatientOnBoard: UserCheck,
  EnRouteToDestination: Truck,
  ArrivedAtDestination: MapPinLine,
  Completed: CheckCircle,
  Cancelled: XCircle,
}

const PHASES: JourneyStatus[] = ['Scheduled', 'Activated', 'EnRouteToOrigin', 'ArrivedAtOrigin', 'PatientOnBoard', 'EnRouteToDestination', 'ArrivedAtDestination', 'Completed']

function nextPhase(status: JourneyStatus): JourneyStatus {
  const index = PHASES.indexOf(status)
  return index < 0 || index === PHASES.length - 1 ? 'Completed' : PHASES[index + 1]
}

function isPending(journey: Journey) {
  return journey.status === 'Scheduled' && !journey.vehicleId
}

/**
 * Detalle rápido en panel lateral: se abre desde la lista sin salir de ella, con
 * navegación ‹ › entre los traslados del resultado y acceso al detalle completo.
 */
export function JourneyQuickView({ journeys, index, onClose, onNavigate, vehicles, onAssignVehicle, onAssignDriver, onChanged }: {
  journeys: Journey[]
  index: number
  onClose: () => void
  onNavigate: (index: number) => void
  vehicles: Vehicle[]
  onAssignVehicle: (journeyId: string, vehicleId: string) => void
  onAssignDriver: (journeyId: string, driverName: string) => void
  onChanged: () => void
}) {
  const row = journeys[index]
  const [detail, setDetail] = useState<Journey | null>(null)
  const [loading, setLoading] = useState(false)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')

  async function loadDetail() {
    if (!row) return
    setLoading(true); setMessage('')
    try {
      setDetail(await api.getJourney(row.id))
    } catch {
      setDetail(null)   // la fila de la lista ya trae lo esencial: degradamos sin romper
    } finally { setLoading(false) }
  }
  const loadEffect = useEffectEvent(loadDetail)

  useEffect(() => { setDetail(null); void loadEffect() }, [row?.id])

  // Escape cierra; las flechas navegan entre traslados.
  useEffect(() => {
    function onKey(event: KeyboardEvent) {
      if (event.key === 'Escape') onClose()
      if (event.key === 'ArrowRight' && index < journeys.length - 1) onNavigate(index + 1)
      if (event.key === 'ArrowLeft' && index > 0) onNavigate(index - 1)
    }
    window.addEventListener('keydown', onKey)
    const previous = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => { window.removeEventListener('keydown', onKey); document.body.style.overflow = previous }
  }, [index, journeys.length, onClose, onNavigate])

  if (!row) return null
  const shown = detail ?? row
  const events = detail?.statusEvents ?? []
  const pending = isPending(shown)
  const next = nextPhase(shown.status)
  const mutable = shown.status !== 'Completed' && shown.status !== 'Cancelled'
  // JSX no acepta <Componente[clave] />: hay que resolver el icono antes.
  const PhaseIcon = PHASE_ICON[shown.status]
  const NextIcon = PHASE_ICON[next]

  async function markNext() {
    setBusy(true); setMessage('')
    try {
      await api.addJourneyStatus(shown.id, next, new Date().toISOString(), crypto.randomUUID())
      // El handler usa la función normal; loadEffect es solo para el efecto (regla de hooks).
      await loadDetail()
      onChanged()
      setMessage(`Estado marcado: ${statusLabel(next)}.`)
    } catch (e) { setMessage(e instanceof Error ? e.message : 'No se pudo marcar el estado.') }
    finally { setBusy(false) }
  }

  return <>
    <div className="qv-scrim" onClick={onClose} aria-hidden="true" />
    <aside className="quick-view" role="dialog" aria-modal="true" aria-label={`Detalle rápido de ${shown.publicId}`}>
      <header className="qv-head">
        <div className="qv-nav">
          <button className="icon-button" onClick={() => onNavigate(index - 1)} disabled={index === 0} aria-label="Traslado anterior"><CaretLeft size={14} aria-hidden="true" /></button>
          <span className="qv-counter">{index + 1} de {journeys.length}</span>
          <button className="icon-button" onClick={() => onNavigate(index + 1)} disabled={index === journeys.length - 1} aria-label="Traslado siguiente"><CaretRight size={14} aria-hidden="true" /></button>
        </div>
        <div className="qv-head-actions">
          <Link className="button button-small button-secondary" to={`/trayectos/${shown.id}`}>Detalle completo</Link>
          <button className="icon-button" onClick={onClose} aria-label="Cerrar detalle rápido"><X size={15} aria-hidden="true" /></button>
        </div>
      </header>

      <div className="qv-body">
        <div className="qv-title">
          <span className={`qv-phase qv-${shown.status.toLowerCase()}`} aria-hidden="true"><PhaseIcon size={16} weight="duotone" /></span>
          <div>
            <h2>{shown.publicId}</h2>
            <p className="muted">{shown.requestPublicId} · {directionLabel(shown.direction)}</p>
          </div>
          {pending ? <span className="badge badge-pending"><span aria-hidden="true" />Pendiente de vehículo</span> : <StatusBadge status={shown.status} />}
        </div>
        <Indicators external={shown.externallyModified} cancelledBy={shown.cancelledBy} delivery={shown.deliveryState} />
        {message && <div className="inline-message" role="status">{message}</div>}

        <dl className="qv-grid">
          <div><dt><User size={12} aria-hidden="true" /> Paciente</dt><dd>{shown.patientName || 'Sin identificar'}</dd></div>
          <div><dt><Phone size={12} aria-hidden="true" /> Teléfono</dt><dd>{shown.patientPhone || 'Sin teléfono'}</dd></div>
          <div><dt><Clock size={12} aria-hidden="true" /> Hora operativa</dt><dd>{shown.pickupTimePending ? 'Hora pendiente' : operationalTime(shown)}</dd></div>
          <div><dt>Motivo</dt><dd>{shown.reason || 'Sin motivo'}</dd></div>
          <div className="span-2"><dt>Ruta</dt><dd>{shown.origin.name} → {shown.destination.name}</dd></div>
          <div className="span-2"><dt>Movilidad y requisitos</dt><dd><RequirementsIcons requirements={shown.requirements} withText /></dd></div>
          <div><dt>Proveedor</dt><dd>{shown.provider || 'Sin asignar'}</dd></div>
          <div><dt>Contrato</dt><dd>{shown.contract || 'Sin contrato'}</dd></div>
        </dl>

        <section className="qv-block">
          <h3>Vehículo y conductor</h3>
          <AssignmentCell journey={shown} vehicles={vehicles} onAssignVehicle={onAssignVehicle} onAssignDriver={onAssignDriver} showDriver />
        </section>

        {mutable && <section className="qv-block">
          <h3>Avanzar el estado</h3>
          <button className="button button-primary" disabled={busy} onClick={() => void markNext()}>
            <NextIcon size={14} weight="duotone" aria-hidden="true" />{busy ? 'Guardando…' : `Marcar ${statusLabel(next).toLowerCase()}`}
          </button>
        </section>}

        {shown.notes && <section className="qv-block">
          <h3>Observaciones</h3>
          <p className="qv-notes">{shown.notes}</p>
        </section>}

        {shown.cancelledBy && <section className="qv-block">
          <h3>Cancelación</h3>
          <p className="qv-notes">Cancelado por {shown.cancelledBy === 'Provider' ? 'el proveedor' : 'el solicitante'}{shown.cancellationReason ? ` · ${CANCELLATION_REASON_LABELS[shown.cancellationReason]}` : ''}.</p>
        </section>}

        <section className="qv-block">
          <h3>Historial de estados</h3>
          {loading && !events.length ? <p className="muted">Cargando historial…</p> : events.length ? (
            <ul className="qv-timeline">{events.map((event) => {
              const Icon = PHASE_ICON[event.status]
              return <li key={event.id} className={`qv-timeline-item qv-${event.status.toLowerCase()}`}>
                <span className="qv-timeline-node" aria-hidden="true"><Icon size={13} weight="duotone" /></span>
                <div><strong>{statusLabel(event.status)}</strong><span>{formatDateTime(event.occurredAt)} · {event.actor || 'Sistema'}{event.source === 'Provider' ? ' (proveedor)' : ''}</span></div>
              </li>
            })}</ul>
          ) : <p className="muted">Sin eventos registrados todavía.</p>}
        </section>

        <p className="qv-foot"><Eye size={13} aria-hidden="true" /> Vista rápida. Para editar ruta, horarios o cancelar con motivo, abre el detalle completo.</p>
      </div>
    </aside>
  </>
}
