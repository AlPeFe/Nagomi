import { Link } from '../router'
import type { Journey } from '../types'
import { directionLabel, operationalTime, requirementSummary } from '../utils'
import { DeliveryBadge, Indicators, StatusBadge } from './Badges'

// Paleta de acentos para parejas ida/vuelta: cada solicitud con ambos trayectos
// visibles comparte un color de borde para que la vuelta se identifique al instante.
const pairAccents = ['#0e7490', '#7c3aed', '#c2410c', '#15803d', '#be185d', '#b45309', '#1d4ed8', '#a16207']

function accentFor(requestId: string) {
  let hash = 0
  for (let i = 0; i < requestId.length; i++) hash = (hash * 31 + requestId.charCodeAt(i)) | 0
  return pairAccents[Math.abs(hash) % pairAccents.length]
}

export function JourneyTable({ journeys }: { journeys: Journey[] }) {
  const pairs = new Map<string, { outbound?: Journey; return?: Journey }>()
  for (const journey of journeys) {
    const pair = pairs.get(journey.requestId) ?? {}
    if (journey.direction === 'Outbound') pair.outbound = journey
    else pair.return = journey
    pairs.set(journey.requestId, pair)
  }
  return <div className="table-scroll"><table className="operations-table">
    <caption className="sr-only">Trayectos del resultado operativo actual</caption>
    <thead><tr><th>Hora / trayecto</th><th>Paciente</th><th>Ruta</th><th>Motivo / requisitos</th><th>Estado</th><th>Proveedor</th></tr></thead>
    <tbody>{journeys.map((journey) => {
      const pair = pairs.get(journey.requestId)
      const paired = !!(pair?.outbound && pair?.return)
      const accent = paired ? accentFor(journey.requestId) : undefined
      const mate = journey.direction === 'Outbound' ? pair?.return : pair?.outbound
      return <tr key={journey.id} className={`${journey.deliveryState === 'Dead' ? 'row-alert' : ''}${paired ? ' pair-row' : ''}`} style={accent ? { '--pair-accent': accent } as React.CSSProperties : undefined}>
        <td data-label="Hora / trayecto"><div className="journey-cell"><span className={`rail-dot rail-${journey.status.toLowerCase()}`} aria-hidden="true" /><div><strong className={journey.pickupTimePending ? 'pending-time' : ''}>{operationalTime(journey)}</strong><Link to={`/trayectos/${journey.id}`}>{journey.publicId}</Link><small>{directionLabel(journey.direction)} · {journey.requestPublicId}{paired && mate ? <span className="pair-chip" title={`Ida y vuelta de ${journey.requestPublicId}`}>↕ {mate.publicId}</span> : null}</small></div></div></td>
        <td data-label="Paciente"><strong>{journey.patientName || 'Sin identificar'}</strong><small>{journey.patientPhone || 'Sin teléfono'}</small></td>
        <td data-label="Ruta"><strong>{journey.origin.name}</strong><span className="route-arrow" aria-hidden="true">→</span>{journey.destination.name}<small>{journey.origin.municipality} · {journey.destination.municipality}</small></td>
        <td data-label="Motivo / requisitos">{journey.reason}<small>{requirementSummary(journey.requirements)}</small></td>
        <td data-label="Estado"><StatusBadge status={journey.status} /><Indicators external={journey.externallyModified} cancelledBy={journey.cancelledBy} delivery={journey.deliveryState} /></td>
        <td data-label="Proveedor"><strong>{journey.provider || 'Sin asignar'}</strong><small>{journey.contract || 'Sin contrato'}</small><DeliveryBadge state={journey.deliveryState} /></td>
      </tr>
    })}</tbody>
  </table></div>
}
