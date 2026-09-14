import { Link } from '../router'
import type { Journey, Vehicle } from '../types'
import { VEHICLE_TYPE_LABELS } from '../types'
import { directionLabel, operationalTime, requirementSummary } from '../utils'
import { Indicators, StatusBadge } from './Badges'

// Paleta de acentos para parejas ida/vuelta: cada solicitud con ambos trayectos
// visibles comparte un color de borde para que la vuelta se identifique al instante.
const pairAccents = ['#0e7490', '#7c3aed', '#c2410c', '#15803d', '#be185d', '#b45309', '#1d4ed8', '#a16207']

function accentFor(requestId: string) {
  let hash = 0
  for (let i = 0; i < requestId.length; i++) hash = (hash * 31 + requestId.charCodeAt(i)) | 0
  return pairAccents[Math.abs(hash) % pairAccents.length]
}

/**
 * Pendiente NO es un estado del dominio: un traslado programado SIN vehículo
 * asignado está pendiente de asignación. Se deriva en lectura para no duplicar verdad.
 */
function isPending(journey: Journey) {
  return journey.status === 'Scheduled' && !journey.vehicleId
}

export function AssignmentCell({ journey, vehicles, onAssignVehicle, onAssignDriver }: {
  journey: Journey
  vehicles: Vehicle[]
  onAssignVehicle?: (journeyId: string, vehicleId: string) => void
  onAssignDriver?: (journeyId: string, driverName: string) => void
}) {
  const editable = !!onAssignVehicle
  return <div className="assignment-cell">
    <select
      className="assignment-select"
      aria-label={`Vehículo de ${journey.publicId}`}
      value={journey.vehicleId ?? ''}
      disabled={!editable}
      onChange={(e) => onAssignVehicle?.(journey.id, e.target.value)}
    >
      <option value="">Sin asignar</option>
      {vehicles.map((vehicle) => (
        <option key={vehicle.id} value={vehicle.id}>{vehicle.name} · {VEHICLE_TYPE_LABELS[vehicle.vehicleType]}</option>
      ))}
    </select>
    {editable && onAssignDriver && (
      <input
        className="assignment-driver"
        aria-label={`Conductor de ${journey.publicId}`}
        defaultValue={journey.driverName ?? ''}
        placeholder="Conductor"
        onBlur={(e) => { if (e.target.value.trim() !== (journey.driverName ?? '')) onAssignDriver(journey.id, e.target.value) }}
      />
    )}
    {!editable && journey.driverName && <small>{journey.driverName}</small>}
  </div>
}

export function JourneyTable({ journeys, vehicles = [], onAssignVehicle, onAssignDriver }: {
  journeys: Journey[]
  vehicles?: Vehicle[]
  onAssignVehicle?: (journeyId: string, vehicleId: string) => void
  onAssignDriver?: (journeyId: string, driverName: string) => void
}) {
  const pairs = new Map<string, { outbound?: Journey; return?: Journey }>()
  for (const journey of journeys) {
    const pair = pairs.get(journey.requestId) ?? {}
    if (journey.direction === 'Outbound') pair.outbound = journey
    else pair.return = journey
    pairs.set(journey.requestId, pair)
  }
  return <div className="table-scroll"><table className="operations-table">
    <caption className="sr-only">Trayectos del resultado operativo actual</caption>
    <thead><tr>
      <th>Hora / trayecto</th><th>Paciente</th><th>Ruta</th><th>Motivo / requisitos</th>
      <th>Estado</th><th>Vehículo / conductor</th><th>Observaciones</th><th>Proveedor</th>
    </tr></thead>
    <tbody>{journeys.map((journey) => {
      const pair = pairs.get(journey.requestId)
      const paired = !!(pair?.outbound && pair?.return)
      const accent = paired ? accentFor(journey.requestId) : undefined
      const mate = journey.direction === 'Outbound' ? pair?.return : pair?.outbound
      return <tr key={journey.id} className={`${journey.deliveryState === 'Dead' ? 'row-alert' : ''}${paired ? ' pair-row' : ''}`} style={accent ? { '--pair-accent': accent } as React.CSSProperties : undefined}>
        <td data-label="Hora / trayecto"><div className="journey-cell"><span className={`rail-dot rail-${journey.status.toLowerCase()}`} aria-hidden="true" /><div><strong className={journey.pickupTimePending ? 'pending-time' : ''}>{operationalTime(journey)}</strong><Link to={`/trayectos/${journey.id}`}>{journey.publicId}</Link><small>{directionLabel(journey.direction)} · {journey.requestPublicId}{paired && mate ? <span className="pair-chip" title={`Ida y vuelta de ${journey.requestPublicId}`}>↕ {mate.publicId}</span> : null}</small></div></div></td>
        <td data-label="Paciente"><strong>{journey.patientName || 'Sin identificar'}</strong><small>{journey.patientPhone || 'Sin teléfono'}</small></td>
        <td data-label="Ruta"><div className="route-line"><strong>{journey.origin.name}</strong><span className="route-arrow" aria-hidden="true">→</span><span>{journey.destination.name}</span></div>{[journey.origin.municipality, journey.destination.municipality].filter(Boolean).length > 0 && <small>{[journey.origin.municipality, journey.destination.municipality].filter(Boolean).join(' · ')}</small>}</td>
        <td data-label="Motivo / requisitos">{journey.reason}<small>{requirementSummary(journey.requirements)}</small></td>
        <td data-label="Estado">
          {isPending(journey) ? <span className="badge badge-pending"><span aria-hidden="true" />Pendiente de vehículo</span> : <StatusBadge status={journey.status} />}
          <Indicators external={journey.externallyModified} cancelledBy={journey.cancelledBy} delivery={journey.deliveryState} />
        </td>
        <td data-label="Vehículo / conductor">
          <AssignmentCell journey={journey} vehicles={vehicles} onAssignVehicle={onAssignVehicle} onAssignDriver={onAssignDriver} />
        </td>
        <td data-label="Observaciones">
          {journey.notes
            ? <details className="notes-peek"><summary>Ver observaciones</summary><p>{journey.notes}</p></details>
            : <span className="muted">—</span>}
        </td>
        <td data-label="Proveedor"><strong>{journey.provider || 'Sin asignar'}</strong><small>{journey.contract || 'Sin contrato'}</small></td>
      </tr>
    })}</tbody>
  </table></div>
}
