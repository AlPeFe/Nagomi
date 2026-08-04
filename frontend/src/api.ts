import type { DeliveryState, EmergencyDraft, EmergencyStatus, EmergencyTransport, Journey, JourneyFilters, JourneySchedule, JourneyStatus, ListResponse, LocationSnapshot, RecurrencePattern, Requirements, TransportRequest, TransportRequestDraft, TransportRequestSubmission } from './types'

export class ApiError extends Error {
  status?: number
  constructor(message: string, status?: number) { super(message); this.status = status }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  try {
    const response = await fetch(`/api${path}`, {
      ...init,
      headers: { 'Content-Type': 'application/json', ...init?.headers },
    })
    if (!response.ok) {
      const problem = await response.json().catch(() => null) as { detail?: string; title?: string } | null
      throw new ApiError(problem?.detail ?? problem?.title ?? `La API respondió con el estado ${response.status}.`, response.status)
    }
    if (response.status === 204) return undefined as T
    return await response.json() as T
  } catch (error) {
    if (error instanceof ApiError) throw error
    throw new ApiError('No se puede conectar con la API. Comprueba que el servicio de Nagomi esté disponible.')
  }
}

function asList<T>(value: ListResponse<T> | T[]): ListResponse<T> {
  return Array.isArray(value) ? { items: value, total: value.length } : value
}

type BackendLocation = Omit<LocationSnapshot, 'address' | 'type'> & { type?: number | LocationSnapshot['type']; street?: string }
type BackendRequirements = Partial<Requirements> & {
  requiresOxygen?: boolean
  oxygenConcentrationPercent?: number
  oxygenFlowLitresPerMinute?: number
  companionRequired?: boolean
  medicalStaffRequired?: boolean
  isolationRequired?: boolean
  bariatricRequired?: boolean
  stairsAssistanceRequired?: boolean
}
type BackendSchedule = { appointmentAt?: string; scheduledStartAt: string; scheduledPickupAt?: string; pickupTimePending?: boolean }
type BackendJourney = {
  id: string; transportRequestId: string; publicId: string; direction: number | Journey['direction']; origin: BackendLocation; destination: BackendLocation
  requirements: BackendRequirements; schedule: BackendSchedule; currentStatus: number | JourneyStatus; providerVisibleNotes?: string; providerReference?: string
  externallyModified?: boolean; retrievalState?: string; currentCancellingParty?: number | string; statusHistory?: Array<Record<string, unknown>>
}
type OperationsRow = {
  journeyId: string; journeyPublicId: string; requestId: string; requestPublicId: string; operationalAt: string; pickupTimePending: boolean
  patientName: string; patientPhone?: string; origin: string; destination: string; direction: number | Journey['direction']; reason: string
  requirements: string; status: number | JourneyStatus; provider?: string; contractCode?: string; providerReference?: string; retrievalState?: string
  externallyModified?: boolean; providerCancelled?: boolean
}
type BackendRequest = {
  id: string; publicId?: string; status: number | TransportRequest['status']; patient?: { firstName?: string; lastName?: string; phone?: string }
  reason?: { description?: string }; defaultOrigin?: BackendLocation; defaultDestination?: BackendLocation; requirements?: BackendRequirements
  contractCode?: string; providerName?: string; privateNotes?: string; providerVisibleNotes?: string; recurrence?: RecurrencePattern
  journeyRecords?: BackendJourney[]; updatedAt?: string; deliveries?: BackendDelivery[]
}
type BackendDelivery = { id: string; state: string; createdAt: string; retrievedAt?: string; attempts?: number }

const journeyStatuses: JourneyStatus[] = ['Scheduled', 'Activated', 'EnRouteToOrigin', 'ArrivedAtOrigin', 'PatientOnBoard', 'EnRouteToDestination', 'ArrivedAtDestination', 'Completed', 'Cancelled']
const requestStatuses: TransportRequest['status'][] = ['Draft', 'Active', 'Completed', 'Cancelled']
const directions: Journey['direction'][] = ['Outbound', 'Return']
const deliveryStates: DeliveryState[] = ['Pending', 'Published', 'Retrieved', 'Dead', 'NotPublished']
const locationTypes: Array<NonNullable<LocationSnapshot['type']>> = ['PrivateAddress', 'HealthcareFacility']
const enumValue = <T extends string>(value: number | string | undefined, values: T[], fallback: T): T => typeof value === 'number' ? values[value] ?? fallback : values.includes(value as T) ? value as T : fallback

function mapLocation(value?: BackendLocation): LocationSnapshot {
  return { type: enumValue(value?.type, locationTypes, 'PrivateAddress'), name: value?.name ?? '', address: value?.street, municipality: value?.municipality, phone: value?.phone, observations: value?.observations }
}

function mapRequirements(value?: BackendRequirements): Requirements {
  return {
    mobility: enumValue(value?.mobility, ['Autonomous', 'Wheelchair', 'Stretcher'], 'Autonomous'), oxygen: value?.requiresOxygen ?? value?.oxygen ?? false,
    oxygenConcentration: value?.oxygenConcentrationPercent ?? value?.oxygenConcentration, oxygenFlow: value?.oxygenFlowLitresPerMinute ?? value?.oxygenFlow,
    companion: value?.companionRequired ?? value?.companion ?? false, medicalStaff: value?.medicalStaffRequired ?? value?.medicalStaff ?? false,
    isolation: value?.isolationRequired ?? value?.isolation ?? false, bariatric: value?.bariatricRequired ?? value?.bariatric ?? false,
    stairsAssistance: value?.stairsAssistanceRequired ?? value?.stairsAssistance ?? false,
  }
}

function mapJourney(value: BackendJourney, parent?: BackendRequest): Journey {
  return {
    id: value.id, publicId: value.publicId, requestId: value.transportRequestId, requestPublicId: parent?.publicId ?? '',
    direction: enumValue(value.direction, directions, 'Outbound'), scheduledStartAt: value.schedule.scheduledStartAt, scheduledPickupAt: value.schedule.scheduledPickupAt,
    appointmentAt: value.schedule.appointmentAt, pickupTimePending: value.schedule.pickupTimePending, patientName: [parent?.patient?.firstName, parent?.patient?.lastName].filter(Boolean).join(' '),
    patientPhone: parent?.patient?.phone, origin: mapLocation(value.origin), destination: mapLocation(value.destination), reason: parent?.reason?.description ?? '',
    requirements: mapRequirements(value.requirements), status: enumValue(value.currentStatus, journeyStatuses, 'Scheduled'), provider: parent?.providerName,
    contract: parent?.contractCode, providerReference: value.providerReference, deliveryState: enumValue(value.retrievalState, deliveryStates, 'NotPublished'),
    externallyModified: value.externallyModified, cancelledBy: value.currentCancellingParty === 1 || value.currentCancellingParty === 'TransportProvider' ? 'Provider' : undefined,
    notes: value.providerVisibleNotes, statusEvents: (value.statusHistory ?? []).map((event) => ({ id: String(event.id), status: enumValue(event.status as number | string, journeyStatuses, 'Scheduled'), occurredAt: String(event.occurredAt), recordedAt: event.recordedAt ? String(event.recordedAt) : undefined, actor: event.actor ? String(event.actor) : undefined, source: event.source === 1 || event.source === 'TransportProvider' ? 'Provider' : 'Nagomi', externalResourceCode: event.externalResourceCode ? String(event.externalResourceCode) : undefined })),
  }
}

function mapOperationsRow(value: OperationsRow): Journey {
  const direction = enumValue(value.direction, directions, 'Outbound')
  return {
    id: value.journeyId, publicId: value.journeyPublicId, requestId: value.requestId, requestPublicId: value.requestPublicId, direction,
    scheduledStartAt: direction === 'Outbound' ? value.operationalAt : undefined, scheduledPickupAt: direction === 'Return' ? value.operationalAt : undefined,
    pickupTimePending: value.pickupTimePending, patientName: value.patientName, patientPhone: value.patientPhone, origin: { name: value.origin }, destination: { name: value.destination },
    reason: value.reason, requirements: { ...mapRequirements(), mobility: enumValue(value.requirements, ['Autonomous', 'Wheelchair', 'Stretcher'], 'Autonomous') },
    status: enumValue(value.status, journeyStatuses, 'Scheduled'), provider: value.provider, contract: value.contractCode, providerReference: value.providerReference,
    deliveryState: enumValue(value.retrievalState, deliveryStates, 'NotPublished'), externallyModified: value.externallyModified, cancelledBy: value.providerCancelled ? 'Provider' : undefined,
  }
}

function mapRequest(value: BackendRequest): TransportRequest {
  return {
    id: value.id, publicId: value.publicId, status: enumValue(value.status, requestStatuses, 'Draft'), patientName: [value.patient?.firstName, value.patient?.lastName].filter(Boolean).join(' '),
    patientPhone: value.patient?.phone, reason: value.reason?.description, contract: value.contractCode, provider: value.providerName,
    origin: value.defaultOrigin ? mapLocation(value.defaultOrigin) : undefined, destination: value.defaultDestination ? mapLocation(value.defaultDestination) : undefined,
    privateNotes: value.privateNotes, providerNotes: value.providerVisibleNotes, recurring: value.recurrence,
    journeys: value.journeyRecords?.map((journey) => mapJourney(journey, value)), updatedAt: value.updatedAt,
    deliveries: value.deliveries?.map((delivery) => ({ id: delivery.id, state: enumValue(delivery.state, deliveryStates, 'Pending'), createdAt: delivery.createdAt, retrievedAt: delivery.retrievedAt, attempts: delivery.attempts })),
  }
}

const emergencyStatuses: EmergencyStatus[] = ['Active', 'Completed', 'Cancelled']
type BackendEmergency = Omit<EmergencyTransport, 'status'> & { status: number | EmergencyStatus }
function mapEmergency(value: BackendEmergency): EmergencyTransport {
  return { ...value, status: enumValue(value.status, emergencyStatuses, 'Active') }
}

function backendLocation(value?: LocationSnapshot) {
  return value && { type: value.type === 'HealthcareFacility' ? 1 : 0, name: value.name, street: value.address, municipality: value.municipality, phone: value.phone, observations: value.observations }
}

function backendRequirements(value: Requirements) {
  return { mobility: ['Autonomous', 'Wheelchair', 'Stretcher'].indexOf(value.mobility), requiresOxygen: value.oxygen, oxygenConcentrationPercent: value.oxygenConcentration, oxygenFlowLitresPerMinute: value.oxygenFlow, companionRequired: value.companion, medicalStaffRequired: value.medicalStaff, isolationRequired: value.isolation, bariatricRequired: value.bariatric, stairsAssistanceRequired: value.stairsAssistance }
}

function backendDraft(value: TransportRequestDraft) {
  return { ...value, defaultOrigin: backendLocation(value.defaultOrigin), defaultDestination: backendLocation(value.defaultDestination), requirements: backendRequirements(value.requirements) }
}

function backendSchedule(value: JourneySchedule) { return value }

export const api = {
  async listAutonomousCommunities() {
    return (await request<Array<{ code: string; name: string; parentCode?: string }>>('/reference-data/autonomous-communities'))
  },
  async listProvinces(autonomousCommunityCode?: string) {
    const q = autonomousCommunityCode ? `?autonomousCommunityCode=${encodeURIComponent(autonomousCommunityCode)}` : ''
    return (await request<Array<{ code: string; name: string; parentCode?: string }>>(`/reference-data/provinces${q}`))
  },
  async listMunicipalities(provinceCode?: string, query?: string) {
    const params = new URLSearchParams()
    if (provinceCode) params.set('provinceCode', provinceCode)
    if (query) params.set('query', query)
    const qs = params.toString()
    return (await request<Array<{ code: string; name: string; parentCode?: string }>>(`/reference-data/municipalities${qs ? `?${qs}` : ''}`))
  },
  async searchHealthcareFacilities(query?: string, municipalityCode?: string, limit = 50) {
    const params = new URLSearchParams({ limit: String(limit) })
    if (query) params.set('query', query)
    if (municipalityCode) params.set('municipalityCode', municipalityCode)
    return (await request<Array<{ publicId: string; name: string; ccn?: string; codcnh?: string; officialAddressText?: string; phone?: string; address?: { municipalityCode?: string; provinceCode?: string; autonomousCommunityCode?: string; postalCode?: string } }>>(`/reference-data/healthcare-facilities?${params}`))
  },
  async listJourneys(filters: JourneyFilters) {
    const params = new URLSearchParams()
    const names: Record<string, string> = { provider: 'providerId', contract: 'contractCode', reason: 'reasonCode', originMunicipality: 'originMunicipalityCode', destinationMunicipality: 'destinationMunicipalityCode', deliveryState: 'retrievalState' }
    Object.entries(filters).forEach(([key, value]) => { if (value && !(key === 'status' && value === 'active')) params.set(names[key] ?? key, value) })
    const rows = (await request<OperationsRow[]>(`/operations/journeys?${params}`)).map(mapOperationsRow)
    return asList(filters.status === 'active' ? rows.filter((journey) => !['Completed', 'Cancelled'].includes(journey.status)) : rows)
  },
  async getJourney(id: string) { const raw = await request<BackendJourney>(`/journeys/${encodeURIComponent(id)}`); const parent = await request<BackendRequest>(`/transport-requests/${encodeURIComponent(raw.transportRequestId)}`); return mapJourney(raw, parent) },
  async updateJourney(id: string, body: Journey) {
    const command = { origin: backendLocation(body.origin), destination: backendLocation(body.destination), requirements: backendRequirements(body.requirements), schedule: backendSchedule({ appointmentAt: body.appointmentAt, scheduledStartAt: body.scheduledStartAt ?? '', scheduledPickupAt: body.scheduledPickupAt, pickupTimePending: body.pickupTimePending ?? false }), providerVisibleNotes: body.notes, providerReference: body.providerReference, source: 0, actor: 'simulated-user' }
    return { ...body, ...mapJourney(await request<BackendJourney>(`/journeys/${encodeURIComponent(id)}/snapshot`, { method: 'PUT', body: JSON.stringify(command) })), patientName: body.patientName, patientPhone: body.patientPhone, reason: body.reason, provider: body.provider, contract: body.contract, requestPublicId: body.requestPublicId }
  },
  cancelJourney: (id: string) => request<void>(`/journeys/${encodeURIComponent(id)}/cancel`, { method: 'POST', body: JSON.stringify({ reason: 0, cancellingParty: 0, source: 0, actor: 'simulated-user' }) }),
  async listRequests(search = '') {
    const rows = await request<BackendRequest[]>(`/operations/requests?search=${encodeURIComponent(search)}`)
    return asList(rows.map(mapRequest))
  },
  async getRequest(id: string) { return mapRequest(await request<BackendRequest>(`/transport-requests/${encodeURIComponent(id)}`)) },
  async saveDraft(body: TransportRequestDraft) { return mapRequest(await request<BackendRequest>('/transport-requests/drafts', { method: 'POST', body: JSON.stringify(backendDraft(body)) })) },
  async updateRequest(id: string, body: TransportRequestDraft, propagateToJourneys = false, overwriteExceptions = false) {
    const command = { snapshot: backendDraft(body), propagateToJourneys, overwriteExceptions, source: 0, actor: 'simulated-user' }
    return mapRequest(await request<BackendRequest>(`/transport-requests/${encodeURIComponent(id)}/snapshot`, { method: 'PUT', body: JSON.stringify(command) }))
  },
  async submitRequest(draft: TransportRequestDraft, submission: TransportRequestSubmission) {
    const created = await request<BackendRequest>('/transport-requests/drafts', { method: 'POST', body: JSON.stringify(backendDraft(draft)) })
    const command = submission.kind === 'oneOff' ? { outbound: backendSchedule(submission.outbound), return: submission.return && backendSchedule(submission.return) } : { recurrence: submission.recurrence }
    const submitted = await request<BackendRequest>(`/transport-requests/${encodeURIComponent(created.id)}/submit/${submission.kind === 'oneOff' ? 'one-off' : 'recurring'}`, { method: 'POST', body: JSON.stringify(command) })
    return mapRequest(submitted)
  },
  previewRecurrence: (id: string, body: unknown) => request<{ additions: number; cancellations: number; exceptions: number }>(`/transport-requests/${encodeURIComponent(id)}/recurrence/preview`, { method: 'POST', body: JSON.stringify(body) }),
  applyRecurrence: (id: string, recurrence: RecurrencePattern, overwriteExceptions: boolean) => request<void>(`/transport-requests/${encodeURIComponent(id)}/recurrence/apply`, { method: 'POST', body: JSON.stringify({ recurrence, overwriteExceptions }) }),
  cancelRequest: (id: string) => request<void>(`/transport-requests/${encodeURIComponent(id)}/cancel`, { method: 'POST', body: JSON.stringify({ reason: 0, cancellingParty: 0, source: 0, actor: 'simulated-user' }) }),
  addJourneyStatus: (id: string, status: JourneyStatus, occurredAt: string, idempotencyKey: string) => request<void>(`/journeys/${encodeURIComponent(id)}/statuses`, { method: 'POST', body: JSON.stringify({ status: journeyStatuses.indexOf(status), occurredAt, idempotencyKey, source: 0, actor: 'simulated-user' }) }),
  async listEmergencies() { return (await request<BackendEmergency[]>('/emergency-transports')).map(mapEmergency) },
  async getEmergency(id: string) { return mapEmergency(await request<BackendEmergency>(`/emergency-transports/${encodeURIComponent(id)}`)) },
  async createEmergency(draft: EmergencyDraft) { return mapEmergency(await request<BackendEmergency>('/emergency-transports', { method: 'POST', body: JSON.stringify(draft) })) },
  cancelEmergency: async (id: string) => mapEmergency(await request<BackendEmergency>(`/emergency-transports/${encodeURIComponent(id)}/cancel`, { method: 'POST' })),
}
