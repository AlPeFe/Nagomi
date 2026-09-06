import { useEffect, useEffectEvent, useState } from 'react'
import { api } from '../api'
import { EmptyState, ErrorState, LoadingState, PageHeader } from '../components/States'
import type { Patient } from '../types'

interface PatientForm { firstName: string; lastName: string; documentNumber: string; healthCardNumber: string; phone: string; notes: string }

const emptyForm: PatientForm = { firstName: '', lastName: '', documentNumber: '', healthCardNumber: '', phone: '', notes: '' }

export function PatientsPage() {
  const [patients, setPatients] = useState<Patient[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [search, setSearch] = useState('')
  const [form, setForm] = useState<PatientForm>(emptyForm)
  const [editing, setEditing] = useState<Patient | null>(null)

  async function load(silent = false) {
    if (!silent) setLoading(true)
    try { setPatients(await api.listPatients(search.trim() || undefined)); setError('') }
    catch (caught) { setError(caught instanceof Error ? caught.message : 'Error desconocido.') }
    finally { setLoading(false) }
  }
  const loadEffect = useEffectEvent(load)
  useEffect(() => { void loadEffect() }, [])
  useEffect(() => {
    const t = window.setTimeout(() => { void loadEffect(true) }, 250)
    return () => window.clearTimeout(t)
  }, [search])

  function reset() { setForm(emptyForm); setEditing(null) }
  function startCreate() { reset(); setMessage('') }
  function startEdit(patient: Patient) {
    setEditing(patient)
    setForm({ firstName: patient.firstName ?? '', lastName: patient.lastName ?? '', documentNumber: patient.documentNumber ?? '', healthCardNumber: patient.healthCardNumber ?? '', phone: patient.phone ?? '', notes: patient.notes ?? '' })
    setMessage('')
  }

  function body() {
    return {
      firstName: form.firstName.trim() || undefined,
      lastName: form.lastName.trim() || undefined,
      documentNumber: form.documentNumber.trim() || undefined,
      healthCardNumber: form.healthCardNumber.trim() || undefined,
      phone: form.phone.trim() || undefined,
      notes: form.notes.trim() || undefined,
    }
  }

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!form.firstName.trim() && !form.lastName.trim() && !form.documentNumber.trim()) {
      setMessage('Hace falta un nombre o un documento de identidad.')
      return
    }
    try {
      if (editing) {
        await api.updatePatient(editing.id, body())
        setMessage('Paciente actualizado.')
      } else {
        await api.createPatient(body())
        setMessage('Paciente creado.')
      }
      reset(); await load(true)
    } catch (caught) { setMessage(caught instanceof Error ? caught.message : 'No se pudo guardar.') }
  }

  async function remove(id: string) {
    if (!window.confirm('¿Eliminar este paciente? No se borrará de traslados históricos, solo dejará de estar disponible.')) return
    try { await api.deletePatient(id); setMessage('Paciente eliminado.'); await load(true) }
    catch (caught) { setMessage(caught instanceof Error ? caught.message : 'No se pudo eliminar.') }
  }

  const fullName = (patient: Patient) => [patient.firstName, patient.lastName].filter(Boolean).join(' ') || patient.publicId

  if (loading) return <div className="page"><LoadingState label="Cargando pacientes" /></div>
  if (error && patients.length === 0) return <div className="page"><ErrorState message={error} retry={() => void load()} /></div>

  return <div className="page">
    <PageHeader eyebrow="Directorio" title="Pacientes" description="Directorio de pacientes: al crear una solicitud, si el paciente ya existe se reutiliza en vez de duplicarse. Los datos sensibles solo se muestran aquí y en el detalle, nunca en los listados operativos." />
    {message && <div className="inline-message" role="status">{message}</div>}
    <form className="inline-form patient-form" onSubmit={(event) => void submit(event)}>
      <label><span>Nombre</span><input value={form.firstName} onChange={(e) => setForm({ ...form, firstName: e.target.value })} placeholder="María" /></label>
      <label><span>Apellidos</span><input value={form.lastName} onChange={(e) => setForm({ ...form, lastName: e.target.value })} placeholder="García López" /></label>
      <label><span>Documento</span><input value={form.documentNumber} onChange={(e) => setForm({ ...form, documentNumber: e.target.value })} placeholder="DNI / NIE" /></label>
      <label><span>Tarjeta sanitaria</span><input value={form.healthCardNumber} onChange={(e) => setForm({ ...form, healthCardNumber: e.target.value })} placeholder="CIP / TSI" /></label>
      <label><span>Teléfono</span><input value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} type="tel" placeholder="600 000 000" /></label>
      <label className="span-2"><span>Notas</span><input value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} placeholder="Observaciones (opcional)" /></label>
      <div className="inline-actions">
        <button className="button button-accent" type="submit">{editing ? 'Guardar cambios' : 'Añadir paciente'}</button>
        {editing && <button className="button" type="button" onClick={startCreate}>Cancelar</button>}
      </div>
    </form>
    <div className="table-toolbar">
      <input className="search-input" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Buscar por nombre, apellidos o documento…" aria-label="Buscar pacientes" />
    </div>
    {patients.length ? <div className="card-list">{patients.map((patient) => (
      <article key={patient.id} className="list-row">
        <div><strong>{fullName(patient)}</strong><span>{patient.publicId}{patient.documentNumber ? ` · ${patient.documentNumber}` : ''}{patient.phone ? ` · ${patient.phone}` : ''}</span></div>
        <div className="list-actions">
          <button className="button button-small" onClick={() => startEdit(patient)}>Editar</button>
          <button className="button button-danger" onClick={() => void remove(patient.id)}>Eliminar</button>
        </div>
      </article>
    ))}</div> : <EmptyState title="Sin pacientes" message="Añade tu primer paciente o créalo directamente al preparar un transporte." />}
  </div>
}
