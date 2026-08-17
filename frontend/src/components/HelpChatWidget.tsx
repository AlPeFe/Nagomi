import { useEffect, useRef, useState } from 'react'
import { api } from '../api'
import type { HelpChatMessage } from '../types'

export function HelpChatWidget() {
  const [enabled, setEnabled] = useState(false)
  const [checked, setChecked] = useState(false)
  const [open, setOpen] = useState(false)
  const [messages, setMessages] = useState<HelpChatMessage[]>([])
  const [draft, setDraft] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const endRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    let active = true
    api.getHelpChatStatus()
      .then((status) => { if (active) { setEnabled(status.enabled); setChecked(true) } })
      .catch(() => { if (active) setChecked(true) })
    return () => { active = false }
  }, [])

  useEffect(() => {
    endRef.current?.scrollIntoView?.({ behavior: 'smooth' })
  }, [messages, busy])

  if (!checked) return null
  if (!enabled) return null

  const send = async () => {
    const text = draft.trim()
    if (!text || busy) return
    const userMessage: HelpChatMessage = { role: 'user', content: text }
    const next = [...messages, userMessage]
    setMessages(next)
    setDraft('')
    setBusy(true)
    setError(null)
    try {
      const reply = await api.sendHelpChatMessage(text, next)
      setMessages((current) => [...current, { role: 'assistant', content: reply.reply }])
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo obtener respuesta del asistente.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="help-chat">
      {open && (
        <div className="help-chat-panel" role="dialog" aria-label="Chat de ayuda">
          <div className="help-chat-header">
            <strong>Ayuda</strong>
            <button type="button" className="button button-small" onClick={() => setOpen(false)} aria-label="Cerrar chat de ayuda">×</button>
          </div>
          <div className="help-chat-body">
            {messages.length === 0 && <p className="muted help-chat-empty">¿En qué puedo ayudarte?</p>}
            {messages.map((message, index) => (
              <div key={index} className={`help-chat-bubble ${message.role === 'user' ? 'help-chat-user' : 'help-chat-assistant'}`}>
                {message.content}
              </div>
            ))}
            {busy && <div className="help-chat-bubble help-chat-assistant help-chat-typing">Escribiendo…</div>}
            {error && <div className="alert alert-error help-chat-error" role="alert">{error}</div>}
            <div ref={endRef} />
          </div>
          <form className="help-chat-form" onSubmit={(event) => { event.preventDefault(); void send() }}>
            <input
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              placeholder="Escribe tu consulta…"
              aria-label="Mensaje para el asistente"
              disabled={busy}
            />
            <button className="button button-accent" type="submit" disabled={busy || !draft.trim()}>Enviar</button>
          </form>
        </div>
      )}
      <button
        type="button"
        className="help-chat-fab"
        onClick={() => setOpen((value) => !value)}
        aria-label={open ? 'Cerrar chat de ayuda' : 'Abrir chat de ayuda'}
      >
        {open ? '×' : '?'}
      </button>
    </div>
  )
}
