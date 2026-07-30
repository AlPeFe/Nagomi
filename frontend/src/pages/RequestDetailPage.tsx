import { useEffect, useEffectEvent, useState } from 'react'
import { Link, useParams } from '../router'
import { api } from '../api'
import { DeliveryBadge, StatusBadge } from '../components/Badges'
import { EmptyState, ErrorState, LoadingState, PageHeader } from '../components/States'
import type { TransportRequest } from '../types'
import { directionLabel, formatDateTime } from '../utils'

export function RequestDetailPage() {
  const { requestId = '' } = useParams()
  const [request, setRequest] = useState<TransportRequest>()
  const [statusFilter, setStatusFilter] = useState('active')
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [preview, setPreview] = useState<{ additions: number; cancellations: number; exceptions: number }>()
  async function load() { try { setRequest(await api.getRequest(requestId)); setError('') } catch (e) { setError(e instanceof Error ? e.message : 'Error desconocido.') } }
  const loadEffect = useEffectEvent(load)
  useEffect(() => { void loadEffect() }, [requestId])
  async function cancel() { if (!request || !window.confirm('Se cancelarán todos los trayectos no completados. ¿Continuar?')) return; try { await api.cancelRequest(request.id); await load() } catch (e) { setMessage(e instanceof Error ? e.message : 'No se pudo cancelar.') } }
  async function previewRecurrence() { if (!request) return; try { setPreview(await api.previewRecurrence(request.id, { recurrence: request.recurring })) } catch (e) { setMessage(e instanceof Error ? e.message : 'No se pudo calcular el impacto.') } }
  async function applyRecurrence(overwriteExceptions: boolean) { if (!request?.recurring) return; try { await api.applyRecurrence(request.id, request.recurring, overwriteExceptions); setPreview(undefined); setMessage('Recurrencia aplicada.'); await load() } catch (e) { setMessage(e instanceof Error ? e.message : 'No se pudo aplicar.') } }
  if (error) return <div className="page"><ErrorState message={error} retry={() => void load()} /></div>
  if (!request) return <div className="page"><LoadingState label="Cargando solicitud" /></div>
  const journeys = (request.journeys ?? []).filter((journey) => statusFilter === 'all' || !['Completed', 'Cancelled'].includes(journey.status))
  return <div className="page detail-page">
    <div className="breadcrumbs"><Link to="/solicitudes">Solicitudes</Link><span>/</span><span>{request.publicId ?? 'Borrador'}</span></div>
    <PageHeader eyebrow={request.status === 'Draft' ? 'Borrador' : 'Solicitud activa'} title={request.publicId ?? 'Solicitud sin enviar'} description={`${request.patientName || 'Paciente sin identificar'} · ${request.reason || 'Motivo pendiente'}`} actions={<>{request.status !== 'Draft' && request.status !== 'Cancelled' && <button className="button button-danger" onClick={() => void cancel()}>Cancelar solicitud</button>}</>} />
    {message && <div className="inline-message" role="status">{message}</div>}
    <div className="request-summary"><div><span>Ruta base</span><strong>{request.origin?.name || 'Pendiente'} → {request.destination?.name || 'Pendiente'}</strong></div><div><span>Contrato / proveedor</span><strong>{request.contract || 'Sin contrato'} · {request.provider || 'Sin proveedor'}</strong></div><div><span>Programación</span><strong>{request.recurring ? 'Recurrente' : 'Puntual'}</strong></div><div><span>Actualización</span><strong>{formatDateTime(request.updatedAt)}</strong></div></div>
    {request.recurring && <section className="detail-card recurrence-actions"><div><h2>Patrón de recurrencia</h2><p>Previsualiza altas, cancelaciones y excepciones antes de propagar cualquier cambio.</p></div><button className="button button-secondary" onClick={() => void previewRecurrence()}>Previsualizar impacto</button>{preview && <div className="impact-box" role="dialog" aria-label="Impacto de recurrencia"><strong>Impacto calculado</strong><span>+{preview.additions} altas</span><span>−{preview.cancellations} cancelaciones</span><span>{preview.exceptions} excepciones</span><button className="button button-primary" onClick={() => void applyRecurrence(false)}>Conservar excepciones</button><button className="button button-accent" onClick={() => void applyRecurrence(true)}>Sobrescribir excepciones</button></div>}</section>}
    <section className="detail-card"><div className="card-heading"><div><h2>Trayectos</h2><p>Los activos se muestran por defecto.</p></div><label><span className="sr-only">Filtrar trayectos por estado</span><select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}><option value="active">Activos</option><option value="all">Todos</option></select></label></div>{journeys.length ? <div className="compact-journeys">{journeys.map((journey) => <Link to={`/trayectos/${journey.id}`} key={journey.id}><span className="direction-box">{directionLabel(journey.direction)}</span><div><strong>{journey.publicId}</strong><small>{journey.origin.name} → {journey.destination.name}</small></div><StatusBadge status={journey.status} /><span aria-hidden="true">→</span></Link>)}</div> : <EmptyState title="No hay trayectos en este filtro" message="Selecciona todos para consultar trayectos terminales." />}</section>
    <div className="two-columns"><section className="detail-card"><h2>Auditoría</h2><div className="history-list">{request.audit?.length ? request.audit.map((entry) => <article key={entry.id}><strong>{entry.action}</strong><div><span>{entry.actor} · {entry.source}</span><small>{formatDateTime(entry.occurredAt)}</small></div></article>) : <p>Sin cambios registrados.</p>}</div></section><section className="detail-card"><h2>Entregas de integración</h2><div className="history-list">{request.deliveries?.length ? request.deliveries.map((delivery) => <article key={delivery.id}><DeliveryBadge state={delivery.state} /><div><span>{formatDateTime(delivery.createdAt)}</span><small>{delivery.retrievedAt ? `Recibido ${formatDateTime(delivery.retrievedAt)}` : `${delivery.attempts ?? 0} intentos`}</small></div></article>) : <p>No se han generado notificaciones.</p>}</div></section></div>
  </div>
}
