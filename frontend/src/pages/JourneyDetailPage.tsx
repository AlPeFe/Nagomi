import { useEffect, useEffectEvent, useState } from 'react'
import { Link, useParams } from '../router'
import { api } from '../api'
import { DeliveryBadge, Indicators, StatusBadge } from '../components/Badges'
import { ErrorState, LoadingState, PageHeader } from '../components/States'
import { JourneyTimeline } from '../components/Timeline'
import type { Journey } from '../types'
import { directionLabel, formatDateTime, requirementSummary } from '../utils'

export function JourneyDetailPage() {
  const { journeyId = '' } = useParams()
  const [journey, setJourney] = useState<Journey>()
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [editing, setEditing] = useState(false)
  const [message, setMessage] = useState('')
  async function load(silent = false) { if (!silent) setLoading(true); try { setJourney(await api.getJourney(journeyId)); setError('') } catch (e) { setError(e instanceof Error ? e.message : 'Error desconocido.') } finally { setLoading(false) } }
  const loadEffect = useEffectEvent(load)
  useEffect(() => { void loadEffect(); const timer = window.setInterval(() => void loadEffect(true), 30_000); return () => window.clearInterval(timer) }, [journeyId])
  async function update(event: React.FormEvent<HTMLFormElement>) { event.preventDefault(); if (!journey) return; try { const data = Object.fromEntries(new FormData(event.currentTarget)); setJourney(await api.updateJourney(journey.id, { ...journey, scheduledStartAt: String(data.scheduledStartAt), scheduledPickupAt: String(data.scheduledPickupAt) || undefined, providerReference: String(data.providerReference), notes: String(data.notes), origin: { ...journey.origin, observations: String(data.originObservations) }, destination: { ...journey.destination, observations: String(data.destinationObservations) } })); setEditing(false); setMessage('Cambios guardados.') } catch (e) { setMessage(e instanceof Error ? e.message : 'No se pudo guardar.') } }
  async function cancel() { if (!journey || !window.confirm('¿Cancelar solo este trayecto? Sus trayectos hermanos no cambiarán.')) return; try { await api.cancelJourney(journey.id); await load(); setMessage('Trayecto cancelado.') } catch (e) { setMessage(e instanceof Error ? e.message : 'No se pudo cancelar.') } }
  if (loading) return <div className="page"><LoadingState label="Cargando trayecto" /></div>
  if (error || !journey) return <div className="page"><ErrorState message={error || 'El trayecto no existe.'} retry={() => void load()} /></div>
  return <div className="page detail-page">
    <div className="breadcrumbs"><Link to="/trayectos">Trayectos</Link><span>/</span><span>{journey.publicId}</span></div>
    <PageHeader eyebrow={`${directionLabel(journey.direction)} · ${journey.contract || 'Sin contrato'}`} title={journey.publicId} description={`${journey.origin.name} → ${journey.destination.name}`} actions={<><StatusBadge status={journey.status} /><button className="button button-secondary" onClick={() => setEditing(!editing)}>{editing ? 'Cerrar edición' : 'Editar trayecto'}</button>{journey.status !== 'Completed' && journey.status !== 'Cancelled' && <button className="button button-danger" onClick={() => void cancel()}>Cancelar trayecto</button>}</>} />
    {message && <div className="inline-message" role="status">{message}</div>}
    <Indicators external={journey.externallyModified} cancelledBy={journey.cancelledBy} delivery={journey.deliveryState} />
    <div className="detail-layout"><aside className="timeline-card"><h2>Progreso operativo</h2><JourneyTimeline journey={journey} /></aside><div className="detail-content">
      <section className="detail-card"><div className="card-heading"><h2>Datos del servicio</h2><Link to={`/solicitudes/${journey.requestId}`}>Ver solicitud {journey.requestPublicId} →</Link></div>{editing ? <form className="field-grid" onSubmit={(e) => void update(e)}><label><span>Inicio previsto</span><input name="scheduledStartAt" type="datetime-local" defaultValue={journey.scheduledStartAt?.slice(0, 16)} /></label><label><span>Recogida prevista</span><input name="scheduledPickupAt" type="datetime-local" defaultValue={journey.scheduledPickupAt?.slice(0, 16)} /></label><label><span>Referencia proveedor</span><input name="providerReference" defaultValue={journey.providerReference} /></label><label><span>Observaciones origen</span><input name="originObservations" defaultValue={journey.origin.observations} /></label><label><span>Observaciones destino</span><input name="destinationObservations" defaultValue={journey.destination.observations} /></label><label className="span-2"><span>Notas operativas</span><textarea name="notes" defaultValue={journey.notes} /></label><button className="button button-accent">Guardar instantánea</button></form> : <dl className="data-grid"><div><dt>Paciente</dt><dd>{journey.patientName || 'Sin identificar'}</dd></div><div><dt>Teléfono</dt><dd>{journey.patientPhone || 'Sin teléfono'}</dd></div><div><dt>Hora operativa</dt><dd>{journey.pickupTimePending ? 'Hora pendiente' : formatDateTime(journey.direction === 'Return' ? journey.scheduledPickupAt : journey.scheduledStartAt)}</dd></div><div><dt>Cita</dt><dd>{formatDateTime(journey.appointmentAt)}</dd></div><div><dt>Motivo</dt><dd>{journey.reason}</dd></div><div><dt>Requisitos</dt><dd>{requirementSummary(journey.requirements)}</dd></div><div><dt>Proveedor</dt><dd>{journey.provider || 'Sin asignar'}</dd></div><div><dt>Referencia externa</dt><dd>{journey.providerReference || 'Sin referencia'}</dd></div></dl>}</section>
      <section className="detail-card"><h2>Ruta</h2><div className="route-detail"><article><span>Origen</span><h3>{journey.origin.name}</h3><p>{journey.origin.address}</p><small>{journey.origin.observations}</small></article><span aria-hidden="true">→</span><article><span>Destino</span><h3>{journey.destination.name}</h3><p>{journey.destination.address}</p><small>{journey.destination.observations}</small></article></div></section>
      <section className="detail-card"><h2>Integración</h2><div className="integration-line"><DeliveryBadge state={journey.deliveryState} /><span>Actualización automática cada 30 segundos</span></div></section>
      <section className="detail-card"><h2>Historial de estados</h2><div className="history-list">{journey.statusEvents?.length ? journey.statusEvents.map((event) => <article key={event.id}><StatusBadge status={event.status} /><div><strong>{formatDateTime(event.occurredAt)}</strong><span>{event.actor || 'Sistema'} · {event.source || 'Nagomi'}</span></div></article>) : <p>Sin eventos adicionales.</p>}</div></section>
      <section className="detail-card"><h2>Auditoría de cambios</h2><div className="history-list">{journey.audit?.length ? journey.audit.map((entry) => <article key={entry.id}><strong>{entry.action}</strong><div><span>{entry.actor} · {entry.source}</span><small>{formatDateTime(entry.occurredAt)}{entry.changes?.length ? ` · ${entry.changes.join(', ')}` : ''}</small></div></article>) : <p>Sin cambios registrados.</p>}</div></section>
    </div></div>
  </div>
}
