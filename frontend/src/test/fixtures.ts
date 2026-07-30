import type { Journey, TransportRequest } from '../types'

export const journey: Journey = {
  id: 'journey-1', publicId: 'TRA-2026-0042', requestId: 'request-1', requestPublicId: 'SOL-2026-0019', direction: 'Return', scheduledPickupAt: '2026-07-29T23:59:00+02:00', pickupTimePending: true, patientName: 'Ana Martín', patientPhone: '600 123 456', origin: { name: 'Hospital La Paz', municipality: 'Madrid', address: 'Paseo de la Castellana 261' }, destination: { name: 'Residencia Los Olivos', municipality: 'Alcobendas', address: 'Calle Mayor 8' }, reason: 'Alta hospitalaria', requirements: { mobility: 'Wheelchair', oxygen: false, companion: true, medicalStaff: false, isolation: false, bariatric: false, stairsAssistance: false }, status: 'Cancelled', provider: 'Ambulancias Centro', contract: 'CTR-MAD-01', providerReference: 'EXT-887', deliveryState: 'Dead', externallyModified: true, cancelledBy: 'Provider', statusEvents: [{ id: 'event-1', status: 'Cancelled', occurredAt: '2026-07-29T14:30:00+02:00', actor: 'Ambulancias Centro', source: 'Provider' }], audit: [],
}

export const recurrence = { startDate: '2026-07-01', endDate: '2026-08-01', utcOffset: '02:00:00', weekdaySchedules: [{ dayOfWeek: 1, outboundAppointmentTime: '10:00:00', returnPickupTime: '14:00:00', returnPickupNextDay: false, returnPickupTimePending: false }] }
export const request: TransportRequest = { id: 'request-1', publicId: 'SOL-2026-0019', status: 'Active', patientName: 'Ana Martín', reason: 'Alta hospitalaria', contract: 'CTR-MAD-01', provider: 'Ambulancias Centro', origin: journey.origin, destination: journey.destination, recurring: recurrence, journeys: [journey], audit: [], deliveries: [] }

export const backendJourney = {
  id: 'journey-1', transportRequestId: 'request-1', publicId: 'TRA-2026-0042', direction: 1, serviceDate: '2026-07-29',
  origin: { type: 1, name: 'Hospital La Paz', street: 'Paseo de la Castellana 261', municipality: 'Madrid' }, destination: { type: 0, name: 'Residencia Los Olivos', street: 'Calle Mayor 8', municipality: 'Alcobendas' },
  requirements: { mobility: 1, requiresOxygen: false, companionRequired: true }, schedule: { scheduledStartAt: '2026-07-29T23:59:00+02:00', scheduledPickupAt: '2026-07-29T23:59:00+02:00', pickupTimePending: true },
  currentStatus: 8, providerReference: 'EXT-887', externallyModified: true, retrievalState: 'Dead', currentCancellingParty: 1,
  statusHistory: [{ id: 'event-1', status: 8, occurredAt: '2026-07-29T14:30:00+02:00', actor: 'Ambulancias Centro', source: 1 }],
}

export const backendRequest = {
  id: 'request-1', publicId: 'SOL-2026-0019', status: 1, patient: { firstName: 'Ana', lastName: 'Martín', phone: '600 123 456' }, reason: { code: 'Alta hospitalaria', description: 'Alta hospitalaria' },
  defaultOrigin: backendJourney.origin, defaultDestination: backendJourney.destination, requirements: backendJourney.requirements, contractCode: 'CTR-MAD-01', providerName: 'Ambulancias Centro', recurrence, journeyRecords: [backendJourney], updatedAt: '2026-07-29T12:00:00Z',
}

export const operationsRow = {
  journeyId: 'journey-1', journeyPublicId: 'TRA-2026-0042', requestId: 'request-1', requestPublicId: 'SOL-2026-0019', operationalAt: '2026-07-29T23:59:00+02:00', pickupTimePending: true,
  patientName: 'Ana Martín', patientPhone: '600 123 456', origin: 'Hospital La Paz', destination: 'Residencia Los Olivos', direction: 1, reason: 'Alta hospitalaria', requirements: 'Wheelchair', status: 0,
  provider: 'Ambulancias Centro', contractCode: 'CTR-MAD-01', providerReference: 'EXT-887', retrievalState: 'Dead', externallyModified: true, providerCancelled: true,
}

export function json(data: unknown, status = 200) { return Promise.resolve(new Response(JSON.stringify(data), { status, headers: { 'Content-Type': 'application/json' } })) }
