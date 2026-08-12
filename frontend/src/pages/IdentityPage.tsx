import { useCallback, useEffect, useState } from 'react'
import { api, type AdminUserRow, type ApiClientRow } from '../api'
import { getToken, me } from '../auth'

const ROLES = ['admin', 'default']

export function IdentityPage() {
  // --- Usuarios (single-tenant) ---
  const [users, setUsers] = useState<AdminUserRow[]>([])
  const [currentUserId, setCurrentUserId] = useState<string | null>(null)
  const [newEmail, setNewEmail] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [newRole, setNewRole] = useState('default')
  const [resetPasswordFor, setResetPasswordFor] = useState<string | null>(null)
  const [resetPassword, setResetPassword] = useState('')

  // --- Clientes de API (M2M) ---
  const [clients, setClients] = useState<ApiClientRow[]>([])
  const [newClientId, setNewClientId] = useState('')
  const [newClientName, setNewClientName] = useState('')
  const [createdSecret, setCreatedSecret] = useState<{ clientId: string; clientSecret: string } | null>(null)

  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const flash = (message: string) => {
    setNotice(message)
    window.setTimeout(() => setNotice(null), 4000)
  }

  const refresh = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [rows, current, clientRows] = await Promise.all([api.listUsers(), me(), api.listApiClients()])
      setUsers(rows)
      setCurrentUserId(current.id)
      setClients(clientRows)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo cargar el sistema de identidad.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void refresh()
  }, [refresh])

  const createUser = async (event: React.FormEvent) => {
    event.preventDefault()
    setBusy(true)
    try {
      await api.createUser(newEmail, newPassword, newRole)
      setNewEmail(''); setNewPassword(''); setNewRole('default')
      flash('Usuario creado.')
      await refresh()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo crear el usuario.')
    } finally {
      setBusy(false)
    }
  }

  const changeRole = async (user: AdminUserRow, role: string) => {
    try { await api.updateUser(user.id, { role }); await refresh() }
    catch (err) { setError(err instanceof Error ? err.message : 'No se pudo cambiar el rol.') }
  }

  const toggleActive = async (user: AdminUserRow) => {
    try { await api.updateUser(user.id, { isActive: !user.isActive }); await refresh() }
    catch (err) { setError(err instanceof Error ? err.message : 'No se pudo actualizar el estado.') }
  }

  const removeUser = async (user: AdminUserRow) => {
    try { await api.deleteUser(user.id); flash('Usuario eliminado.'); await refresh() }
    catch (err) { setError(err instanceof Error ? err.message : 'No se pudo eliminar el usuario.') }
  }

  const saveResetPassword = async (user: AdminUserRow) => {
    try { await api.updateUser(user.id, { password: resetPassword }); flash('Contraseña actualizada.'); setResetPasswordFor(null); setResetPassword('') }
    catch (err) { setError(err instanceof Error ? err.message : 'No se pudo actualizar la contraseña.') }
  }

  const createApiClient = async (event: React.FormEvent) => {
    event.preventDefault()
    setBusy(true)
    setCreatedSecret(null)
    try {
      const created = await api.createApiClient(newClientId.trim(), newClientName.trim() || undefined)
      setCreatedSecret({ clientId: created.clientId, clientSecret: created.clientSecret })
      setNewClientId(''); setNewClientName('')
      await refresh()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo crear el cliente de API.')
    } finally {
      setBusy(false)
    }
  }

  const rotateSecret = async (client: ApiClientRow) => {
    try {
      const rotated = await api.rotateApiClientSecret(client.clientId)
      setCreatedSecret({ clientId: rotated.clientId, clientSecret: rotated.clientSecret })
      await refresh()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo rotar el secreto.')
    }
  }

  const revokeClient = async (client: ApiClientRow) => {
    try { await api.deleteApiClient(client.clientId); flash('Cliente revocado.'); await refresh() }
    catch (err) { setError(err instanceof Error ? err.message : 'No se pudo revocar el cliente.') }
  }

  return (
    <section className="page">
      <h1>Identidad y acceso</h1>
      <p className="muted">Sistema central sobre OpenIddict: gestiona los usuarios (single-tenant) y los clientes de API para consumidores (flujo M2M). Solo administradores.</p>

      {notice && <div className="alert alert-success">{notice}</div>}
      {error && <div className="alert alert-error" role="alert">{error}</div>}

      {createdSecret && (
        <div className="alert alert-info">
          <strong>Cliente {createdSecret.clientId}</strong> creado. Copia el secreto ahora: no se volverá a mostrar.
          <pre className="secret-box">{createdSecret.clientSecret}</pre>
          <button className="button button-small" onClick={() => setCreatedSecret(null)}>Entendido</button>
        </div>
      )}

      {loading ? (
        <p className="muted">Cargando…</p>
      ) : (
        <>
          {/* ============ USUARIOS ============ */}
          <div className="card">
            <h2>Usuarios</h2>
            <form className="form-row" onSubmit={createUser}>
              <label className="field">
                <span>Correo electrónico</span>
                <input type="email" value={newEmail} onChange={(e) => setNewEmail(e.target.value)} required />
              </label>
              <label className="field">
                <span>Contraseña inicial</span>
                <input type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} required minLength={8} />
              </label>
              <label className="field">
                <span>Rol</span>
                <select value={newRole} onChange={(e) => setNewRole(e.target.value)}>
                  {ROLES.map((role) => <option key={role} value={role}>{role}</option>)}
                </select>
              </label>
              <button className="button button-accent" type="submit" disabled={busy || !getToken()}>
                {busy ? 'Creando…' : 'Crear'}
              </button>
            </form>

            <div className="table-wrap">
              <table className="table">
                <thead>
                  <tr>
                    <th>Correo</th>
                    <th>Rol</th>
                    <th>Estado</th>
                    <th>Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  {users.map((user) => (
                    <tr key={user.id}>
                      <td>
                        <strong>{user.email}</strong>
                        {user.id === currentUserId && <span className="badge">tú</span>}
                      </td>
                      <td>
                        <select
                          value={user.roles.includes('admin') ? 'admin' : 'default'}
                          onChange={(e) => void changeRole(user, e.target.value)}
                          disabled={user.id === currentUserId}
                          title={user.id === currentUserId ? 'No puedes cambiar tu propio rol.' : undefined}
                        >
                          {ROLES.map((role) => <option key={role} value={role}>{role}</option>)}
                        </select>
                      </td>
                      <td>
                        <span className={user.isActive ? 'badge badge-ok' : 'badge badge-off'}>
                          {user.isActive ? 'Activo' : 'Desactivado'}
                        </span>
                      </td>
                      <td className="actions-cell">
                        <button className="button button-small" onClick={() => void toggleActive(user)}>
                          {user.isActive ? 'Desactivar' : 'Activar'}
                        </button>
                        <button className="button button-small" onClick={() => setResetPasswordFor(user.id)}>
                          Contraseña
                        </button>
                        <button className="button button-small button-danger" onClick={() => void removeUser(user)} disabled={user.id === currentUserId}>
                          Eliminar
                        </button>
                        {resetPasswordFor === user.id && (
                          <span className="inline-reset">
                            <input type="password" placeholder="Nueva contraseña" value={resetPassword} minLength={8} onChange={(e) => setResetPassword(e.target.value)} />
                            <button className="button button-small" onClick={() => void saveResetPassword(user)} disabled={resetPassword.length < 8}>Guardar</button>
                            <button className="button button-small" onClick={() => { setResetPasswordFor(null); setResetPassword('') }}>Cancelar</button>
                          </span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          {/* ============ CLIENTES DE API (M2M) ============ */}
          <div className="card">
            <h2>Clientes de API para consumidores</h2>
            <p className="muted">
              Credenciales M2M (OAuth 2.0 <code>client_credentials</code>): el consumidor obtiene un token desde{' '}
              <code>/connect/token</code> con su <code>client_id</code> y <code>client_secret</code>.
            </p>
            <form className="form-row" onSubmit={createApiClient}>
              <label className="field">
                <span>ID de cliente</span>
                <input value={newClientId} onChange={(e) => setNewClientId(e.target.value)} placeholder="mutua-pepe-api" required />
              </label>
              <label className="field">
                <span>Nombre descriptivo</span>
                <input value={newClientName} onChange={(e) => setNewClientName(e.target.value)} placeholder="Mutua Pepe — integración" />
              </label>
              <button className="button button-accent" type="submit" disabled={busy || !getToken()}>
                {busy ? 'Creando…' : 'Crear cliente'}
              </button>
            </form>

            <div className="table-wrap">
              <table className="table">
                <thead>
                  <tr>
                    <th>Client ID</th>
                    <th>Nombre</th>
                    <th>Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  {clients.map((client) => (
                    <tr key={client.clientId}>
                      <td><code>{client.clientId}</code></td>
                      <td>{client.displayName || <span className="muted">—</span>}</td>
                      <td className="actions-cell">
                        <button className="button button-small" onClick={() => void rotateSecret(client)}>Rotar secreto</button>
                        <button className="button button-small button-danger" onClick={() => void revokeClient(client)}>Revocar</button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}
    </section>
  )
}
