import type { Journey, JourneyStatus } from '../types'
import { formatDateTime, statusLabel } from '../utils'

const stages: JourneyStatus[] = ['Scheduled', 'Activated', 'EnRouteToOrigin', 'ArrivedAtOrigin', 'PatientOnBoard', 'EnRouteToDestination', 'ArrivedAtDestination', 'Completed']

export function JourneyTimeline({ journey }: { journey: Journey }) {
  const current = stages.indexOf(journey.status)
  return <ol className="journey-timeline" aria-label="Progreso del trayecto">{stages.map((stage, index) => {
    const event = journey.statusEvents?.filter((item) => item.status === stage).sort((a, b) => b.occurredAt.localeCompare(a.occurredAt))[0]
    const reached = index <= current && journey.status !== 'Cancelled'
    return <li key={stage} className={`${reached ? 'reached' : ''} ${stage === journey.status ? 'current' : ''}`}><span className="timeline-node" aria-hidden="true" /><div><strong>{statusLabel(stage)}</strong><span>{event ? formatDateTime(event.occurredAt) : stage === journey.status ? 'Estado actual' : 'Pendiente'}</span>{event?.actor && <small>{event.actor} · {event.source}</small>}</div></li>
  })}</ol>
}
