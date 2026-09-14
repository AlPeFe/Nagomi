import type { Journey, JourneyStatus, Requirements } from './types'

const statusLabels: Record<JourneyStatus, string> = {
  Scheduled: 'Programado', Activated: 'Activado', EnRouteToOrigin: 'Hacia origen', ArrivedAtOrigin: 'En origen', PatientOnBoard: 'Paciente recogido', EnRouteToDestination: 'En traslado', ArrivedAtDestination: 'En destino', Completed: 'Completado', Cancelled: 'Cancelado',
}

export const statusLabel = (status: JourneyStatus) => statusLabels[status]
export const directionLabel = (direction: Journey['direction']) => direction === 'Outbound' ? 'Ida' : 'Vuelta'
export const deliveryLabel = (state: Journey['deliveryState']) => ({ Pending: 'Pendiente de envío', Published: 'Enviado', Retrieved: 'Recibido', Dead: 'Envío fallido', NotPublished: 'Sin publicar' })[state]

/** DD/MM del calendario LOCAL. `toISOString()` da la fecha UTC: en España (UTC+1/+2)
 *  entre las 00:00 y la 01:00/02:00 locales devolvía AYER, y la ventana operativa
 *  de los listados se desplazaba un día. */
export function localDate(offset = 0) {
  const date = new Date()
  date.setDate(date.getDate() + offset)
  return localDateOnly(date)
}

/** Fecha local (YYYY-MM-DD) de un Date o de un instante con offset. */
export function localDateOnly(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

export function formatDateTime(value?: string) {
  if (!value) return 'Sin hora'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('es-ES', { dateStyle: 'short', timeStyle: 'short' }).format(date)
}

export function operationalTime(journey: Journey) {
  if (journey.direction === 'Return' && journey.pickupTimePending) return 'Hora pendiente'
  return formatDateTime(journey.direction === 'Return' ? journey.scheduledPickupAt : journey.scheduledStartAt)
}

export function requirementSummary(requirements: Requirements) {
  const labels = { Autonomous: 'Autónomo', Wheelchair: 'Silla', Stretcher: 'Camilla' }
  const values = [labels[requirements.mobility]]
  if (requirements.oxygen) values.push('Oxígeno')
  if (requirements.companion) values.push('Acompañante')
  if (requirements.medicalStaff) values.push('Personal sanitario')
  if (requirements.isolation) values.push('Aislamiento')
  if (requirements.bariatric) values.push('Bariátrica')
  if (requirements.stairsAssistance) values.push('Ayuda escaleras')
  return values.join(' · ')
}

export function csvForJourneys(journeys: Journey[]) {
  const headers = ['Trayecto', 'Solicitud', 'Hora operativa', 'Paciente', 'Teléfono', 'Origen', 'Destino', 'Dirección', 'Motivo', 'Requisitos', 'Estado', 'Proveedor', 'Recepción']
  const rows = journeys.map((journey) => [journey.publicId, journey.requestPublicId, operationalTime(journey), journey.patientName ?? '', journey.patientPhone ?? '', journey.origin.name, journey.destination.name, directionLabel(journey.direction), journey.reason, requirementSummary(journey.requirements), statusLabel(journey.status), journey.provider ?? '', deliveryLabel(journey.deliveryState)])
  const escape = (value: string) => `"${value.replaceAll('"', '""')}"`
  return `\uFEFF${[headers, ...rows].map((row) => row.map(escape).join(';')).join('\r\n')}`
}
