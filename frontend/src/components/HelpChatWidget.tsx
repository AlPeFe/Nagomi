import { useEffect, useRef, useState } from 'react'
import { ChatCircleDots, PaperPlaneRight, Robot, Sparkle, X } from '@phosphor-icons/react'
import { api } from '../api'
import type { HelpChatMessage } from '../types'

/** Preguntas de arranque: dan una idea de para qué sirve y evitan la pantalla vacía. */
const SUGGESTIONS = [
  '¿Qué traslados tengo hoy?',
  '¿Cómo asigno un vehículo a un trayecto?',
  '¿Qué significa "pendiente de vehículo"?',
  '¿Cómo leo el histórico de traslados?',
]

export function HelpChatWidget() {
  const [enabled, setEnabled] = useState(false)
  const [checked, setChecked] = useState(false)
  const [open, setOpen] = useState(false)
  const [messages, setMessages] = useState<HelpChatMessage[]>([])
  const [draft, setDraft] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const endRef = useRef<HTMLDivElement | null>(null)
  const provider = useRef<string | undefined>(undefined)

  useEffect(() => {
    let active = true
    api.getHelpChatStatus()
      .then((status) => { if (active) { setEnabled(status.enabled); provider.current = status.provider; setChecked(true) } })
      .catch(() => { if (active) setChecked(true) })
    return () => { active = false }
  }, [])

  useEffect(() => {
    endRef.current?.scrollIntoView?.({ behavior: 'smooth' })
  }, [messages, busy, open])

  if (!checked) return null
  if (!enabled) return null

  const send = async (text: string) => {
    const value = text.trim()
    if (!value || busy) return
    const userMessage: HelpChatMessage = { role: 'user', content: value }
    const next = [...messages, userMessage]
    setMessages(next)
    setDraft('')
    setBusy(true)
    setError(null)
    try {
      const reply = await api.sendHelpChatMessage(value, next)
      setMessages([...next, { role: 'assistant', content: reply.reply }])
    } catch (caught) {
      const detail = caught instanceof Error ? caught.message.trim() : ''
      // Un 502 significa que el proveedor no está disponible: mejor decirlo con
      // claridad y apuntar a la configuración que mostrar un error opaco.
      setError(detail && !/^\s*$/.test(detail)
        ? detail
        : 'El asistente no está disponible ahora mismo. Revisa la configuración de IA.')
    } finally {
      setBusy(false)
    }
  }

  return <div className={`help-chat${open ? ' help-chat-open' : ''}`}>
    {open && <section className="help-chat-panel" role="dialog" aria-label="Asistente de Nagomi">
      <header className="help-chat-header">
        <span className="help-chat-avatar" aria-hidden="true"><Robot size={16} weight="duotone" /></span>
        <div className="help-chat-title">
          <strong>Asistente de Nagomi</strong>
          <small><span className="help-chat-dot" aria-hidden="true" />En línea</small>
        </div>
        <button className="icon-button" onClick={() => setOpen(false)} aria-label="Cerrar el asistente">
          <X size={15} aria-hidden="true" />
        </button>
      </header>

      <div className="help-chat-body">
        {!messages.length && !busy && <div className="help-chat-empty">
          <span className="help-chat-empty-icon" aria-hidden="true"><Sparkle size={18} weight="duotone" /></span>
          <strong>¿En qué te ayudo?</strong>
          <p>Pregunta por la operación del día, estados de un traslado o cómo hacer algo en Nagomi.</p>
          <ul className="help-chat-suggestions">
            {SUGGESTIONS.map((suggestion) => (
              <li key={suggestion}>
                <button type="button" onClick={() => void send(suggestion)} disabled={busy}>{suggestion}</button>
              </li>
            ))}
          </ul>
        </div>}

        {messages.map((message, index) => (
          <div key={index} className={`help-chat-row ${message.role === 'user' ? 'help-chat-row-user' : 'help-chat-row-assistant'}`}>
            {message.role !== 'user' && <span className="help-chat-avatar help-chat-avatar-small" aria-hidden="true"><Robot size={13} weight="duotone" /></span>}
            <div className={`help-chat-bubble ${message.role === 'user' ? 'help-chat-user' : 'help-chat-assistant'}`}>{message.content}</div>
          </div>
        ))}

        {busy && <div className="help-chat-row help-chat-row-assistant">
          <span className="help-chat-avatar help-chat-avatar-small" aria-hidden="true"><Robot size={13} weight="duotone" /></span>
          <div className="help-chat-bubble help-chat-assistant help-chat-typing" aria-label="El asistente está escribiendo">
            <span className="help-chat-typing-dot" /><span className="help-chat-typing-dot" /><span className="help-chat-typing-dot" />
          </div>
        </div>}

        {error && <div className="alert alert-error help-chat-error" role="alert">{error}</div>}
        <div ref={endRef} />
      </div>

      <form className="help-chat-form" onSubmit={(event) => { event.preventDefault(); void send(draft) }}>
        <label className="sr-only" htmlFor="help-chat-input">Escribe tu pregunta</label>
        <input id="help-chat-input" value={draft} onChange={(event) => setDraft(event.target.value)}
          placeholder="Escribe tu consulta…" autoComplete="off" />
        <button className="button button-primary help-chat-send" type="submit" disabled={busy || !draft.trim()} aria-label="Enviar">
          <PaperPlaneRight size={15} weight="fill" aria-hidden="true" />
        </button>
      </form>
      <p className="help-chat-foot">
        Respuestas generadas por IA{provider.current ? ` · ${provider.current}` : ''} · pueden contener errores.
      </p>
    </section>}

    <button className="help-chat-fab" onClick={() => setOpen((value) => !value)}
      aria-label={open ? 'Cerrar chat de ayuda' : 'Abrir chat de ayuda'} aria-expanded={open}>
      {open ? <X size={20} weight="bold" aria-hidden="true" /> : <ChatCircleDots size={22} weight="duotone" aria-hidden="true" />}
    </button>
  </div>
}