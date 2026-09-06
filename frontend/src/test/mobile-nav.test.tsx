import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from '../router'
import App from '../App'

describe('mobile navigation drawer', () => {
  beforeEach(() => {
    // Simulate a phone viewport: the hook branches to the mobile UI.
    Object.defineProperty(window, 'innerWidth', { value: 375, configurable: true, writable: true })
  })

  afterEach(() => {
    Object.defineProperty(window, 'innerWidth', { value: 1024, configurable: true, writable: true })
  })

  it('shows the hamburger on mobile and opens the drawer with the full nav', async () => {
    const user = userEvent.setup()
    render(
      <MemoryRouter initialEntries={['/trayectos']}>
        <App />
      </MemoryRouter>,
    )

    const menuButton = screen.getByRole('button', { name: 'Abrir menú' })
    expect(menuButton).toBeInTheDocument()

    await user.click(menuButton)

    const drawer = screen.getByRole('navigation', { name: 'Navegación móvil' })
    expect(within(drawer).getByRole('link', { name: 'Operación' })).toBeInTheDocument()
    expect(within(drawer).getByRole('link', { name: 'Coordinación' })).toBeInTheDocument()
    expect(within(drawer).getByRole('link', { name: 'Solicitudes' })).toBeInTheDocument()
    expect(within(drawer).getByRole('link', { name: 'Nueva solicitud' })).toBeInTheDocument()
  })

  it('closes the drawer after navigating', async () => {
    const user = userEvent.setup()
    render(
      <MemoryRouter initialEntries={['/trayectos']}>
        <App />
      </MemoryRouter>,
    )

    await user.click(screen.getByRole('button', { name: 'Abrir menú' }))
    const drawer = screen.getByRole('navigation', { name: 'Navegación móvil' })

    await user.click(within(drawer).getByRole('link', { name: 'Coordinación' }))

    expect(screen.queryByRole('navigation', { name: 'Navegación móvil' })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Coordinación' })).toHaveAttribute('aria-current', 'page')
  })

  it('closes the drawer when tapping the scrim', async () => {
    const user = userEvent.setup()
    render(
      <MemoryRouter initialEntries={['/trayectos']}>
        <App />
      </MemoryRouter>,
    )

    await user.click(screen.getByRole('button', { name: 'Abrir menú' }))
    expect(screen.getByRole('navigation', { name: 'Navegación móvil' })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Cerrar menú' }))

    expect(screen.queryByRole('navigation', { name: 'Navegación móvil' })).not.toBeInTheDocument()
  })
})
