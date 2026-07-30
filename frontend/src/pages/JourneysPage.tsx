import { useEffect, useEffectEvent, useState } from 'react'
import { api } from '../api'
import { JourneyTable } from '../components/JourneyTable'
import { EmptyState, ErrorState, LoadingState, PageHeader } from '../components/States'
import type { Journey, JourneyFilters } from '../types'
import { csvForJourneys, localDate } from '../utils'

const defaultFilters: JourneyFilters = { from: localDate(-1), to: localDate(1), status: 'active', provider: '', contract: '', direction: '', reason: '', originMunicipality: '', destinationMunicipality: '', deliveryState: '', search: '' }

export function JourneysPage() {
  const [filters, setFilters] = useState(defaultFilters)
  const [applied, setApplied] = useState(defaultFilters)
  const [journeys, setJourneys] = useState<Journey[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [refreshedAt, setRefreshedAt] = useState<Date>()

  async function load(silent = false) {
    if (!silent) setLoading(true)
    try {
      const result = await api.listJourneys(applied)
      const sorted = [...result.items].sort((a, b) => {
        const aTime = a.direction === 'Return' ? a.scheduledPickupAt : a.scheduledStartAt
        const bTime = b.direction === 'Return' ? b.scheduledPickupAt : b.scheduledStartAt
        return (aTime ?? '').localeCompare(bTime ?? '')
      })
      setJourneys(sorted); setError(''); setRefreshedAt(new Date())
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'Error desconocido.') }
    finally { setLoading(false) }
  }
  const loadEffect = useEffectEvent(load)

  useEffect(() => {
    void loadEffect()
    const timer = window.setInterval(() => { void loadEffect(true) }, 30_000)
    return () => window.clearInterval(timer)
  }, [applied])

  function change(name: keyof JourneyFilters, value: string) { setFilters((current) => ({ ...current, [name]: value })) }
  function exportCsv() {
    const blob = new Blob([csvForJourneys(journeys)], { type: 'text/csv;charset=utf-8' })
    const link = document.createElement('a'); link.href = URL.createObjectURL(blob); link.download = `trayectos-${applied.from}-${applied.to}.csv`; link.click(); URL.revokeObjectURL(link.href)
  }

  return <div className="page wide-page">
    <PageHeader eyebrow="Mesa de operaciones" title="Seguimiento de trayectos" description="Ventana activa de ayer a mañana. Actualización automática cada 30 segundos." actions={<><span className="refresh-note">{refreshedAt ? `Actualizado ${refreshedAt.toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })}` : 'Sin actualizar'}</span><button className="button button-secondary" onClick={() => void load()}>Actualizar</button><button className="button button-primary" onClick={exportCsv} disabled={!journeys.length}>Exportar CSV</button></>} />
    <form className="filter-panel" onSubmit={(event) => { event.preventDefault(); setApplied(filters) }}>
      <label className="search-field"><span>Buscar</span><input type="search" value={filters.search} onChange={(e) => change('search', e.target.value)} placeholder="Solicitud, trayecto, referencia, paciente, documento o teléfono" /></label>
      <label><span>Desde</span><input type="date" value={filters.from} onChange={(e) => change('from', e.target.value)} /></label>
      <label><span>Hasta</span><input type="date" value={filters.to} onChange={(e) => change('to', e.target.value)} /></label>
      <label><span>Estado</span><select value={filters.status} onChange={(e) => change('status', e.target.value)}><option value="active">Activos</option><option value="">Todos</option><option value="Scheduled">Programado</option><option value="Completed">Completado</option><option value="Cancelled">Cancelado</option></select></label>
      <label><span>Dirección</span><select value={filters.direction} onChange={(e) => change('direction', e.target.value)}><option value="">Todas</option><option value="Outbound">Ida</option><option value="Return">Vuelta</option></select></label>
      <details className="advanced-filters"><summary>Más filtros</summary><div className="filter-grid">
        <label><span>Proveedor</span><input value={filters.provider} onChange={(e) => change('provider', e.target.value)} /></label>
        <label><span>Contrato</span><input value={filters.contract} onChange={(e) => change('contract', e.target.value)} /></label>
        <label><span>Motivo</span><input value={filters.reason} onChange={(e) => change('reason', e.target.value)} /></label>
        <label><span>Municipio origen</span><input value={filters.originMunicipality} onChange={(e) => change('originMunicipality', e.target.value)} /></label>
        <label><span>Municipio destino</span><input value={filters.destinationMunicipality} onChange={(e) => change('destinationMunicipality', e.target.value)} /></label>
        <label><span>Recepción</span><select value={filters.deliveryState} onChange={(e) => change('deliveryState', e.target.value)}><option value="">Todas</option><option value="Pending">Pendiente</option><option value="Retrieved">Recibido</option><option value="Dead">Fallido</option></select></label>
      </div></details>
      <button className="button button-accent" type="submit">Aplicar filtros</button>
    </form>
    <div className="result-bar"><strong>{journeys.length} trayectos</strong><span>Ordenados por hora operativa</span></div>
    {loading ? <LoadingState label="Cargando trayectos" /> : error ? <ErrorState message={error} retry={() => void load()} /> : journeys.length ? <JourneyTable journeys={journeys} /> : <EmptyState title="No hay trayectos en esta ventana" message="Amplía las fechas o cambia los filtros para consultar otros servicios." />}
  </div>
}
