import { useCallback, useEffect, useState } from 'react'
import { api, type AdminUserRow } from '../api'
import { getToken, me } from '../auth'

const ROLES = ['admin', 'default']

export function UsersPage() {
  const [users, setUsers] = useState<AdminUserRow[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [currentUserId, setCurrentUserId] = useState<string | null>(null)

  const [newEmail, setNewEmail] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [newRole, setNewRole] = useState('default')
  const [busy, setBusy] = useState(false)
  const [resetPasswordFor, setResetPasswordFor] = useState<string | null>(null)
  const [resetPassword, setResetPassword] = useState('')

  const refresh = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [rows, current] = await Promise.all([api.listUsers(), me()])
      setUsers(rows)
      setCurrentUserId(current.id)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudieron cargar los usuarios.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void refresh()
  }, [refresh])

  const flash = (message: string) => {
    setNotice(message)
    window.setTimeout(() => setNotice(null), 4000)
  }

  const createUser = async (event: React.FormEvent) => {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await api.createUser(newEmail.trim(), newPassword, newRole)
      setNewEmail('')
      setNewPassword('')
      setNewRole('default')
      flash('Usuario creado.')
      await refresh()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo crear el usuario.')
    } finally {
      setBusy(false)
    }
  }

  const toggleActive = async (user: AdminUserRow) => {
    try {
      await api.updateUser(user.id, { isActive: !user.isActive })
      flash(user.isActive ? 'Usuario desactivado.' : 'Usuario activado.')
      await refresh()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo actualizar el usuario.')
    }
  }

  const changeRole = async (user: AdminUserRow, role: string) => {
    try {
      await api.updateUser(user.id, { role })
      flash('Rol actualizado.')
      await refresh()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo actualizar el rol.')
    }
  }

  const saveResetPassword = async (user: AdminUserRow) => {
    try {
      await api.updateUser(user.id, { password: resetPassword })
      setResetPasswordFor(null)
      setResetPassword('')
      flash('Contraseña actualizada.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo actualizar la contraseña.')
    }
  }

  const removeUser = async (user: AdminUserRow) => {
    if (!window.confirm(`¿Eliminar el usuario ${user.email}?`)) return
    try {
      await api.deleteUser(user.id)
      flash('Usuario eliminado.')
      await refresh()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo eliminar el usuario.')
    }
  }

  return (
    <section className="page">
      <h1>Usuarios</h1>

      {notice && <div className="alert alert-success">{notice}</div>}
      {error && <div className="alert alert-error" role="alert">{error}</div>}

      <form className="card create-user" onSubmit={createUser}>
        <h2>Nuevo usuario</h2>
        <div className="form-row">
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
        </div>
      </form>

      {loading ? (
        <p className="muted">Cargando usuarios…</p>
      ) : (
        <div className="table-wrap card">
          <table className="table users-table">
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
                    {user.displayName && <div className="muted">{user.displayName}</div>}
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
                        <input
                          type="password"
                          placeholder="Nueva contraseña"
                          value={resetPassword}
                          minLength={8}
                          onChange={(e) => setResetPassword(e.target.value)}
                        />
                        <button className="button button-small" onClick={() => void saveResetPassword(user)} disabled={resetPassword.length < 8}>
                          Guardar
                        </button>
                        <button className="button button-small" onClick={() => { setResetPasswordFor(null); setResetPassword('') }}>
                          Cancelar
                        </button>
                      </span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
