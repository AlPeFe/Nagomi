import { useEffect, useState } from 'react'
import { CheckCircle, PlugsConnected, Robot, XCircle } from '@phosphor-icons/react'
import { api } from '../api'
import type { AiSettings } from '../types'

/**
 * Configuración del asistente de IA. Por defecto apunta al gateway de Hermes, que
 * autentica con usuario y contraseña (Basic); también vale cualquier proveedor
 * OpenAI-compatible o un Ollama local (sin credenciales).
 *
 * La contraseña se guarda en el servidor y NUNCA se devuelve: el campo aparece vacío
 * con la pista "sin cambios" y sólo se envía si se escribe una nueva.
 */
const PROVIDERS = [
  { value: 'hermes', label: 'Gateway de Hermes (usuario y contraseña)', model: 'hermes' },
  { value: 'openai', label: 'Proveedor OpenAI-compatible (clave de API)', model: '' },
  { value: 'ollama', label: 'Ollama local (sin credenciales)', model: 'llama3.1' },
]

export function AiSettingsCard() {
  const [settings, setSettings] = useState<AiSettings | null>(null)
  const [enabled, setEnabled] = useState(false)
  const [provider, setProvider] = useState('hermes')
  const [baseUrl, setBaseUrl] = useState('')
  const [model, setModel] = useState('')
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [enableTools, setEnableTools] = useState(true)
  const [systemPrompt, setSystemPrompt] = useState('')
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [test, setTest] = useState<{ ok: boolean; detail: string } | null>(null)

  useEffect(() => {
    void api.getAiSettings().then((value) => {
      setSettings(value)
      setEnabled(value.enabled); setProvider(value.provider ?? 'hermes')
      setBaseUrl(value.baseUrl ?? ''); setModel(value.model ?? ''); setUsername(value.username ?? '')
      setEnableTools(value.enableTools); setSystemPrompt(value.systemPrompt ?? '')
    }).catch((caught) => setError(caught instanceof Error ? caught.message : 'No se pudo cargar la configuración de IA.'))
  }, [])

  async function save(event: React.FormEvent) {
    event.preventDefault()
    setBusy(true); setNotice(''); setError('')
    try {
      const value = await api.updateAiSettings({
        enabled, provider, baseUrl, model, username,
        // Cadena vacía = no tocar la contraseña guardada.
        password: password || undefined,
        systemPrompt, enableTools,
      })
      setSettings(value); setPassword('')
      setNotice('Configuración de IA guardada.')
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'No se pudo guardar.') }
    finally { setBusy(false) }
  }

  async function runTest() {
    setBusy(true); setTest(null); setError('')
    try {
      const result = await api.testAiConnection({ baseUrl, model, username, password: password || undefined, provider })
      setTest({ ok: result.ok, detail: result.detail })
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'No se pudo probar la conexión.') }
    finally { setBusy(false) }
  }

  async function clearPassword() {
    setBusy(true)
    try {
      const value = await api.updateAiSettings({ enabled, provider, baseUrl, model, username, clearPassword: true, systemPrompt, enableTools })
      setSettings(value); setNotice('Contraseña borrada.')
    } catch (caught) { setError(caught instanceof Error ? caught.message : 'No se pudo borrar la contraseña.') }
    finally { setBusy(false) }
  }

  return <section className="config-card">
    <div className="section-heading">
      <h2><Robot size={15} aria-hidden="true" /> Asistente de IA</h2>
      <p>Contra qué modelo responde el chat de ayuda. Por defecto, el gateway de Hermes (usuario y contraseña); también sirve cualquier proveedor OpenAI-compatible u Ollama local.</p>
    </div>
    {notice && <div className="alert alert-success" role="status">{notice}</div>}
    {error && <div className="alert alert-error" role="alert">{error}</div>}

    {!settings ? <p className="muted">Cargando configuración…</p> : <form className="ai-form" onSubmit={save}>
      <div className="form-row">
        <label className="field"><span>Proveedor</span>
          <select value={provider} onChange={(e) => setProvider(e.target.value)}>
            {PROVIDERS.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
          </select>
        </label>
        <label className="field span-2"><span>URL del gateway</span>
          <input value={baseUrl} onChange={(e) => setBaseUrl(e.target.value)} placeholder="http://192.168.31.223:9119/v1" />
        </label>
        <label className="field"><span>Modelo</span>
          <input value={model} onChange={(e) => setModel(e.target.value)} placeholder="hermes" />
        </label>
      </div>
      <div className="form-row">
        <label className="field"><span>Usuario del gateway</span>
          <input value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="off" placeholder="opcional" />
        </label>
        <label className="field"><span>Contraseña</span>
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="new-password"
            placeholder={settings.hasPassword ? '•••••• (sin cambios)' : 'sin contraseña'} />
        </label>
      </div>
      <p className="muted ai-hint">
        La contraseña se guarda en el servidor y no se devuelve nunca al navegador.
        {settings.hasPassword && <> Ya hay una guardada: deja el campo vacío para conservarla o <button type="button" className="link-button" onClick={() => void clearPassword()} disabled={busy}>bórrala</button>.</>}
      </p>
      <label className="check-line"><input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} /><span>Asistente activo (muestra el botón de ayuda en la aplicación)</span></label>
      <label className="check-line"><input type="checkbox" checked={enableTools} onChange={(e) => setEnableTools(e.target.checked)} /><span>Permitir consultar datos reales (pacientes, trayectos, flota)</span></label>
      <label className="field"><span>Instrucciones del asistente (opcional)</span>
        <textarea value={systemPrompt} onChange={(e) => setSystemPrompt(e.target.value)} rows={3} placeholder="Deja vacío para usar el comportamiento por defecto." />
      </label>

      <div className="form-actions ai-actions">
        <button className="button button-secondary" type="button" onClick={() => void runTest()} disabled={busy}>
          <PlugsConnected size={14} aria-hidden="true" /> Probar conexión
        </button>
        <button className="button button-primary" type="submit" disabled={busy}>{busy ? 'Guardando…' : 'Guardar'}</button>
      </div>
      {test && <div className={`alert ${test.ok ? 'alert-success' : 'alert-error'}`} role="status">
        {test.ok ? <CheckCircle size={14} aria-hidden="true" /> : <XCircle size={14} aria-hidden="true" />} {test.detail}
      </div>}
    </form>}
  </section>
}