import { useEffect, useEffectEvent, useState } from 'react'
import { api } from '../api'
import { EmptyState, ErrorState, LoadingState, PageHeader } from '../components/States'
import type { Vehicle } from '../types'

export function VehiclesPage() {
  const [vehicles, setVehicles] = useState<Vehicle[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [name, setName] = useState('')
  const [externalCode, setExternalCode] = useState('')

  async function load(silent = false) {
    if (!silent) setLoading(true)
    try { setVehicles(await api.listVehicles()); setError('') }
    catch (caught) { setError(caught instanceof Error ? caught.message : 'Error desconocido.') }
    finally { setLoading(false) }
  }
  const loadEffect = useEffectEvent(load)
  useEffect(() => { void loadEffect() }, [])

  async function create(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!name.trim()) return
    try {
      await api.createVehicle({ name: name.trim(), externalCode: externalCode.trim() || undefined })
      setName(''); setExternalCode(''); setMessage('Vehículo creado.'); await load(true)
    } catch (caught) { setMessage(caught instanceof Error ? caught.message : 'No se pudo crear.') }
  }

  async function remove(id: string) {
    if (!window.confirm('¿Eliminar este vehículo? No se borrará de trayectos históricos, solo dejará de estar disponible.')) return
    try { await api.deleteVehicle(id); setMessage('Vehículo eliminado.'); await load(true) }
    catch (caught) { setMessage(caught instanceof Error ? caught.message : 'No se pudo eliminar.') }
  }

  if (loading) return <div className="page"><LoadingState label="Cargando vehículos" /></div>
  if (error && vehicles.length === 0) return <div className="page"><ErrorState message={error} retry={() => void load()} /></div>

  return <div className="page">
    <PageHeader eyebrow="Flota" title="Vehículos" description="Gestiona los vehículos con los que tu organización ejecuta traslados." />
    {message && <div className="inline-message" role="status">{message}</div>}
    <form className="inline-form" onSubmit={(event) => void create(event)}>
      <label><span>Nombre</span><input value={name} onChange={(e) => setName(e.target.value)} placeholder="Ambulancia 01" required /></label>
      <label><span>Código externo</span><input value={externalCode} onChange={(e) => setExternalCode(e.target.value)} placeholder="AMB-01" /></label>
      <button className="button button-accent" type="submit">Añadir vehículo</button>
    </form>
    {vehicles.length ? <div className="card-list">{vehicles.map((vehicle) => (
      <article key={vehicle.id} className="list-row">
        <div><strong>{vehicle.name}</strong><span>{vehicle.publicId}{vehicle.externalCode ? ` · ${vehicle.externalCode}` : ''}</span></div>
        <button className="button button-danger" onClick={() => void remove(vehicle.id)}>Eliminar</button>
      </article>
    ))}</div> : <EmptyState title="Sin vehículos" message="Añade tu primer vehículo para poder asignarlo a los traslados." />}
  </div>
}
