import { useState } from 'react'
import { useNavigate } from '../router'
import { api } from '../api'
import { PageHeader } from '../components/States'
import type { JourneySchedule, RecurrencePattern, TransportRequestDraft, TransportRequestSubmission } from '../types'

const weekdays = [['monday', 'Lunes'], ['tuesday', 'Martes'], ['wednesday', 'Miércoles'], ['thursday', 'Jueves'], ['friday', 'Viernes'], ['saturday', 'Sábado'], ['sunday', 'Domingo']]
const dayNumbers: Record<string, number> = { sunday: 0, monday: 1, tuesday: 2, wednesday: 3, thursday: 4, friday: 5, saturday: 6 }

function offsetDateTime(value: FormDataEntryValue | undefined) {
  if (!value) return undefined
  const date = new Date(String(value)); const offset = -date.getTimezoneOffset(); const sign = offset >= 0 ? '+' : '-'
  return `${String(value)}:00${sign}${String(Math.floor(Math.abs(offset) / 60)).padStart(2, '0')}:${String(Math.abs(offset) % 60).padStart(2, '0')}`
}

function utcOffset() {
  const minutes = -new Date().getTimezoneOffset(); const sign = minutes >= 0 ? '' : '-'
  return `${sign}${String(Math.floor(Math.abs(minutes) / 60)).padStart(2, '0')}:${String(Math.abs(minutes) % 60).padStart(2, '0')}:00`
}

export function RequestFormPage() {
  const navigate = useNavigate()
  const [mode, setMode] = useState<'oneOff' | 'recurring'>('oneOff')
  const [roundTrip, setRoundTrip] = useState(false)
  const [oxygen, setOxygen] = useState(false)
  const [selectedDays, setSelectedDays] = useState<string[]>([])
  const [saving, setSaving] = useState<'draft' | 'submit' | ''>('')
  const [message, setMessage] = useState('')

  async function save(event: React.FormEvent<HTMLFormElement>, submit: boolean) {
    event.preventDefault(); setSaving(submit ? 'submit' : 'draft'); setMessage('')
    const data = Object.fromEntries(new FormData(event.currentTarget))
    const patientParts = String(data.patientName ?? '').trim().split(/\s+/); const lastName = patientParts.length > 1 ? patientParts.pop() : undefined
    const location = (prefix: 'origin' | 'destination') => ({ type: String(data[`${prefix}Type`]) as 'PrivateAddress' | 'HealthcareFacility', name: String(data[`${prefix}Name`] ?? ''), address: String(data[`${prefix}Address`] ?? '') || undefined, municipality: String(data[`${prefix}Municipality`] ?? '') || undefined, observations: String(data[`${prefix}Observations`] ?? '') || undefined })
    const draft: TransportRequestDraft = {
      patient: { firstName: patientParts.join(' ') || undefined, lastName, documentNumber: String(data.patientDocument ?? '') || undefined, healthCardNumber: String(data.healthCard ?? '') || undefined, phone: String(data.patientPhone ?? '') || undefined },
      reason: data.reason ? { code: String(data.reason), description: String(data.reason) } : undefined,
      defaultOrigin: data.originName ? location('origin') : undefined, defaultDestination: data.destinationName ? location('destination') : undefined,
      requirements: { mobility: String(data.mobility) as 'Autonomous' | 'Wheelchair' | 'Stretcher', oxygen, oxygenConcentration: data.oxygenConcentration ? Number(data.oxygenConcentration) : undefined, oxygenFlow: data.oxygenFlow ? Number(data.oxygenFlow) : undefined, companion: data.companion === 'on', medicalStaff: data.medicalStaff === 'on', isolation: data.isolation === 'on', bariatric: data.bariatric === 'on', stairsAssistance: data.stairsAssistance === 'on' },
      contractCode: String(data.contract ?? '') || undefined, providerName: String(data.provider ?? '') || undefined, privateNotes: String(data.privateNotes ?? '') || undefined, providerVisibleNotes: String(data.providerNotes ?? '') || undefined,
    }
    const appointmentAt = offsetDateTime(data.appointmentAt); const startAt = offsetDateTime(data.scheduledStartAt) ?? (appointmentAt ? new Date(new Date(appointmentAt).getTime() - 3_600_000).toISOString() : '')
    const outbound: JourneySchedule = { appointmentAt, scheduledStartAt: startAt, pickupTimePending: false }
    let submission: TransportRequestSubmission
    if (mode === 'oneOff') {
      const pending = data.pickupTimePending === 'on'; const returnAt = pending ? `${String(data.appointmentAt).slice(0, 10)}T23:59:00${appointmentAt?.slice(-6)}` : offsetDateTime(data.returnPickupAt)
      submission = { kind: 'oneOff', outbound, return: roundTrip && returnAt ? { scheduledStartAt: returnAt, scheduledPickupAt: returnAt, pickupTimePending: pending } : undefined }
    } else {
      const recurrence: RecurrencePattern = { startDate: String(data.recurrenceStart), endDate: String(data.recurrenceEnd), utcOffset: utcOffset(), weekdaySchedules: selectedDays.map((day) => ({ dayOfWeek: dayNumbers[day], outboundAppointmentTime: `${String(data.recurringAppointmentTime)}:00`, returnPickupTime: roundTrip && data.recurringReturnTime ? `${String(data.recurringReturnTime)}:00` : undefined, returnPickupNextDay: false, returnPickupTimePending: false })) }
      submission = { kind: 'recurring', recurrence }
    }
    try { const result = submit ? await api.submitRequest(draft, submission) : await api.saveDraft(draft); navigate(`/solicitudes/${result.id}`) }
    catch (e) { setMessage(e instanceof Error ? e.message : 'No se pudo guardar la solicitud.') }
    finally { setSaving('') }
  }

  return <div className="page form-page">
    <PageHeader eyebrow="Nueva solicitud" title="Preparar transporte" description="Guarda información incompleta como borrador o revisa todos los datos antes de enviar al proveedor." />
    <form className="request-form" onSubmit={(e) => void save(e, true)}>
      {message && <div className="form-error" role="alert">{message}</div>}
      <section className="form-section"><div className="section-number">01</div><div className="section-heading"><h2>Paciente y motivo</h2><p>Los identificadores sensibles solo aparecen en el detalle, nunca en listados operativos.</p></div><div className="field-grid">
        <label><span>Nombre y apellidos</span><input name="patientName" autoComplete="name" /></label><label><span>Teléfono</span><input name="patientPhone" type="tel" autoComplete="tel" /></label><label><span>Documento de identidad</span><input name="patientDocument" /></label><label><span>Tarjeta sanitaria</span><input name="healthCard" /></label><label className="span-2"><span>Motivo del transporte *</span><select name="reason" required defaultValue=""><option value="" disabled>Selecciona un motivo</option><option>Consulta externa</option><option>Alta hospitalaria</option><option>Tratamiento programado</option><option>Traslado entre centros</option></select></label>
      </div></section>
      <section className="form-section"><div className="section-number">02</div><div className="section-heading"><h2>Ruta operativa</h2><p>Al menos uno de los puntos debe ser un centro sanitario.</p></div><div className="locations-grid">
        <fieldset><legend>Origen</legend><label><span>Tipo *</span><select name="originType" required><option value="HealthcareFacility">Centro sanitario</option><option value="PrivateAddress">Domicilio</option></select></label><label><span>Centro o referencia *</span><input name="originName" required /></label><label><span>Dirección</span><input name="originAddress" /></label><label><span>Municipio</span><input name="originMunicipality" /></label><label><span>Indicaciones de recogida</span><textarea name="originObservations" rows={2} /></label></fieldset>
        <fieldset><legend>Destino</legend><label><span>Tipo *</span><select name="destinationType" required><option value="HealthcareFacility">Centro sanitario</option><option value="PrivateAddress">Domicilio</option></select></label><label><span>Centro o referencia *</span><input name="destinationName" required /></label><label><span>Dirección</span><input name="destinationAddress" /></label><label><span>Municipio</span><input name="destinationMunicipality" /></label><label><span>Indicaciones de llegada</span><textarea name="destinationObservations" rows={2} /></label></fieldset>
      </div></section>
      <section className="form-section"><div className="section-number">03</div><div className="section-heading"><h2>Necesidades del traslado</h2><p>La movilidad en silla y camilla es excluyente.</p></div><div className="field-grid">
        <label><span>Movilidad *</span><select name="mobility" defaultValue="Autonomous"><option value="Autonomous">Autónomo</option><option value="Wheelchair">Silla de ruedas</option><option value="Stretcher">Camilla</option></select></label><label className="check-line"><input type="checkbox" checked={oxygen} onChange={(e) => setOxygen(e.target.checked)} /><span>Necesita oxígeno</span></label>{oxygen && <><label><span>Concentración (%)</span><input name="oxygenConcentration" type="number" min="0" max="100" /></label><label><span>Flujo (l/min)</span><input name="oxygenFlow" type="number" min="0" step="0.1" /></label></>}
        <div className="checkbox-grid span-2">{[['companion', 'Acompañante'], ['medicalStaff', 'Personal sanitario'], ['isolation', 'Aislamiento'], ['bariatric', 'Bariátrico'], ['stairsAssistance', 'Ayuda en escaleras']].map(([name, label]) => <label className="check-line" key={name}><input name={name} type="checkbox" /><span>{label}</span></label>)}</div>
      </div></section>
      <section className="form-section"><div className="section-number">04</div><div className="section-heading"><h2>Programación</h2><p>La ida se planifica por cita; la vuelta puede quedar con hora pendiente.</p></div><div>
        <div className="segmented" role="radiogroup" aria-label="Tipo de programación"><button type="button" aria-pressed={mode === 'oneOff'} onClick={() => setMode('oneOff')}>Una fecha</button><button type="button" aria-pressed={mode === 'recurring'} onClick={() => setMode('recurring')}>Recurrente</button></div>
        {mode === 'oneOff' ? <div className="field-grid schedule-fields"><label><span>Fecha y hora de cita *</span><input name="appointmentAt" type="datetime-local" required /></label><label><span>Inicio previsto</span><input name="scheduledStartAt" type="datetime-local" /><small>Si se deja vacío, será una hora antes.</small></label><label className="check-line"><input type="checkbox" checked={roundTrip} onChange={(e) => setRoundTrip(e.target.checked)} /><span>Incluir vuelta</span></label>{roundTrip && <><label><span>Recogida de vuelta</span><input name="returnPickupAt" type="datetime-local" /></label><label className="check-line"><input name="pickupTimePending" type="checkbox" /><span>Hora de vuelta pendiente</span></label></>}</div> : <div className="recurrence-panel"><div className="field-grid"><label><span>Desde *</span><input name="recurrenceStart" type="date" required /></label><label><span>Hasta * (máximo 6 meses)</span><input name="recurrenceEnd" type="date" required /></label></div><fieldset className="weekday-picker"><legend>Días de servicio *</legend>{weekdays.map(([value, label]) => <label key={value}><input type="checkbox" aria-label={label} checked={selectedDays.includes(value)} onChange={(e) => setSelectedDays((days) => e.target.checked ? [...days, value] : days.filter((day) => day !== value))} /><span>{label.slice(0, 2)}</span><small>{label}</small></label>)}</fieldset><div className="field-grid"><label><span>Hora de cita *</span><input name="recurringAppointmentTime" type="time" required /></label><label className="check-line"><input type="checkbox" checked={roundTrip} onChange={(e) => setRoundTrip(e.target.checked)} /><span>Incluir vuelta recurrente</span></label>{roundTrip && <label><span>Recogida de vuelta</span><input name="recurringReturnTime" type="time" /></label>}</div></div>}
      </div></section>
      <section className="form-section"><div className="section-number">05</div><div className="section-heading"><h2>Asignación y notas</h2><p>Las notas privadas nunca se comparten con el proveedor.</p></div><div className="field-grid"><label><span>Contrato *</span><select name="contract" required defaultValue=""><option value="" disabled>Selecciona un contrato</option><option value="CTR-MAD-01">CTR-MAD-01 · Transporte sanitario Madrid</option><option value="CTR-SIN-RUTA">Sin ruta activa</option></select></label><label><span>Proveedor</span><input name="provider" placeholder="Asignado por el contrato" readOnly /></label><label className="span-2"><span>Notas para el proveedor</span><textarea name="providerNotes" rows={3} /></label><label className="span-2"><span>Notas privadas</span><textarea name="privateNotes" rows={3} /></label></div></section>
      <div className="form-actions"><button className="button button-secondary" type="button" disabled={!!saving} onClick={(e) => { const form = e.currentTarget.form; if (form) void save({ preventDefault: () => undefined, currentTarget: form } as unknown as React.FormEvent<HTMLFormElement>, false) }}>{saving === 'draft' ? 'Guardando…' : 'Guardar borrador'}</button><button className="button button-accent" disabled={!!saving || (mode === 'recurring' && !selectedDays.length)}>{saving === 'submit' ? 'Enviando…' : 'Revisar y enviar solicitud'}</button></div>
    </form>
  </div>
}
