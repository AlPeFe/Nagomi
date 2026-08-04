import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from '../router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../App'
import { geocodeAddress } from '../geocode'
import { json } from './fixtures'

vi.mock('../components/IncidentMap', () => ({ IncidentMap: () => <div data-testid="incident-map-mock" /> }))
vi.mock('../geocode', () => ({ geocodeAddress: vi.fn(), reverseGeocode: vi.fn() }))

const emergency = {
  id: 'e1',
  publicId: 'EMG-ABC',
  status: 0,
  reason: 'Atropello en vía pública',
  contactPhone: '600111222',
  incident: { latitude: 41.3874, longitude: 2.1686, address: 'Carrer de Balmes 1', municipality: 'Barcelona' },
  observations: 'Acceso por la entrada principal',
  createdAt: '2026-08-01T09:00:00Z',
  updatedAt: '2026-08-01T09:00:00Z',
}

function renderAt(path: string) { return render(<MemoryRouter initialEntries={[path]}><App /></MemoryRouter>) }

describe('urgencias', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (url.includes('/api/emergency-transports') && init?.method === 'POST')
        return json({ ...emergency, id: 'e2', publicId: 'EMG-NEW' })
      return json([emergency])
    }))
  })

  it('lista las urgencias con su punto de incidencia', async () => {
    renderAt('/urgencias')
    expect(await screen.findByText('EMG-ABC')).toBeInTheDocument()
    expect(screen.getByText('Atropello en vía pública')).toBeInTheDocument()
    expect(screen.getByText(/Carrer de Balmes 1/)).toBeInTheDocument()
  })

  it('requiere marcar el punto de incidencia en el mapa antes de registrar', async () => {
    const user = userEvent.setup()
    renderAt('/urgencias')
    await screen.findByText('EMG-ABC')
    await user.click(screen.getByRole('button', { name: 'Nueva urgencia' }))
    await user.type(screen.getByPlaceholderText('Ej. Atropello, caída, dolor torácico'), 'Dolor torácico')
    await user.click(screen.getByRole('button', { name: 'Registrar urgencia' }))
    expect(await screen.findByText(/Marca el punto de incidencia/)).toBeInTheDocument()
  })

  it('geolocaliza por dirección y registra la urgencia', async () => {
    vi.mocked(geocodeAddress).mockResolvedValue({
      latitude: 41.4, longitude: 2.17, address: 'Carrer de Balmes 1',
    })
    const user = userEvent.setup()
    renderAt('/urgencias')
    await screen.findByText('EMG-ABC')
    await user.click(screen.getByRole('button', { name: 'Nueva urgencia' }))
    await user.type(screen.getByPlaceholderText('Ej. Atropello, caída, dolor torácico'), 'Dolor torácico')
    await user.type(screen.getByPlaceholderText('Buscar dirección…'), 'Carrer de Balmes 1')
    await user.click(screen.getByRole('button', { name: 'Buscar' }))
    expect(await screen.findByText(/Marcado: Carrer de Balmes 1/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Registrar urgencia' }))
    expect(await screen.findByText(/Urgencia EMG-NEW registrada/)).toBeInTheDocument()
    await waitFor(() => {
      const post = vi.mocked(fetch).mock.calls.find(([url, init]) =>
        String(url).includes('/api/emergency-transports') && init?.method === 'POST')
      expect(post).toBeTruthy()
      const body = JSON.parse(String(post?.[1]?.body))
      expect(body.incident.latitude).toBe(41.4)
      expect(body.reason).toBe('Dolor torácico')
    })
  })
})
