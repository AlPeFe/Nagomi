import { useEffect, useState } from 'react'
import { api } from '../api'
import { JourneyTable } from '../components/JourneyTable'
import { JourneyQuickView } from '../components/JourneyQuickView'
import { JourneyMapModal } from '../components/JourneyMapModal'
import { JourneyAssignmentPanel } from '../components/JourneyAssignmentPanel'
import { EmptyState, ErrorState, LoadingState, PageHeader } from '../components/States'
import type { Journey, JourneyFilters, Vehicle } from '../types'
import { csvForJourneys, localDate } from '../utils'

/**
 * Histórico: consulta del dataset COMPLETO de traslados (incluye completados y
 * cancelados, sin límite de fecha). Arranca vacío a propósito: no lanza ninguna
 * consulta hasta que se aplican filtros, para no barrer toda la tabla al entrar.
 */
type HistoryFilters = JourneyFilters & { anyDate: boolean }

const emptyFilters: JourneyFilters = {
  from: '', to: '', status: '', provider: '', contract: '', direction: '', reason: '',
  originMunicipality: '', destinationMunicipality: '', deliveryState: '', search: '',
}

// Ventana amplia cuando no se indican fechas: "todo el histórico".
const WIDE_FROM = '2000-01-01'

export function HistoryPage() {
  const [filters, setFilters] = useState<HistoryFilters>({ ...emptyFilters, anyDate: true })
  const [applied, setApplied] = useState<HistoryFilters | null>(null)
  const [journeys, setJourneys] = useState<Journey[]>([])
  const [vehicles, setVehicles] = useState<Vehicle[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [quickId, setQuickId] = useState<string | null>(null)
  const [mapId, setMapId] = useState<string | null>(null)
  const [assignId, setAssignId] = useState<string | null>(null)

  useEffect(() => {
    // La lista de vehículos solo alimenta el selector de asignación: si falla, la
    // pantalla sigue siendo útil.
    void api.listVehicles().then(setVehicles).catch(() => setVehicles([]))
  }, [])

  async function search(next: HistoryFilters) {
    setLoading(true)
    setError('')
    try {
      const result = await api.listJourneys({
        ...next,
        from: next.anyDate ? WIDE_FROM : next.from,
        to: next.anyDate ? localDate(365) : next.to,
      })
      const sorted = [...result.items].sort((a, b) => {
        const aTime = a.direction === 'Return' ? a.scheduledPickupAt : a.scheduledStartAt
        const bTime = b.direction === 'Return' ? b.scheduledPickupAt : b.scheduledStartAt
        return (bTime ?? '').localeCompare(aTime ?? '')
      })
      setJourneys(sorted)
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'Error desconocido.')
      setJourneys([])
    } finally {
      setLoading(false)
    }
  }
  function change(name: keyof HistoryFilters, value: string | boolean) {
    setFilters((current) => ({ ...current, [name]: value }))
  }

  function reset() {
    setFilters({ ...emptyFilters, anyDate: true })
    setApplied(null)
    setJourneys([])
    setError('')
  }



  function exportCsv() {
    const blob = new Blob([csvForJourneys(journeys)], { type: 'text/csv;charset=utf-8' })
    const link = document.createElement('a')
    link.href = URL.createObjectURL(blob)
    link.download = `historico-traslados-${localDate(0)}.csv`
    link.click()
    URL.revokeObjectURL(link.href)
  }

  return <div className="page wide-page">
    <PageHeader
      eyebrow="Consulta"
      title="Histórico de traslados"
      description="Todos los traslados, incluidos completados y cancelados. Afina con los filtros y lanza la búsqueda: la pantalla no consulta nada hasta que la ejecutas."
      actions={journeys.length ? <button className="button button-secondary" onClick={exportCsv}>Exportar CSV</button> : undefined}
    />
    <form className="filter-panel history-filters" onSubmit={(event) => { event.preventDefault(); setApplied(filters); void search(filters) }}>
      <label className="search-field"><span>Buscar</span><input type="search" value={filters.search} onChange={(e) => change('search', e.target.value)} placeholder="Solicitud, trayecto, referencia, paciente, documento o teléfono" /></label>
      <label><span>Estado</span><select value={filters.status} onChange={(e) => change('status', e.target.value)}>
        <option value="">Todos</option><option value="Scheduled">Programado</option><option value="Activated">Activado</option>
        <option value="EnRouteToOrigin">Hacia origen</option><option value="ArrivedAtOrigin">En origen</option>
        <option value="PatientOnBoard">Paciente recogido</option><option value="EnRouteToDestination">En traslado</option>
        <option value="ArrivedAtDestination">En destino</option><option value="Completed">Completado</option><option value="Cancelled">Cancelado</option>
      </select></label>
      <label><span>Dirección</span><select value={filters.direction} onChange={(e) => change('direction', e.target.value)}><option value="">Todas</option><option value="Outbound">Ida</option><option value="Return">Vuelta</option></select></label>
      <label><span>Desde</span><input type="date" value={filters.from} disabled={filters.anyDate} onChange={(e) => change('from', e.target.value)} /></label>
      <label><span>Hasta</span><input type="date" value={filters.to} disabled={filters.anyDate} onChange={(e) => change('to', e.target.value)} /></label>
      <div className="filter-actions">
        <button className="button button-primary" type="submit" disabled={loading}>{loading ? 'Buscando…' : 'Buscar'}</button>
        <button className="button button-secondary" type="button" onClick={reset}>Limpiar</button>
      </div>
      <label className="check-line any-date"><input type="checkbox" checked={filters.anyDate} onChange={(e) => change('anyDate', e.target.checked)} /><span>Cualquier fecha</span></label>
      <details className="advanced-filters" open>
        <summary>Filtros avanzados</summary>
        <div className="filter-grid">
          <label><span>Proveedor</span><input value={filters.provider} onChange={(e) => change('provider', e.target.value)} /></label>
          <label><span>Contrato</span><input value={filters.contract} onChange={(e) => change('contract', e.target.value)} /></label>
          <label><span>Motivo</span><input value={filters.reason} onChange={(e) => change('reason', e.target.value)} /></label>
          <label><span>Municipio origen</span><input value={filters.originMunicipality} onChange={(e) => change('originMunicipality', e.target.value)} /></label>
          <label><span>Municipio destino</span><input value={filters.destinationMunicipality} onChange={(e) => change('destinationMunicipality', e.target.value)} /></label>
          <label><span>Recepción</span><select value={filters.deliveryState} onChange={(e) => change('deliveryState', e.target.value)}><option value="">Todas</option><option value="Pending">Pendiente</option><option value="Published">Enviado</option><option value="Retrieved">Recibido</option><option value="Dead">Fallido</option><option value="NotPublished">Sin publicar</option></select></label>
        </div>
      </details>
    </form>

    {error && <ErrorState message={error} retry={applied ? () => void search(applied) : undefined} />}
    {loading && !journeys.length && <LoadingState label="Buscando traslados" />}
    {!applied && !loading && !journeys.length && !error && (
      <EmptyState
        title="Aún no hay resultados"
        message="Aplica los filtros que necesites y pulsa Buscar. Los traslados completados y cancelados también aparecen aquí."
      />
    )}
    {applied && !loading && !journeys.length && !error && (
      <EmptyState title="Sin resultados" message="Ningún traslado cumple esos filtros." />
    )}
    {journeys.length > 0 && <>
      <div className="result-bar"><strong>{journeys.length}</strong><span>traslados</span><span>· ordenados del más reciente al más antiguo</span></div>
      <JourneyTable
        journeys={journeys}
        onOpen={setQuickId}
        onShowMap={setMapId}
        onAssign={setAssignId}
      />
    </>}
    {quickId && journeys.some((journey) => journey.id === quickId) && <JourneyQuickView
      journeys={journeys}
      index={journeys.findIndex((journey) => journey.id === quickId)}
      onClose={() => setQuickId(null)}
      onNavigate={(nextIndex) => setQuickId(journeys[nextIndex]?.id ?? null)}
      onManageVehicle={setAssignId}
      onChanged={() => applied ? void search(applied) : undefined}
    />}    {assignId && journeys.some((journey) => journey.id === assignId) && <JourneyAssignmentPanel
      journey={journeys.find((journey) => journey.id === assignId)!}
      vehicles={vehicles}
      onClose={() => setAssignId(null)}
      onChanged={() => (applied ? void search(applied) : undefined)}
    />}
    {mapId && journeys.some((journey) => journey.id === mapId) && <JourneyMapModal
      journey={journeys.find((journey) => journey.id === mapId)!}
      onClose={() => setMapId(null)}
    />}
  </div>
}
