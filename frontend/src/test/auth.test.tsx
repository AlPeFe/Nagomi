import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from '../router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../App'

function renderAt(path: string) {
  return render(<MemoryRouter initialEntries={[path]}><App /></MemoryRouter>)
}

function json(body: unknown) {
  return Promise.resolve(new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  }))
}

describe('autenticación', () => {
  beforeEach(() => {
    localStorage.clear()
    sessionStorage.clear()
  })

  it('redirige al login cuando no hay sesión', async () => {
    vi.stubGlobal('fetch', vi.fn(() => json([])))
    renderAt('/trayectos')
    expect(await screen.findByRole('heading', { name: /Accede a Nagomi/i })).toBeInTheDocument()
  })

  it('inicia sesión y navega a la operación', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = String(input)
      if (url.includes('/connect/token')) return json({ access_token: 'tok-123' })
      if (url.includes('/api/auth/me')) {
        return json({ id: 'u1', name: 'admin@nagomi.local', email: 'admin@nagomi.local', displayName: null, roles: ['admin'] })
      }
      return json([])
    })
    vi.stubGlobal('fetch', fetchMock)

    renderAt('/login')
    await userEvent.type(await screen.findByLabelText(/Correo electrónico/i), 'admin@nagomi.local')
    await userEvent.type(screen.getByLabelText(/Contraseña/i), 'Password123')
    await userEvent.click(screen.getByRole('button', { name: /Entrar/i }))

    await waitFor(() => {
      expect(screen.getByRole('navigation', { name: /Navegación principal/i })).toBeInTheDocument()
    })
    expect(fetchMock).toHaveBeenCalledWith('/connect/token', expect.objectContaining({ method: 'POST' }))
  })

  it('muestra el panel de usuarios al admin', async () => {
    localStorage.setItem('nagomi_token', 'test-token')
    sessionStorage.setItem('nagomi_roles', JSON.stringify(['admin']))
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL) => {
      const url = String(input)
      if (url.includes('/api/auth/me')) {
        return json({ id: 'u1', name: 'admin@nagomi.local', email: 'admin@nagomi.local', displayName: null, roles: ['admin'] })
      }
      if (url.includes('/api/admin/users')) {
        return json([
          { id: 'u1', email: 'admin@nagomi.local', displayName: null, roles: ['admin'], isActive: true, createdAt: '2026-08-01T00:00:00Z' },
          { id: 'u2', email: 'op@nagomi.local', displayName: 'Operador', roles: ['default'], isActive: false, createdAt: '2026-08-02T00:00:00Z' },
        ])
      }
      return json([])
    }))

    renderAt('/usuarios')
    expect(await screen.findByText('admin@nagomi.local')).toBeInTheDocument()
    expect(screen.getByText('op@nagomi.local')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Crear/i })).toBeInTheDocument()
    expect(screen.getByText('Desactivado')).toBeInTheDocument()
  })

  it('cierra la sesión al recibir 401 de la API', async () => {
    localStorage.setItem('nagomi_token', 'expired-token')
    sessionStorage.setItem('nagomi_roles', JSON.stringify(['admin']))
    vi.stubGlobal('fetch', vi.fn(() =>
      Promise.resolve(new Response(JSON.stringify({ title: 'Unauthorized' }), { status: 401, headers: { 'Content-Type': 'application/json' } }))))

    renderAt('/trayectos')
    await waitFor(() => {
      expect(localStorage.getItem('nagomi_token')).toBeNull()
    })
  })
})
