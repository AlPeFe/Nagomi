import { Link } from '../router'
import type { Journey } from '../types'
import { directionLabel, operationalTime, requirementSummary } from '../utils'
import { DeliveryBadge, Indicators, StatusBadge } from './Badges'

export function JourneyTable({ journeys }: { journeys: Journey[] }) {
  return <div className="table-scroll"><table className="operations-table">
    <caption className="sr-only">Trayectos del resultado operativo actual</caption>
    <thead><tr><th>Hora / trayecto</th><th>Paciente</th><th>Ruta</th><th>Motivo / requisitos</th><th>Estado</th><th>Proveedor</th></tr></thead>
    <tbody>{journeys.map((journey) => <tr key={journey.id} className={journey.deliveryState === 'Dead' ? 'row-alert' : ''}>
      <td data-label="Hora / trayecto"><div className="journey-cell"><span className={`rail-dot rail-${journey.status.toLowerCase()}`} aria-hidden="true" /><div><strong className={journey.pickupTimePending ? 'pending-time' : ''}>{operationalTime(journey)}</strong><Link to={`/trayectos/${journey.id}`}>{journey.publicId}</Link><small>{directionLabel(journey.direction)} · {journey.requestPublicId}</small></div></div></td>
      <td data-label="Paciente"><strong>{journey.patientName || 'Sin identificar'}</strong><small>{journey.patientPhone || 'Sin teléfono'}</small></td>
      <td data-label="Ruta"><strong>{journey.origin.name}</strong><span className="route-arrow" aria-hidden="true">→</span>{journey.destination.name}<small>{journey.origin.municipality} · {journey.destination.municipality}</small></td>
      <td data-label="Motivo / requisitos">{journey.reason}<small>{requirementSummary(journey.requirements)}</small></td>
      <td data-label="Estado"><StatusBadge status={journey.status} /><Indicators external={journey.externallyModified} cancelledBy={journey.cancelledBy} delivery={journey.deliveryState} /></td>
      <td data-label="Proveedor"><strong>{journey.provider || 'Sin asignar'}</strong><small>{journey.contract || 'Sin contrato'}</small><DeliveryBadge state={journey.deliveryState} /></td>
    </tr>)}</tbody>
  </table></div>
}
