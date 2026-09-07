import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from '../router'
import { OnboardingPage } from '../pages/OnboardingPage'

const json = (body: unknown, ok = true) => ({ ok, status: ok ? 200 : 400, json: async () => body })

describe('OnboardingPage', () => {
  beforeEach(() => {
    localStorage.setItem('nagomi_token', 'test-token')
    sessionStorage.setItem('nagomi_roles', JSON.stringify(['admin']))
    sessionStorage.setItem('nagomi_onboarding', '1')
  })

  it('shows the first-run form for the bootstrap account', () => {
    render(
      <MemoryRouter initialEntries={['/onboarding']}>
        <OnboardingPage />
      </MemoryRouter>,
    )
    expect(screen.getByText('Primera configuración')).toBeInTheDocument()
    expect(screen.getByLabelText('Nombre')).toBeInTheDocument()
    expect(screen.getByLabelText('Correo electrónico')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Crear administrador y entrar/ })).toBeInTheDocument()
  })

  it('rejects mismatched passwords', async () => {
    const user = userEvent.setup()
    render(
      <MemoryRouter initialEntries={['/onboarding']}>
        <OnboardingPage />
      </MemoryRouter>,
    )

    await user.type(screen.getByLabelText('Nombre'), 'Jefa')
    await user.type(screen.getByLabelText('Correo electrónico'), 'jefa@empresa.es')
    await user.type(screen.getByLabelText('Usuario'), 'jefa')
    await user.type(screen.getByLabelText('Contraseña (mínimo 12 caracteres)'), 'UnaSegura2026!')
    await user.type(screen.getByLabelText('Repite la contraseña'), 'OtraDistinta2026!')
    await user.click(screen.getByRole('button', { name: /Crear administrador y entrar/ }))

    expect(await screen.findByText('Las contraseñas no coinciden.')).toBeInTheDocument()
  })

  it('submits onboarding and signs in with the new administrator', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = String(input)
      if (url.includes('/api/auth/onboarding')) return Promise.resolve(json({}))
      if (url.includes('/connect/token')) return Promise.resolve(json({ access_token: 'new-token' }))
      if (url.includes('/api/auth/me')) {
        return Promise.resolve(json({
          id: '1', name: 'jefa', email: 'jefa@empresa.es', displayName: 'Jefa',
          roles: ['admin'], onboardingRequired: false,
        }))
      }
      return Promise.resolve(json({}))
    })
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()

    render(
      <MemoryRouter initialEntries={['/onboarding']}>
        <OnboardingPage />
      </MemoryRouter>,
    )

    await user.type(screen.getByLabelText('Nombre'), 'Jefa')
    await user.type(screen.getByLabelText('Correo electrónico'), 'jefa@empresa.es')
    await user.type(screen.getByLabelText('Usuario'), 'jefa')
    await user.type(screen.getByLabelText('Contraseña (mínimo 12 caracteres)'), 'JefaSegura2026!')
    await user.type(screen.getByLabelText('Repite la contraseña'), 'JefaSegura2026!')
    await user.click(screen.getByRole('button', { name: /Crear administrador y entrar/ }))

    // After success the onboarding flag is cleared (new account has no pending onboarding).
    await vi.waitFor(() => {
      expect(sessionStorage.getItem('nagomi_onboarding')).toBeNull()
    })
    const calls = fetchMock.mock.calls.map((call) => String(call[0]))
    expect(calls.some((url) => url.includes('/api/auth/onboarding'))).toBe(true)
  })
})
