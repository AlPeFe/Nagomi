import { useEffect, useEffectEvent, useState } from 'react'
import { api } from '../api'
import { EmptyState, ErrorState, LoadingState, PageHeader } from '../components/States'
import { VEHICLE_TYPE_LABELS, type Vehicle, type VehicleType } from '../types'

interface VehicleForm { name: string; code: string; externalCode: string; vehicleType: VehicleType }

const emptyForm: VehicleForm = { name: '', code: '', externalCode: '', vehicleType: 'Conventional' }

export function VehiclesPage() {
  const [vehicles, setVehicles] = useState<Vehicle[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [form, setForm] = useState<VehicleForm>(emptyForm)
  const [editing, setEditing] = useState<Vehicle | null>(null)

  async function load(silent = false) {
    if (!silent) setLoading(true)
    try { setVehicles(await api.listVehicles()); setError('') }
    catch (caught) { setError(caught instanceof Error ? caught.message : 'Error desconocido.') }
    finally { setLoading(false) }
  }
  const loadEffect = useEffectEvent(load)
  useEffect(() => { void loadEffect() }, [])

  function reset() { setForm(emptyForm); setEditing(null) }
  function startCreate() { reset(); setMessage('') }
  function startEdit(vehicle: Vehicle) {
    setEditing(vehicle)
    setForm({ name: vehicle.name, code: vehicle.publicId, externalCode: vehicle.externalCode ?? '', vehicleType: vehicle.vehicleType })
    setMessage('')
  }

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!form.name.trim()) return
    try {
      if (editing) {
        await api.updateVehicle(editing.id, { name: form.name.trim(), code: form.code.trim() || undefined, externalCode: form.externalCode.trim() || undefined, vehicleType: form.vehicleType })
        setMessage('Vehículo actualizado.')
      } else {
        await api.createVehicle({ name: form.name.trim(), code: form.code.trim() || undefined, externalCode: form.externalCode.trim() || undefined, vehicleType: form.vehicleType })
        setMessage('Vehículo creado.')
      }
      reset(); await load(true)
    } catch (caught) { setMessage(caught instanceof Error ? caught.message : 'No se pudo guardar.') }
  }

  async function remove(id: string) {
    if (!window.confirm('¿Eliminar este vehículo? No se borrará de trayectos históricos, solo dejará de estar disponible.')) return
    try { await api.deleteVehicle(id); setMessage('Vehículo eliminado.'); await load(true) }
    catch (caught) { setMessage(caught instanceof Error ? caught.message : 'No se pudo eliminar.') }
  }

  if (loading) return <div className="page"><LoadingState label="Cargando vehículos" /></div>
  if (error && vehicles.length === 0) return <div className="page"><ErrorState message={error} retry={() => void load()} /></div>

  return <div className="page">
    <PageHeader eyebrow="Flota" title="Vehículos" description="Gestiona los vehículos con los que tu organización ejecuta traslados. Puedes definir el código interno (p. ej. AMB-01) o dejarlo vacío para que se genere automáticamente." />
    {message && <div className="inline-message" role="status">{message}</div>}
    <form className="inline-form vehicle-form" onSubmit={(event) => void submit(event)}>
      <label><span>Nombre</span><input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} placeholder="Ambulancia 01" required /></label>
      <label><span>Tipo</span><select value={form.vehicleType} onChange={(e) => setForm({ ...form, vehicleType: e.target.value as VehicleType })}>{(Object.keys(VEHICLE_TYPE_LABELS) as VehicleType[]).map((t) => <option key={t} value={t}>{VEHICLE_TYPE_LABELS[t]}</option>)}</select></label>
      <label><span>Código interno</span><input value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} placeholder="AMB-01 (si lo dejas vacío, se genera)" /></label>
      <label><span>Código externo</span><input value={form.externalCode} onChange={(e) => setForm({ ...form, externalCode: e.target.value })} placeholder="REF-EXT-01" /></label>
      <div className="inline-actions">
        <button className="button button-accent" type="submit">{editing ? 'Guardar cambios' : 'Añadir vehículo'}</button>
        {editing && <button className="button" type="button" onClick={startCreate}>Cancelar</button>}
      </div>
    </form>
    {vehicles.length ? <div className="card-list">{vehicles.map((vehicle) => (
      <article key={vehicle.id} className="list-row">
        <div><strong>{vehicle.name}</strong><span>{vehicle.publicId}{vehicle.externalCode ? ` · ${vehicle.externalCode}` : ''}{` · ${VEHICLE_TYPE_LABELS[vehicle.vehicleType]}`}</span></div>
        <div className="list-actions">
          <button className="button button-small" onClick={() => startEdit(vehicle)}>Editar</button>
          <button className="button button-danger" onClick={() => void remove(vehicle.id)}>Eliminar</button>
        </div>
      </article>
    ))}</div> : <EmptyState title="Sin vehículos" message="Añade tu primer vehículo para poder asignarlo a los traslados." />}
  </div>
}
