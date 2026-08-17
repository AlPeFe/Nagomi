import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from '../router'
import { describe, expect, it, vi } from 'vitest'
import App from '../App'
import { json } from './fixtures'

function renderAt(path: string) { return render(<MemoryRouter initialEntries={[path]}><App /></MemoryRouter>) }

function stubHelpChat(enabled: boolean, reply = 'Respuesta del asistente') {
  vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL) => {
    const url = String(input)
    if (url.includes('/api/help-chat/status')) return json({ enabled })
    if (url.includes('/api/help-chat/messages')) return json({ reply })
    // Unstubbed endpoints (operation lists) return empty collections.
    return json([])
  }))
}

describe('chat de ayuda', () => {
  it('no muestra el botón cuando el chat no está configurado', async () => {
    stubHelpChat(false)
    renderAt('/trayectos')
    await screen.findByRole('navigation', { name: 'Navegación principal' })
    expect(screen.queryByRole('button', { name: 'Abrir chat de ayuda' })).not.toBeInTheDocument()
  })

  it('muestra el botón cuando el chat está habilitado', async () => {
    stubHelpChat(true)
    renderAt('/trayectos')
    expect(await screen.findByRole('button', { name: 'Abrir chat de ayuda' })).toBeInTheDocument()
  })

  it('envía un mensaje y muestra la respuesta del asistente', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (url.includes('/api/help-chat/status')) return json({ enabled: true })
      if (url.includes('/api/help-chat/messages') && init?.method === 'POST') return json({ reply: 'Respuesta del asistente' })
      return json([])
    })
    vi.stubGlobal('fetch', fetchMock)

    const user = userEvent.setup()
    renderAt('/trayectos')
    await user.click(await screen.findByRole('button', { name: 'Abrir chat de ayuda' }))
    await user.type(screen.getByPlaceholderText('Escribe tu consulta…'), '¿cómo creo un traslado?')
    await user.click(screen.getByRole('button', { name: 'Enviar' }))

    expect(await screen.findByText('Respuesta del asistente')).toBeInTheDocument()

    const post = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST')
    const body = JSON.parse(String(post?.[1]?.body))
    expect(body.message).toBe('¿cómo creo un traslado?')
    expect(body.history[0].role).toBe('user')
    expect(body.history[0].content).toBe('¿cómo creo un traslado?')
  })
})
