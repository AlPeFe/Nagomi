import { useEffect, useEffectEvent, useState } from 'react'
import { Link, useParams } from '../router'
import { api } from '../api'
import { DeliveryBadge, Indicators, StatusBadge } from '../components/Badges'
import { ErrorState, LoadingState, PageHeader } from '../components/States'
import { JourneyTimeline } from '../components/Timeline'
import { StatusMap } from '../components/StatusMap'
import type { Journey, LocationSnapshot, Vehicle } from '../types'
import { directionLabel, formatDateTime, requirementSummary } from '../utils'

interface Option { code: string; name: string }
interface LocationState extends LocationSnapshot { type: 'PrivateAddress' | 'HealthcareFacility' }

export function JourneyDetailPage() {
  const { journeyId = '' } = useParams()
  const [journey, setJourney] = useState<Journey>()
  const [vehicles, setVehicles] = useState<Vehicle[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [editing, setEditing] = useState(false)
  const [message, setMessage] = useState('')
  const [provinces, setProvinces] = useState<Option[]>([])
  const [originMunicipalities, setOriginMunicipalities] = useState<Option[]>([])
  const [destinationMunicipalities, setDestinationMunicipalities] = useState<Option[]>([])
  const [origin, setOrigin] = useState<LocationState>()
  const [destination, setDestination] = useState<LocationState>()
  const [originProvinceCode, setOriginProvinceCode] = useState('')
  const [destinationProvinceCode, setDestinationProvinceCode] = useState('')

  async function load(silent = false) {
    if (!silent) setLoading(true)
    try {
      const [journeyValue, fleet] = await Promise.allSettled([api.getJourney(journeyId), api.listVehicles()])
      if (journeyValue.status === 'fulfilled') { setJourney(journeyValue.value); setError('') } else { setError(journeyValue.reason instanceof Error ? journeyValue.reason.message : 'Error desconocido.') }
      if (fleet.status === 'fulfilled') setVehicles(fleet.value)
    } catch (e) { setError(e instanceof Error ? e.message : 'Error desconocido.') }
    finally { setLoading(false) }
  }
  const loadEffect = useEffectEvent(load)
  useEffect(() => { void loadEffect(); const timer = window.setInterval(() => void loadEffect(true), 30_000); return () => window.clearInterval(timer) }, [journeyId])
  useEffect(() => { api.listProvinces().then(setProvinces).catch(() => setProvinces([])) }, [])
  useEffect(() => {
    if (!journey) return
    setOrigin({ ...journey.origin, type: journey.origin.type ?? 'HealthcareFacility' })
    setDestination({ ...journey.destination, type: journey.destination.type ?? 'HealthcareFacility' })
    setOriginProvinceCode(journey.origin.provinceCode ?? '')
    setDestinationProvinceCode(journey.destination.provinceCode ?? '')
  }, [journey])
  useEffect(() => {
    if (!originProvinceCode) { setOriginMunicipalities([]); return }
    api.listMunicipalities(originProvinceCode).then(setOriginMunicipalities).catch(() => setOriginMunicipalities([]))
  }, [originProvinceCode])
  useEffect(() => {
    if (!destinationProvinceCode) { setDestinationMunicipalities([]); return }
    api.listMunicipalities(destinationProvinceCode).then(setDestinationMunicipalities).catch(() => setDestinationMunicipalities([]))
  }, [destinationProvinceCode])

  async function update(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!journey || !origin || !destination) return
    try {
      const data = Object.fromEntries(new FormData(event.currentTarget))
      const provinceName = (code: string) => provinces.find((p) => p.code === code)?.name ?? ''
      const municipalityName = (code: string, list: Option[]) => list.find((m) => m.code === code)?.name ?? ''
      const originLoc: LocationSnapshot = { ...origin, name: String(data.originName || origin.name), address: String(data.originAddress || origin.address || ''), municipality: municipalityName(originProvinceCode, originMunicipalities) || String(data.originMunicipality || origin.municipality || ''), municipalityCode: originProvinceCode ? String(data.originMunicipalitySelect || origin.municipalityCode || '') : undefined, province: provinceName(originProvinceCode) || origin.province, provinceCode: originProvinceCode || undefined, phone: String(data.originPhone || origin.phone || ''), observations: String(data.originObservations || origin.observations || ''), type: origin.type }
      const destinationLoc: LocationSnapshot = { ...destination, name: String(data.destinationName || destination.name), address: String(data.destinationAddress || destination.address || ''), municipality: municipalityName(destinationProvinceCode, destinationMunicipalities) || String(data.destinationMunicipality || destination.municipality || ''), municipalityCode: destinationProvinceCode ? String(data.destinationMunicipalitySelect || destination.municipalityCode || '') : undefined, province: provinceName(destinationProvinceCode) || destination.province, provinceCode: destinationProvinceCode || undefined, phone: String(data.destinationPhone || destination.phone || ''), observations: String(data.destinationObservations || destination.observations || ''), type: destination.type }
      setJourney(await api.updateJourney(journey.id, { ...journey, scheduledStartAt: String(data.scheduledStartAt), scheduledPickupAt: String(data.scheduledPickupAt) || undefined, providerReference: String(data.providerReference), notes: String(data.notes), origin: originLoc, destination: destinationLoc }))
      setEditing(false); setMessage('Cambios guardados.')
    } catch (e) { setMessage(e instanceof Error ? e.message : 'No se pudo guardar.') }
  }
  async function cancel() { if (!journey || !window.confirm('¿Cancelar solo este trayecto? Sus trayectos hermanos no cambiarán.')) return; try { await api.cancelJourney(journey.id); await load(); setMessage('Trayecto cancelado.') } catch (e) { setMessage(e instanceof Error ? e.message : 'No se pudo cancelar.') } }
  async function reset() { if (!journey || !window.confirm('¿Resetear los estados de este trayecto? Volverá a Programado, se limpia el historial de estados y podrás reasignar vehículo, hora y ruta.')) return; try { await api.resetJourney(journey.id); await load(); setMessage('Estados restablecidos. El trayecto vuelve a Programado.') } catch (e) { setMessage(e instanceof Error ? e.message : 'No se pudo resetear.') } }
  async function assignVehicle(vehicleId: string) { if (!journey) return; try { await api.assignJourneyVehicle(journey.id, vehicleId || undefined); await load(true); setMessage(vehicleId ? 'Vehículo asignado.' : 'Vehículo liberado.') } catch (e) { setMessage(e instanceof Error ? e.message : 'No se pudo asignar.') } }
  async function assignDriver(driverName: string) { if (!journey) return; try { await api.assignJourneyDriver(journey.id, driverName.trim() || undefined); await load(true); setMessage(driverName.trim() ? 'Conductor asignado.' : 'Conductor liberado.') } catch (e) { setMessage(e instanceof Error ? e.message : 'No se pudo asignar.') } }

  if (loading) return <div className="page"><LoadingState label="Cargando trayecto" /></div>
  if (error || !journey) return <div className="page"><ErrorState message={error || 'El trayecto no existe.'} retry={() => void load()} /></div>
  const mutable = journey.status !== 'Completed' && journey.status !== 'Cancelled'
  return <div className="page detail-page">
    <div className="breadcrumbs"><Link to="/trayectos">Trayectos</Link><span>/</span><span>{journey.publicId}</span></div>
    <PageHeader eyebrow={`${directionLabel(journey.direction)} · ${journey.contract || 'Sin contrato'}`} title={journey.publicId} description={`${journey.origin.name} → ${journey.destination.name}`} actions={<><StatusBadge status={journey.status} />{mutable && <button className="button button-secondary" onClick={() => setEditing(!editing)}>{editing ? 'Cerrar edición' : 'Editar trayecto'}</button>}{mutable && <button className="button button-secondary" onClick={() => void reset()}>Resetear estados</button>}{mutable && <button className="button button-danger" onClick={() => void cancel()}>Cancelar trayecto</button>}</>} />
    {message && <div className="inline-message" role="status">{message}</div>}
    <Indicators external={journey.externallyModified} cancelledBy={journey.cancelledBy} delivery={journey.deliveryState} />
    <div className="detail-layout"><aside className="timeline-card"><h2>Progreso operativo</h2><JourneyTimeline journey={journey} /></aside><div className="detail-content">
      <section className="detail-card"><div className="card-heading"><h2>Datos del servicio</h2><Link to={`/solicitudes/${journey.requestId}`}>Ver solicitud {journey.requestPublicId} →</Link></div>{editing ? <form className="field-grid" onSubmit={(e) => void update(e)}><label><span>Inicio previsto</span><input name="scheduledStartAt" type="datetime-local" defaultValue={journey.scheduledStartAt?.slice(0, 16)} /></label><label><span>Recogida prevista</span><input name="scheduledPickupAt" type="datetime-local" defaultValue={journey.scheduledPickupAt?.slice(0, 16)} /></label><label><span>Referencia proveedor</span><input name="providerReference" defaultValue={journey.providerReference} /></label><label><span>Conductor</span><input name="driverName" defaultValue={journey.driverName} onBlur={(e) => void assignDriver(e.target.value)} placeholder="Conductor asignado" /></label><label><span>Vehículo</span><select defaultValue={journey.vehicleId ?? ''} onChange={(e) => void assignVehicle(e.target.value)}><option value="">Sin asignar</option>{vehicles.map((vehicle) => <option key={vehicle.id} value={vehicle.id}>{vehicle.name} ({vehicle.publicId})</option>)}</select></label><label><span>Observaciones origen</span><input name="originObservations" defaultValue={journey.origin.observations} /></label><label><span>Observaciones destino</span><input name="destinationObservations" defaultValue={journey.destination.observations} /></label><label className="span-2"><span>Notas operativas</span><textarea name="notes" defaultValue={journey.notes} /></label><button className="button button-accent">Guardar instantánea</button></form> : <dl className="data-grid"><div><dt>Paciente</dt><dd>{journey.patientName || 'Sin identificar'}</dd></div><div><dt>Teléfono</dt><dd>{journey.patientPhone || 'Sin teléfono'}</dd></div><div><dt>Hora operativa</dt><dd>{journey.pickupTimePending ? 'Hora pendiente' : formatDateTime(journey.direction === 'Return' ? journey.scheduledPickupAt : journey.scheduledStartAt)}</dd></div><div><dt>Cita</dt><dd>{formatDateTime(journey.appointmentAt)}</dd></div><div><dt>Motivo</dt><dd>{journey.reason}</dd></div><div><dt>Requisitos</dt><dd>{requirementSummary(journey.requirements)}</dd></div><div><dt>Proveedor</dt><dd>{journey.provider || 'Sin asignar'}</dd></div><div><dt>Vehículo</dt><dd>{journey.vehicleName || 'Sin asignar'}</dd></div><div><dt>Conductor</dt><dd>{journey.driverName || 'Sin asignar'}</dd></div><div><dt>Referencia externa</dt><dd>{journey.providerReference || 'Sin referencia'}</dd></div></dl>}</section>
      <section className="detail-card"><h2>Ruta</h2>{editing ? <div className="field-grid"><label><span>Origen · Nombre</span><input name="originName" defaultValue={origin?.name} /></label><label><span>Origen · Provincia</span><select value={originProvinceCode} onChange={(e) => setOriginProvinceCode(e.target.value)}><option value="">Selecciona provincia</option>{provinces.map((p) => <option key={p.code} value={p.code}>{p.name}</option>)}</select></label><label><span>Origen · Población</span><select name="originMunicipalitySelect" value={origin?.municipalityCode ?? ''} onChange={(e) => { setOrigin((prev) => prev ? { ...prev, municipalityCode: e.target.value, municipality: originMunicipalities.find((m) => m.code === e.target.value)?.name ?? '' } : prev) }}><option value="">{originProvinceCode ? 'Selecciona población' : 'Primero elige provincia'}</option>{originMunicipalities.map((m) => <option key={m.code} value={m.code}>{m.name}</option>)}</select></label><label><span>Origen · Dirección</span><input name="originAddress" defaultValue={origin?.address} /></label><label><span>Destino · Nombre</span><input name="destinationName" defaultValue={destination?.name} /></label><label><span>Destino · Provincia</span><select value={destinationProvinceCode} onChange={(e) => setDestinationProvinceCode(e.target.value)}><option value="">Selecciona provincia</option>{provinces.map((p) => <option key={p.code} value={p.code}>{p.name}</option>)}</select></label><label><span>Destino · Población</span><select name="destinationMunicipalitySelect" value={destination?.municipalityCode ?? ''} onChange={(e) => { setDestination((prev) => prev ? { ...prev, municipalityCode: e.target.value, municipality: destinationMunicipalities.find((m) => m.code === e.target.value)?.name ?? '' } : prev) }}><option value="">{destinationProvinceCode ? 'Selecciona población' : 'Primero elige provincia'}</option>{destinationMunicipalities.map((m) => <option key={m.code} value={m.code}>{m.name}</option>)}</select></label><label><span>Destino · Dirección</span><input name="destinationAddress" defaultValue={destination?.address} /></label></div> : <div className="route-detail"><article><span>Origen</span><h3>{journey.origin.name}</h3><p>{journey.origin.address}</p><small>{[journey.origin.municipality, journey.origin.province].filter(Boolean).join(' · ')}{journey.origin.observations ? ` — ${journey.origin.observations}` : ''}</small></article><span aria-hidden="true">→</span><article><span>Destino</span><h3>{journey.destination.name}</h3><p>{journey.destination.address}</p><small>{[journey.destination.municipality, journey.destination.province].filter(Boolean).join(' · ')}{journey.destination.observations ? ` — ${journey.destination.observations}` : ''}</small></article></div>}</section>
      <section className="detail-card"><h2>Integración</h2><div className="integration-line"><DeliveryBadge state={journey.deliveryState} /><span>Actualización automática cada 30 segundos</span></div></section>
      <section className="detail-card"><h2>Historial de estados</h2><div className="history-list">{journey.statusEvents?.length ? journey.statusEvents.map((event) => <article key={event.id}><StatusBadge status={event.status} /><div><strong>{formatDateTime(event.occurredAt)}</strong><span>{event.actor || 'Sistema'} · {event.source || 'Nagomi'}{event.externalResourceCode ? ` · ${event.externalResourceCode}` : ''}</span></div></article>) : <p>Sin eventos adicionales.</p>}</div></section>
      <section className="detail-card"><h2>Posiciones registradas</h2><StatusMap points={journey.statusEvents ?? []} /></section>
      <section className="detail-card"><h2>Auditoría de cambios</h2><div className="history-list">{journey.audit?.length ? journey.audit.map((entry) => <article key={entry.id}><strong>{entry.action}</strong><div><span>{entry.actor} · {entry.source}</span><small>{formatDateTime(entry.occurredAt)}{entry.changes?.length ? ` · ${entry.changes.join(', ')}` : ''}</small></div></article>) : <p>Sin cambios registrados.</p>}</div></section>
    </div></div>
  </div>
}
