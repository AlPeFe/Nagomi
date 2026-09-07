import { useState } from 'react'
import { Navigate, useNavigate } from '../router'
import { api } from '../api'
import { login, onboardingRequired, logout } from '../auth'

/**
 * First-run onboarding: the bootstrap account (admin / Admin) must create the real
 * administrator. Once created, the bootstrap is deactivated and we sign in with the
 * new account.
 */
export function OnboardingPage() {
  const navigate = useNavigate()
  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [userName, setUserName] = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  if (!onboardingRequired()) {
    // Not the bootstrap account: nothing to onboard.
    return <Navigate to="/trayectos" replace />
  }

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    setError(null)
    if (password !== confirm) {
      setError('Las contraseñas no coinciden.')
      return
    }
    if (password.length < 12) {
      setError('La contraseña debe tener al menos 12 caracteres, con mayúsculas, minúsculas y números.')
      return
    }
    setBusy(true)
    try {
      await api.onboardAdmin({
        displayName: displayName.trim(),
        email: email.trim(),
        userName: userName.trim(),
        password,
      })
      // Bootstrap is deactivated: sign in with the new administrator.
      logout()
      await login(userName.trim(), password)
      navigate('/trayectos', { replace: true })
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo crear el administrador.')
      setBusy(false)
    }
  }

  return (
    <div className="login-page">
      <form className="login-card" onSubmit={submit}>
        <span className="brand-mark" aria-hidden="true">N</span>
        <h1>Primera configuración</h1>
        <p className="login-subtitle">Crea la cuenta de administrador de tu organización. La cuenta inicial (admin / Admin) se desactivará al completar el alta.</p>
        {error && <div className="alert alert-error" role="alert">{error}</div>}
        <label className="field">
          <span>Nombre</span>
          <input value={displayName} onChange={(e) => setDisplayName(e.target.value)} placeholder="Nombre y apellidos" required autoFocus />
        </label>
        <label className="field">
          <span>Correo electrónico</span>
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="username" placeholder="admin@tuempresa.es" required />
        </label>
        <label className="field">
          <span>Usuario</span>
          <input value={userName} onChange={(e) => setUserName(e.target.value)} autoComplete="username" placeholder="admin" required />
        </label>
        <label className="field">
          <span>Contraseña (mínimo 12 caracteres)</span>
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="new-password" required />
        </label>
        <label className="field">
          <span>Repite la contraseña</span>
          <input type="password" value={confirm} onChange={(e) => setConfirm(e.target.value)} autoComplete="new-password" required />
        </label>
        <button className="button button-accent" type="submit" disabled={busy}>
          {busy ? 'Creando…' : 'Crear administrador y entrar'}
        </button>
      </form>
    </div>
  )
}
