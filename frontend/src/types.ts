export type JourneyStatus = 'Scheduled' | 'Activated' | 'EnRouteToOrigin' | 'ArrivedAtOrigin' | 'PatientOnBoard' | 'EnRouteToDestination' | 'ArrivedAtDestination' | 'Completed' | 'Cancelled'
export type DeliveryState = 'Pending' | 'Published' | 'Retrieved' | 'Dead' | 'NotPublished'

export interface LocationSnapshot {
  type?: 'PrivateAddress' | 'HealthcareFacility'
  name: string
  address?: string
  municipality?: string
  phone?: string
  observations?: string
}

export interface Requirements {
  mobility: 'Autonomous' | 'Wheelchair' | 'Stretcher'
  oxygen: boolean
  oxygenConcentration?: number
  oxygenFlow?: number
  companion: boolean
  medicalStaff: boolean
  isolation: boolean
  bariatric: boolean
  stairsAssistance: boolean
}

export interface Journey {
  id: string
  publicId: string
  requestId: string
  requestPublicId: string
  direction: 'Outbound' | 'Return'
  scheduledStartAt?: string
  scheduledPickupAt?: string
  appointmentAt?: string
  pickupTimePending?: boolean
  patientName?: string
  patientPhone?: string
  origin: LocationSnapshot
  destination: LocationSnapshot
  reason: string
  requirements: Requirements
  status: JourneyStatus
  provider?: string
  contract?: string
  providerReference?: string
  deliveryState: DeliveryState
  externallyModified?: boolean
  cancelledBy?: 'Requester' | 'Provider'
  vehicleId?: string
  vehicleName?: string
  notes?: string
  statusEvents?: StatusEvent[]
  audit?: AuditEntry[]
}

export interface TransportRequest {
  id: string
  publicId?: string
  status: 'Draft' | 'Active' | 'Completed' | 'Cancelled'
  patientName?: string
  patientPhone?: string
  reason?: string
  contract?: string
  provider?: string
  origin?: LocationSnapshot
  destination?: LocationSnapshot
  privateNotes?: string
  providerNotes?: string
  recurring?: RecurrencePattern
  journeys?: Journey[]
  audit?: AuditEntry[]
  deliveries?: DeliveryRecord[]
  updatedAt?: string
}

export interface JourneySchedule {
  appointmentAt?: string
  scheduledStartAt: string
  scheduledPickupAt?: string
  pickupTimePending: boolean
}

export interface RecurrencePattern {
  startDate: string
  endDate: string
  utcOffset: string
  weekdaySchedules: Array<{
    dayOfWeek: number
    outboundAppointmentTime: string
    outboundStartTime?: string
    outboundPickupTime?: string
    returnPickupTime?: string
    returnPickupNextDay: boolean
    returnPickupTimePending: boolean
  }>
}

export interface TransportRequestDraft {
  patient: { firstName?: string; lastName?: string; documentNumber?: string; healthCardNumber?: string; phone?: string }
  reason?: { code: string; description: string }
  defaultOrigin?: LocationSnapshot
  defaultDestination?: LocationSnapshot
  requirements: Requirements
  contractCode?: string
  clientId?: string
  clientName?: string
  providerName?: string
  privateNotes?: string
  providerVisibleNotes?: string
}

export type TransportRequestSubmission =
  | { kind: 'oneOff'; outbound: JourneySchedule; return?: JourneySchedule }
  | { kind: 'recurring'; recurrence: RecurrencePattern }

export interface StatusEvent { id: string; status: JourneyStatus; occurredAt: string; recordedAt?: string; actor?: string; source?: string; externalResourceCode?: string; latitude?: number; longitude?: number }
export interface AuditEntry { id: string; action: string; actor: string; source: string; occurredAt: string; changes?: string[] }
export interface DeliveryRecord { id: string; state: DeliveryState; createdAt: string; retrievedAt?: string; attempts?: number }

export interface Vehicle {
  id: string
  publicId: string
  name: string
  externalCode?: string
  isActive: boolean
  createdAt: string
}

export type JourneyDirection = 'Outbound' | 'Return'

export interface StatusPoint {
  id: string
  status: JourneyStatus
  occurredAt: string
  actor?: string
  externalResourceCode?: string
  latitude?: number
  longitude?: number
}

export interface CoordinationRow {
  journeyId: string
  journeyPublicId: string
  requestId: string
  requestPublicId: string
  direction: JourneyDirection
  operationalAt: string
  patientName: string
  origin: string
  destination: string
  status: JourneyStatus
  vehicleId?: string
  vehiclePublicId?: string
  vehicleName?: string
  statusPoints: StatusPoint[]
}

export interface ListResponse<T> { items: T[]; total?: number }

export type EmergencyStatus = 'Active' | 'Completed' | 'Cancelled'

export interface IncidentLocation {
  latitude: number
  longitude: number
  address?: string
  municipality?: string
  notes?: string
}

export interface EmergencyTransport {
  id: string
  publicId: string
  status: EmergencyStatus
  reason: string
  contactPhone?: string
  incident: IncidentLocation
  observations?: string
  createdAt: string
  updatedAt: string
}

export interface EmergencyDraft {
  reason: string
  incident: IncidentLocation
  contactPhone?: string
  observations?: string
}

export interface JourneyFilters {
  from: string
  to: string
  status: string
  provider: string
  contract: string
  direction: string
  reason: string
  originMunicipality: string
  destinationMunicipality: string
  deliveryState: string
  search: string
}

export interface TenantCapabilities {
  publishesRequests: boolean
  executesTransports: boolean
  handlesEmergencies: boolean
}

export interface TransportClient {
  id: string
  publicId: string
  name: string
  taxId?: string
  contactPerson?: string
  phone?: string
  email?: string
  address?: string
  isActive: boolean
  createdAt: string
}

export interface QueueSnapshot {
  providerId: string
  providerCode: string
  providerName: string
  queueName: string
  messages: number
  messagesReady: number
  messagesUnacknowledged: number
  consumers: number
  error?: string
}

export interface QueueMessageSample {
  deliveryTag: number
  messageId: string
  messageType: string
  entityPublicId?: string
  contractCode?: string
  redelivered: boolean
  body: string
  timestamp?: string
}
