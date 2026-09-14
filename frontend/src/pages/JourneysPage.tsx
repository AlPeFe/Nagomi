import { useEffect, useEffectEvent, useState } from 'react'
import { api } from '../api'
import { JourneyTable } from '../components/JourneyTable'
import { JourneyQuickView } from '../components/JourneyQuickView'
import { EmptyState, ErrorState, LoadingState, PageHeader } from '../components/States'
import type { Journey, JourneyFilters, Vehicle } from '../types'
import { csvForJourneys, localDate } from '../utils'

/**
 * Operación: mesa de trabajo DIARIA. Ventana de ayer a mañana (editable) y solo
 * trabajo vivo. Sin filtro de estado (los completados/cancelados viven en el
 * Histórico) y sin filtro de dirección: ida y vuelta son las dos partes del mismo
 * trabajo y se muestran siempre juntas.
 */
const defaultFilters: JourneyFilters = {
  from: localDate(-1), to: localDate(1), status: 'active',
  provider: '', contract: '', direction: '', reason: '',
  originMunicipality: '', destinationMunicipality: '', deliveryState: '', search: '',
}

export function JourneysPage() {
  const [filters, setFilters] = useState(defaultFilters)
  const [applied, setApplied] = useState(defaultFilters)
  const [journeys, setJourneys] = useState<Journey[]>([])
  const [vehicles, setVehicles] = useState<Vehicle[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [refreshedAt, setRefreshedAt] = useState<Date>()
  const [quickId, setQuickId] = useState<string | null>(null)

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

  useEffect(() => {
    // Alimenta el selector de asignación de vehículo. Si falla, la mesa sigue viva.
    void api.listVehicles().then(setVehicles).catch(() => setVehicles([]))
  }, [])

  function change(name: keyof JourneyFilters, value: string) { setFilters((current) => ({ ...current, [name]: value })) }

  async function assignVehicle(journeyId: string, vehicleId: string) {
    await api.assignJourneyVehicle(journeyId, vehicleId || undefined)
    await load(true)
  }

  async function assignDriver(journeyId: string, driverName: string) {
    await api.assignJourneyDriver(journeyId, driverName.trim() || undefined)
    await load(true)
  }

  function exportCsv() {
    const blob = new Blob([csvForJourneys(journeys)], { type: 'text/csv;charset=utf-8' })
    const link = document.createElement('a'); link.href = URL.createObjectURL(blob); link.download = `trayectos-${applied.from}-${applied.to}.csv`; link.click(); URL.revokeObjectURL(link.href)
  }

  return <div className="page wide-page">
    <PageHeader eyebrow="Mesa de operaciones" title="Trabajo diario" description="Ventana activa de ayer a mañana, editable. Actualización automática cada 30 segundos. Los traslados completados y cancelados están en el Histórico." actions={<><span className="refresh-note">{refreshedAt ? `Actualizado ${refreshedAt.toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })}` : 'Sin actualizar'}</span><button className="button button-secondary" onClick={() => void load()}>Actualizar</button><button className="button button-primary" onClick={exportCsv} disabled={!journeys.length}>Exportar CSV</button></>} />
    <form className="filter-panel" onSubmit={(event) => { event.preventDefault(); setApplied(filters) }}>
      <label className="search-field"><span>Buscar</span><input type="search" value={filters.search} onChange={(e) => change('search', e.target.value)} placeholder="Solicitud, trayecto, referencia, paciente, documento o teléfono" /></label>
      <label><span>Desde</span><input type="date" value={filters.from} onChange={(e) => change('from', e.target.value)} /></label>
      <label><span>Hasta</span><input type="date" value={filters.to} onChange={(e) => change('to', e.target.value)} /></label>
      <div className="filter-actions">
        <button className="button button-accent" type="submit">Aplicar filtros</button>
      </div>
      <details className="advanced-filters"><summary>Más filtros</summary><div className="filter-grid">
        <label><span>Proveedor</span><input value={filters.provider} onChange={(e) => change('provider', e.target.value)} /></label>
        <label><span>Contrato</span><input value={filters.contract} onChange={(e) => change('contract', e.target.value)} /></label>
        <label><span>Motivo</span><input value={filters.reason} onChange={(e) => change('reason', e.target.value)} /></label>
        <label><span>Municipio origen</span><input value={filters.originMunicipality} onChange={(e) => change('originMunicipality', e.target.value)} /></label>
        <label><span>Municipio destino</span><input value={filters.destinationMunicipality} onChange={(e) => change('destinationMunicipality', e.target.value)} /></label>
        <label><span>Recepción</span><select value={filters.deliveryState} onChange={(e) => change('deliveryState', e.target.value)}><option value="">Todas</option><option value="Pending">Pendiente</option><option value="Published">Enviado</option><option value="Retrieved">Recibido</option><option value="Dead">Fallido</option></select></label>
      </div></details>
    </form>

    {loading ? <LoadingState /> : error ? <ErrorState message={error} retry={() => void load()} /> : !journeys.length ? <EmptyState title="Sin trabajo en esta ventana" message="Amplía el rango de fechas o consulta el Histórico para ver traslados anteriores." /> : <>
      <div className="result-bar"><strong>{journeys.length}</strong><span>trayectos</span><span>· ordenados por hora operativa</span></div>
      <JourneyTable
        journeys={journeys}
        vehicles={vehicles}
        onOpen={setQuickId}
        onAssignVehicle={(id, vehicleId) => void assignVehicle(id, vehicleId)}
        onAssignDriver={(id, name) => void assignDriver(id, name)}
      />
    </>}
    {quickId && journeys.some((journey) => journey.id === quickId) && <JourneyQuickView
      journeys={journeys}
      index={journeys.findIndex((journey) => journey.id === quickId)}
      onClose={() => setQuickId(null)}
      onNavigate={(nextIndex) => setQuickId(journeys[nextIndex]?.id ?? null)}
      vehicles={vehicles}
      onAssignVehicle={(id, vehicleId) => void assignVehicle(id, vehicleId)}
      onAssignDriver={(id, name) => void assignDriver(id, name)}
      onChanged={() => void load(true)}
    />}
  </div>
}
