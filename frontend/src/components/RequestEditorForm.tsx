import { useEffect, useMemo, useState } from 'react'
import { api } from '../api'
import type { LocationSnapshot, TransportRequest, TransportRequestDraft } from '../types'

interface Option { code: string; name: string }
interface LocationState extends LocationSnapshot { type: 'PrivateAddress' | 'HealthcareFacility' }

const emptyLocation = (): LocationState => ({ type: 'HealthcareFacility', name: '', address: '', municipality: '', municipalityCode: '', province: '', provinceCode: '', phone: '', observations: '' })

function splitName(full: string) {
  const parts = full.trim().split(/\s+/)
  const lastName = parts.length > 1 ? parts.pop() : undefined
  return { firstName: parts.join(' ') || undefined, lastName }
}

export function RequestEditorForm({ request, onSaved, onClose }: { request: TransportRequest; onSaved: () => void; onClose: () => void }) {
  const [patientName, setPatientName] = useState(request.patientName ?? '')
  const [patientPhone, setPatientPhone] = useState(request.patientPhone ?? '')
  const [reason, setReason] = useState(request.reason ?? '')
  const [origin, setOrigin] = useState<LocationState>(() => ({ ...emptyLocation(), ...request.origin, type: request.origin?.type ?? 'HealthcareFacility' }))
  const [destination, setDestination] = useState<LocationState>(() => ({ ...emptyLocation(), ...request.destination, type: request.destination?.type ?? 'HealthcareFacility' }))
  const [requirements, setRequirements] = useState(request.journeys?.[0]?.requirements ?? { mobility: 'Autonomous' as const, oxygen: false, companion: false, medicalStaff: false, isolation: false, bariatric: false, stairsAssistance: false })
  const [privateNotes, setPrivateNotes] = useState(request.privateNotes ?? '')
  const [providerNotes, setProviderNotes] = useState(request.providerNotes ?? '')
  const [propagate, setPropagate] = useState(true)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState('')
  const [provinces, setProvinces] = useState<Option[]>([])

  const [originMunicipalities, setOriginMunicipalities] = useState<Option[]>([])
  const [destinationMunicipalities, setDestinationMunicipalities] = useState<Option[]>([])

  useEffect(() => { api.listProvinces().then(setProvinces).catch(() => setProvinces([])) }, [])

  useEffect(() => {
    if (!origin.provinceCode) { setOriginMunicipalities([]); return }
    api.listMunicipalities(origin.provinceCode).then(setOriginMunicipalities).catch(() => setOriginMunicipalities([]))
  }, [origin.provinceCode])
  useEffect(() => {
    if (!destination.provinceCode) { setDestinationMunicipalities([]); return }
    api.listMunicipalities(destination.provinceCode).then(setDestinationMunicipalities).catch(() => setDestinationMunicipalities([]))
  }, [destination.provinceCode])

  const provinceName = useMemo(() => {
    const map = new Map(provinces.map((p) => [p.code, p.name]))
    return (code?: string) => code ? (map.get(code) ?? '') : ''
  }, [provinces])

  function patchLocation(which: 'origin' | 'destination', patch: Partial<LocationState>) {
    const setter = which === 'origin' ? setOrigin : setDestination
    setter((prev) => ({ ...prev, ...patch }))
  }

  async function save(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault(); setSaving(true); setMessage('')
    const draft: TransportRequestDraft = {
      patient: { ...splitName(patientName), phone: patientPhone || undefined },
      reason: reason ? { code: reason, description: reason } : undefined,
      defaultOrigin: origin.name || origin.address ? { ...origin } : undefined,
      defaultDestination: destination.name || destination.address ? { ...destination } : undefined,
      requirements,
      privateNotes: privateNotes || undefined,
      providerVisibleNotes: providerNotes || undefined,
      contractCode: request.contract,
      providerName: request.provider,
    }
    try {
      await api.updateRequest(request.id, draft, propagate, false)
      setMessage('Traslado actualizado.')
      onSaved()
    } catch (e) { setMessage(e instanceof Error ? e.message : 'No se pudo guardar.') }
    finally { setSaving(false) }
  }

  return <form className="request-form edit-form" onSubmit={(e) => void save(e)}>
    {message && <div className="form-error" role="alert">{message}</div>}
    <section className="form-section"><div className="section-number">✎</div><div className="section-heading"><h2>Paciente y motivo</h2><p>Actualiza los datos del paciente y el motivo del traslado.</p></div><div className="field-grid">
      <label><span>Nombre y apellidos</span><input value={patientName} onChange={(e) => setPatientName(e.target.value)} autoComplete="name" /></label>
      <label><span>Teléfono</span><input value={patientPhone} onChange={(e) => setPatientPhone(e.target.value)} type="tel" /></label>
      <label className="span-2"><span>Motivo del transporte</span><select value={reason} onChange={(e) => setReason(e.target.value)}><option value="">— Sin motivo —</option><option>Consulta externa</option><option>Alta hospitalaria</option><option>Tratamiento programado</option><option>Traslado entre centros</option></select></label>
    </div></section>
    <section className="form-section"><div className="section-number">✎</div><div className="section-heading"><h2>Ruta operativa</h2><p>Origen y destino con provincia y población; los centros del catálogo se rellenan solos.</p></div>
      {(['origin', 'destination'] as const).map((which) => {
        const value = which === 'origin' ? origin : destination
        const municipalities = which === 'origin' ? originMunicipalities : destinationMunicipalities
        const set = (patch: Partial<LocationState>) => patchLocation(which, patch)
        return <fieldset key={which} className="edit-location-fieldset"><legend>{which === 'origin' ? 'Origen' : 'Destino'}</legend>
          <label><span>Tipo</span><select value={value.type} onChange={(e) => set({ type: e.target.value as LocationState['type'] })}><option value="HealthcareFacility">Centro sanitario</option><option value="PrivateAddress">Domicilio</option></select></label>
          <label><span>Provincia</span><select value={value.provinceCode ?? ''} onChange={(e) => { const code = e.target.value; set({ provinceCode: code, province: provinceName(code), municipalityCode: '', municipality: '' }) }}><option value="">Selecciona provincia</option>{provinces.map((p) => <option key={p.code} value={p.code}>{p.name}</option>)}</select></label>
          <label><span>Población</span><select value={value.municipalityCode ?? ''} onChange={(e) => { const code = e.target.value; const m = municipalities.find((x) => x.code === code); set({ municipalityCode: code, municipality: m?.name ?? '' }) }}><option value="">{value.provinceCode ? 'Selecciona población' : 'Primero elige provincia'}</option>{municipalities.map((m) => <option key={m.code} value={m.code}>{m.name}</option>)}</select></label>
          <label><span>Nombre del lugar</span><input value={value.name} onChange={(e) => set({ name: e.target.value })} placeholder="Hospital, centro o domicilio" /></label>
          <label><span>Dirección</span><input value={value.address ?? ''} onChange={(e) => set({ address: e.target.value })} placeholder="Calle, número" /></label>
          <label><span>Teléfono</span><input value={value.phone ?? ''} onChange={(e) => set({ phone: e.target.value })} type="tel" /></label>
          <label className="span-2"><span>Indicaciones</span><textarea value={value.observations ?? ''} onChange={(e) => set({ observations: e.target.value })} rows={2} /></label>
        </fieldset>
      })}
    </section>
    <section className="form-section"><div className="section-number">✎</div><div className="section-heading"><h2>Requisitos del servicio</h2><p>Recursos necesarios a bordo.</p></div><div className="field-grid">
      <label><span>Movilidad</span><select value={requirements.mobility} onChange={(e) => setRequirements({ ...requirements, mobility: e.target.value as 'Autonomous' | 'Wheelchair' | 'Stretcher' })}><option value="Autonomous">Autónomo</option><option value="Wheelchair">Silla de ruedas</option><option value="Stretcher">Camilla</option></select></label>
      <label className="checkbox-row"><input type="checkbox" checked={requirements.oxygen} onChange={(e) => setRequirements({ ...requirements, oxygen: e.target.checked })} /><span>Oxígeno a bordo</span></label>
      <label className="checkbox-row"><input type="checkbox" checked={requirements.companion} onChange={(e) => setRequirements({ ...requirements, companion: e.target.checked })} /><span>Acompañante</span></label>
      <label className="checkbox-row"><input type="checkbox" checked={requirements.medicalStaff} onChange={(e) => setRequirements({ ...requirements, medicalStaff: e.target.checked })} /><span>Personal sanitario</span></label>
      <label className="checkbox-row"><input type="checkbox" checked={requirements.isolation} onChange={(e) => setRequirements({ ...requirements, isolation: e.target.checked })} /><span>Aislamiento</span></label>
      <label className="checkbox-row"><input type="checkbox" checked={requirements.bariatric} onChange={(e) => setRequirements({ ...requirements, bariatric: e.target.checked })} /><span>Bariátrico</span></label>
      <label className="checkbox-row"><input type="checkbox" checked={requirements.stairsAssistance} onChange={(e) => setRequirements({ ...requirements, stairsAssistance: e.target.checked })} /><span>Ayuda de escaleras</span></label>
    </div></section>
    <section className="form-section"><div className="section-number">✎</div><div className="section-heading"><h2>Notas</h2><p>Las privadas solo las ve la empresa; las del proveedor viajan con el trayecto.</p></div><div className="field-grid">
      <label className="span-2"><span>Notas privadas</span><textarea value={privateNotes} onChange={(e) => setPrivateNotes(e.target.value)} rows={2} /></label>
      <label className="span-2"><span>Notas visibles para el proveedor</span><textarea value={providerNotes} onChange={(e) => setProviderNotes(e.target.value)} rows={2} /></label>
      <label className="checkbox-row span-2"><input type="checkbox" checked={propagate} onChange={(e) => setPropagate(e.target.checked)} /><span>Propagar los cambios de ruta y requisitos a los trayectos ya creados</span></label>
    </div></section>
    <div className="form-actions"><button className="button button-secondary" type="button" onClick={onClose}>Cancelar</button><button className="button button-accent" disabled={saving}>{saving ? 'Guardando…' : 'Guardar cambios'}</button></div>
  </form>
}
