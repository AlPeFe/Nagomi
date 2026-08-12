import { useCallback, useEffect, useState } from 'react'
import { api } from '../api'
import { PageHeader } from '../components/States'
import type { QueueMessageSample, QueueSnapshot, TenantCapabilities, TransportClient } from '../types'

export function TenantConfigPage() {
  const [caps, setCaps] = useState<TenantCapabilities | null>(null)
  const [clients, setClients] = useState<TransportClient[]>([])
  const [queues, setQueues] = useState<QueueSnapshot[]>([])
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)

  // client editor
  const [editorOpen, setEditorOpen] = useState(false)
  const [editing, setEditing] = useState<TransportClient | null>(null)
  const [form, setForm] = useState({ name: '', taxId: '', contactPerson: '', phone: '', email: '', address: '' })

  // queue peek
  const [peekFor, setPeekFor] = useState<string | null>(null)
  const [peeked, setPeeked] = useState<QueueMessageSample[] | null>(null)
  const [peekError, setPeekError] = useState<string | null>(null)

  const flash = (message: string) => {
    setNotice(message)
    window.setTimeout(() => setNotice(null), 4000)
  }

  const refresh = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [capsValue, clientsValue, queuesValue] = await Promise.all([
        api.getCapabilities(), api.listClients(true), api.listQueueSnapshots(),
      ])
      setCaps(capsValue)
      setClients(clientsValue)
      setQueues(queuesValue)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo cargar la configuración.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void refresh()
  }, [refresh])

  const saveCaps = async (patch: Partial<TenantCapabilities>) => {
    if (!caps) return
    const next = { ...caps, ...patch }
    try {
      await api.updateCapabilities(next)
      setCaps(next)
      flash('Capacidades actualizadas.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudieron actualizar las capacidades.')
    }
  }

  const openCreate = () => {
    setEditing(null)
    setForm({ name: '', taxId: '', contactPerson: '', phone: '', email: '', address: '' })
    setEditorOpen(true)
  }

  const openEdit = (client: TransportClient) => {
    setEditing(client)
    setForm({ name: client.name, taxId: client.taxId ?? '', contactPerson: client.contactPerson ?? '', phone: client.phone ?? '', email: client.email ?? '', address: client.address ?? '' })
    setEditorOpen(true)
  }

  const saveClient = async (event: React.FormEvent) => {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      if (editing) await api.updateClient(editing.id, { ...form, isActive: editing.isActive })
      else await api.createClient(form)
      flash(editing ? 'Cliente actualizado.' : 'Cliente creado.')
      setEditorOpen(false)
      await refresh()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo guardar el cliente.')
    } finally {
      setBusy(false)
    }
  }

  const toggleClient = async (client: TransportClient) => {
    try {
      await api.updateClient(client.id, { name: client.name, taxId: client.taxId, contactPerson: client.contactPerson, phone: client.phone, email: client.email, address: client.address, isActive: !client.isActive })
      flash(client.isActive ? 'Cliente desactivado.' : 'Cliente activado.')
      await refresh()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo actualizar el cliente.')
    }
  }

  const peek = async (queueName: string) => {
    setPeekFor(queueName)
    setPeeked(null)
    setPeekError(null)
    try {
      setPeeked(await api.peekQueue(queueName, 10))
    } catch (err) {
      setPeekError(err instanceof Error ? err.message : 'No se pudo inspeccionar la cola.')
    }
  }

  return (
    <section className="page">
      <PageHeader eyebrow="Configuración" title="Configuración del tenant" description="Define el rol operativo de esta instalación y gestiona los clientes facturables." />

      {notice && <div className="alert alert-success">{notice}</div>}
      {error && <div className="alert alert-error" role="alert">{error}</div>}

      {loading ? <p className="muted">Cargando configuración…</p> : (
        <>
          <section className="config-card">
            <div className="section-heading"><h2>Capacidades operativas</h2><p>Puedes habilitar varias a la vez. Definen qué se puede hacer en esta instalación.</p></div>
            <div className="capability-grid">
              <label className="capability-check">
                <input type="checkbox" checked={!!caps?.publishesRequests} onChange={(e) => void saveCaps({ publishesRequests: e.target.checked })} />
                <span className="capability-text"><strong>Publicar a proveedores</strong><small>Modo peticionario: enrutar traslados a contratos externos.</small></span>
              </label>
              <label className="capability-check">
                <input type="checkbox" checked={!!caps?.executesTransports} onChange={(e) => void saveCaps({ executesTransports: e.target.checked })} />
                <span className="capability-text"><strong>Ejecutar traslados propios</strong><small>Modo empresa de ambulancias: la instalación se registra como proveedor de sí misma.</small></span>
              </label>
              <label className="capability-check">
                <input type="checkbox" checked={!!caps?.handlesEmergencies} onChange={(e) => void saveCaps({ handlesEmergencies: e.target.checked })} />
                <span className="capability-text"><strong>Atención de urgencias</strong><small>Modo urgencia: exponer el módulo de transporte de emergencias.</small></span>
              </label>
            </div>
          </section>

          <section className="config-card">
            <div className="section-heading"><h2>Clientes facturables</h2><p>Organizaciones o particulares a los que se factura un traslado. No tienen integración ni cola.</p></div>
            <div className="table-toolbar">
              <button className="button button-accent" onClick={openCreate}>Nuevo cliente</button>
            </div>
            {editorOpen && (
              <form className="client-editor" onSubmit={(e) => void saveClient(e)}>
                <h3>{editing ? 'Editar cliente' : 'Nuevo cliente'}</h3>
                <div className="form-row">
                  <label className="field"><span>Nombre *</span><input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required /></label>
                  <label className="field"><span>NIF / CIF</span><input value={form.taxId} onChange={(e) => setForm({ ...form, taxId: e.target.value })} /></label>
                  <label className="field"><span>Contacto</span><input value={form.contactPerson} onChange={(e) => setForm({ ...form, contactPerson: e.target.value })} /></label>
                  <label className="field"><span>Teléfono</span><input value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} /></label>
                  <label className="field"><span>Correo</span><input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} /></label>
                  <label className="field span-2"><span>Dirección</span><input value={form.address} onChange={(e) => setForm({ ...form, address: e.target.value })} /></label>
                </div>
                <div className="form-actions">
                  <button className="button button-accent" disabled={busy || !form.name.trim()}>{busy ? 'Guardando…' : 'Guardar'}</button>
                  <button type="button" className="button" onClick={() => setEditorOpen(false)}>Cancelar</button>
                </div>
              </form>
            )}
            <div className="table-wrap">
              <table className="table">
                <thead><tr><th>Cliente</th><th>NIF / CIF</th><th>Contacto</th><th>Estado</th><th>Acciones</th></tr></thead>
                <tbody>
                  {clients.map((client) => (
                    <tr key={client.id}>
                      <td><strong>{client.name}</strong><div className="muted">{client.publicId}</div></td>
                      <td>{client.taxId || '—'}</td>
                      <td>{[client.contactPerson, client.phone, client.email].filter(Boolean).join(' · ') || '—'}</td>
                      <td><span className={client.isActive ? 'badge badge-ok' : 'badge badge-off'}>{client.isActive ? 'Activo' : 'Desactivado'}</span></td>
                      <td className="actions-cell">
                        <button className="button button-small" onClick={() => openEdit(client)}>Editar</button>
                        <button className="button button-small" onClick={() => void toggleClient(client)}>{client.isActive ? 'Desactivar' : 'Activar'}</button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          <section className="config-card">
            <div className="section-heading"><h2>Colas de RabbitMQ</h2><p>Estado actual de las colas de los proveedores y vista previa no destructiva de los mensajes publicados.</p></div>
            <div className="table-wrap">
              <table className="table">
                <thead><tr><th>Proveedor</th><th>Cola</th><th>Mensajes</th><th>Consumidores</th><th>Acciones</th></tr></thead>
                <tbody>
                  {queues.length === 0 && <tr><td colSpan={5} className="muted">No hay colas de proveedores activas.</td></tr>}
                  {queues.map((queue) => (
                    <tr key={queue.providerId}>
                      <td><strong>{queue.providerName}</strong><div className="muted">{queue.providerCode}</div></td>
                      <td><code>{queue.queueName}</code></td>
                      <td>
                        {queue.error ? <span className="badge badge-off" title={queue.error}>No accesible</span> : (
                          <span className={queue.messages > 0 ? 'badge' : 'badge badge-ok'}>{queue.messages}</span>
                        )}
                      </td>
                      <td>{queue.consumers}</td>
                      <td className="actions-cell"><button className="button button-small" onClick={() => void peek(queue.queueName)}>Inspeccionar</button></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {peekFor && (
              <div className="queue-peek">
                <h3>Mensajes en <code>{peekFor}</code></h3>
                {peekError && <div className="alert alert-error" role="alert">{peekError}</div>}
                {peeked === null && !peekError && <p className="muted">Inspeccionando cola…</p>}
                {peeked?.length === 0 && <p className="muted">La cola está vacía.</p>}
                {peeked?.map((message, index) => (
                  <details className="queue-message" key={index}>
                    <summary>
                      <strong>{message.messageType || 'Mensaje'}</strong>
                      {message.messageId && <code>{message.messageId.slice(0, 8)}</code>}
                      {message.redelivered && <span className="badge">reentregado</span>}
                    </summary>
                    <pre>{message.body}</pre>
                  </details>
                ))}
                <button className="button button-small" onClick={() => setPeekFor(null)}>Cerrar</button>
              </div>
            )}
          </section>
        </>
      )}
    </section>
  )
}
