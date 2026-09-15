import { useMemo, useState } from 'react'
import { CheckCircle, Gavel, Prohibit, UserMinus } from '@phosphor-icons/react'
import { api } from '../api'
import type { CancellationReason, Journey, Vehicle } from '../types'
import { VEHICLE_TYPE_LABELS } from '../types'
import { formatDateTime } from '../utils'

/**
 * Panel de vehículo de un traslado. Es la única puerta para cambiar el vehículo, porque
 * asignar y adjudicar NO son lo mismo:
 *
 *  - ASIGNAR propone un vehículo (placeholder). No compromete nada, no publica al
 *    proveedor y no genera solicitud en Rabbit.
 *  - ADJUDICAR compromete el vehículo con el traslado: sólo entonces se genera la
 *    solicitud al proveedor y éste puede hacer retrieve.
 *
 * Si el traslado ya está adjudicado, aquí se puede desadjudicar (para cambiar de
 * vehículo) o anular el traslado.
 */
// Motivos reales del enum del backend (una tabla maestra de motivos es trabajo futuro).
const CANCEL_REASONS: { value: CancellationReason; label: string }[] = [
  { value: 'NoLongerRequired', label: 'Alta' },
  { value: 'MedicalReason', label: 'Tratamiento programado' },
  { value: 'ProviderUnavailable', label: 'Sin vehículo disponible' },
  { value: 'PatientUnavailable', label: 'El paciente no está disponible' },
  { value: 'SchedulingConflict', label: 'Cambio de agenda' },
  { value: 'Other', label: 'Otro motivo' },
]

export function JourneyAssignmentPanel({ journey, vehicles, onClose, onChanged }: {
  journey: Journey
  vehicles: Vehicle[]
  onClose: () => void
  onChanged: () => void
}) {
  const [vehicleId, setVehicleId] = useState(journey.vehicleId ?? '')
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const [cancelling, setCancelling] = useState(false)
  const [reason, setReason] = useState<CancellationReason>('NoLongerRequired')

  const adjudicated = !!journey.vehicleAdjudicated
  const active = useMemo(() => vehicles.filter((vehicle) => vehicle.isActive !== false), [vehicles])
  const selected = active.find((vehicle) => vehicle.id === vehicleId)

  async function run(action: () => Promise<unknown>, ok: string) {
    setBusy(true); setMessage(''); setError('')
    try { await action(); setMessage(ok); onChanged() }
    catch (caught) { setError(caught instanceof Error ? caught.message : 'No se pudo completar la acción.') }
    finally { setBusy(false) }
  }

  return <div className="modal-backdrop" role="presentation" onClick={(event) => { if (event.target === event.currentTarget) onClose() }}>
    <section className="assignment-panel" role="dialog" aria-label={`Vehículo de ${journey.publicId}`}>
      <header className="assignment-panel-head">
        <div>
          <h2>Vehículo del traslado</h2>
          <p>{journey.publicId} · {journey.patientName || 'Paciente sin identificar'} · {formatDateTime(journey.direction === 'Return' ? journey.scheduledPickupAt : journey.scheduledStartAt)}</p>
        </div>
        <button className="icon-button" onClick={onClose} aria-label="Cerrar el panel">✕</button>
      </header>

      <p className="assignment-panel-state">
        {adjudicated
          ? <>Adjudicado{journey.vehicleName ? ` con ${journey.vehicleName}` : ''}{journey.adjudicatedBy ? ` por ${journey.adjudicatedBy}` : ''}. El proveedor ya recibió la solicitud.</>
          : journey.vehicleId
            ? <>Vehículo asignado{journey.vehicleName ? `: ${journey.vehicleName}` : ''}. Al adjudicar se envía la solicitud al proveedor.</>
            : <>Sin vehículo asignado.</>}
      </p>

      {message && <div className="alert alert-success" role="status"><CheckCircle size={14} aria-hidden="true" /> {message}</div>}
      {error && <div className="alert alert-error" role="alert">{error}</div>}

      <label className="field"><span>Vehículo</span>
        <select value={vehicleId} onChange={(event) => setVehicleId(event.target.value)} disabled={busy}>
          <option value="">Sin vehículo</option>
          {active.map((vehicle) => <option key={vehicle.id} value={vehicle.id}>{vehicle.name} · {VEHICLE_TYPE_LABELS[vehicle.vehicleType]}</option>)}
        </select>
      </label>
      {selected && <p className="assignment-panel-hint">{selected.publicId}{selected.externalCode ? ` · ${selected.externalCode}` : ''}</p>}

      <div className="assignment-panel-actions">
        <button className="button button-secondary" type="button" disabled={busy || !vehicleId}
          onClick={() => void run(() => api.assignJourneyVehicle(journey.id, vehicleId), 'Vehículo asignado (propuesta, sin comprometer).')}>
          Asignar
        </button>
        <button className="button button-primary" type="button" disabled={busy || !vehicleId}
          onClick={() => void run(() => api.adjudicateJourneyVehicle(journey.id, vehicleId), 'Traslado adjudicado: solicitud enviada al proveedor.')}>
          <Gavel size={14} aria-hidden="true" /> {adjudicated ? 'Adjudicar a este vehículo' : 'Adjudicar'}
        </button>
      </div>
      <p className="assignment-panel-hint">Asignar no compromete el servicio. Adjudicar sí: genera la solicitud en Rabbit y habilita el retrieve.</p>

      {adjudicated && <div className="assignment-panel-danger">
        <div className="assignment-panel-danger-head">
          <strong>Cambiar de vehículo</strong>
          <button className="button button-secondary" type="button" disabled={busy}
            onClick={() => void run(() => api.unadjudicateJourneyVehicle(journey.id), 'Traslado desadjudicado: ya puedes elegir otro vehículo.')}>
            <UserMinus size={14} aria-hidden="true" /> Desadjudicar
          </button>
        </div>
        <p className="assignment-panel-hint">Desadjudicar libera el vehículo y retira la solicitud del proveedor. El traslado sigue existiendo.</p>

        {!cancelling ? <button className="button button-danger" type="button" onClick={() => setCancelling(true)} disabled={busy}>
          <Prohibit size={14} aria-hidden="true" /> Anular traslado
        </button> : <div className="assignment-panel-cancel">
          <label className="field"><span>Motivo de anulación</span>
            <select value={reason} onChange={(event) => setReason(event.target.value as CancellationReason)} disabled={busy}>
              {CANCEL_REASONS.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
            </select>
          </label>
          <div className="assignment-panel-actions">
            <button className="button button-danger" type="button" disabled={busy}
              onClick={() => void run(() => api.cancelJourney(journey.id, reason), 'Traslado anulado.').then(onClose)}>Confirmar anulación</button>
            <button className="button" type="button" onClick={() => setCancelling(false)} disabled={busy}>Volver</button>
          </div>
        </div>}
      </div>}
    </section>
  </div>
}