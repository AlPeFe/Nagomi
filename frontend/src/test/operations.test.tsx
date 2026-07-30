import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from '../router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../App'
import { api } from '../api'
import { csvForJourneys, localDate } from '../utils'
import { backendJourney, backendRequest, journey, json, operationsRow } from './fixtures'

function renderAt(path: string) { return render(<MemoryRouter initialEntries={[path]}><App /></MemoryRouter>) }

describe('mesa de operaciones', () => {
  beforeEach(() => { vi.stubGlobal('fetch', vi.fn(() => json([operationsRow]))) })

  it('aplica por defecto la ventana de ayer a mañana y trayectos activos', async () => {
    renderAt('/trayectos')
    expect(await screen.findByText('TRA-2026-0042')).toBeInTheDocument()
    const url = String(vi.mocked(fetch).mock.calls[0][0])
    expect(url).toContain(`from=${localDate(-1)}`)
    expect(url).toContain(`to=${localDate(1)}`)
    expect(url).toContain('/api/operations/journeys')
    expect(url).not.toContain('status=active')
  })

  it('presenta hora pendiente e indicadores externos sin documentos sensibles', async () => {
    renderAt('/trayectos')
    expect(await screen.findByText('Hora pendiente')).toBeInTheDocument()
    expect(screen.getByText('Cancelado por proveedor')).toBeInTheDocument()
    expect(screen.getByText('Notificación muerta')).toBeInTheDocument()
    expect(screen.queryByText('12345678Z')).not.toBeInTheDocument()
    expect(screen.queryByText('TARJETA-9988')).not.toBeInTheDocument()
  })

  it('envía todos los filtros operativos a la API', async () => {
    const user = userEvent.setup(); renderAt('/trayectos'); await screen.findByText('TRA-2026-0042')
    await user.type(screen.getByLabelText('Buscar'), 'EXT-887')
    await user.selectOptions(screen.getByLabelText('Dirección'), 'Return')
    await user.click(screen.getByText('Más filtros'))
    await user.type(screen.getByLabelText('Proveedor'), '11111111-1111-1111-1111-111111111111')
    await user.selectOptions(screen.getByLabelText('Recepción'), 'Dead')
    await user.click(screen.getByRole('button', { name: 'Aplicar filtros' }))
    await waitFor(() => expect(vi.mocked(fetch).mock.calls.length).toBeGreaterThan(1))
    const url = String(vi.mocked(fetch).mock.calls.at(-1)?.[0])
    expect(url).toContain('search=EXT-887'); expect(url).toContain('providerId=11111111-1111-1111-1111-111111111111'); expect(url).toContain('retrievalState=Dead')
  })

  it('exporta campos operativos y excluye identificadores sensibles', () => {
    const csv = csvForJourneys([{ ...journey, patientName: 'Ana Martín' }])
    expect(csv).toContain('Ana Martín'); expect(csv).toContain('Hora pendiente')
    expect(csv).not.toContain('Documento'); expect(csv).not.toContain('Tarjeta sanitaria')
  })
})

describe('solicitudes y navegación', () => {
  it('crea el borrador antes de enviar una solicitud puntual', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, _init?: RequestInit) => String(input).includes('/submit/one-off') ? json({ ...backendRequest, recurrence: null }) : json({ ...backendRequest, status: 0, publicId: null, journeyRecords: [] }))
    vi.stubGlobal('fetch', fetchMock)
    await api.submitRequest({ patient: { firstName: 'Ana' }, reason: { code: 'alta', description: 'Alta' }, defaultOrigin: { type: 'HealthcareFacility', name: 'Hospital' }, defaultDestination: { type: 'PrivateAddress', name: 'Casa' }, requirements: journey.requirements }, { kind: 'oneOff', outbound: { appointmentAt: '2026-07-29T10:00:00+02:00', scheduledStartAt: '2026-07-29T09:00:00+02:00', pickupTimePending: false } })
    expect(String(fetchMock.mock.calls[0][0]).endsWith('/api/transport-requests/drafts')).toBe(true)
    expect(String(fetchMock.mock.calls[1][0]).endsWith('/api/transport-requests/request-1/submit/one-off')).toBe(true)
    expect(JSON.parse(String(fetchMock.mock.calls[0][1]?.body))).toMatchObject({ defaultOrigin: { type: 1, name: 'Hospital' }, requirements: { mobility: 1, companionRequired: true } })
    expect(JSON.parse(String(fetchMock.mock.calls[1][1]?.body))).toEqual({ outbound: { appointmentAt: '2026-07-29T10:00:00+02:00', scheduledStartAt: '2026-07-29T09:00:00+02:00', pickupTimePending: false } })
  })

  it('guarda un borrador incompleto y navega a su detalle', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (url.includes('/drafts') && init?.method === 'POST') return json({ ...backendRequest, id: 'draft-1', publicId: null, status: 0, journeyRecords: [], recurrence: null })
      return json({ ...backendRequest, id: 'draft-1', publicId: null, status: 0, journeyRecords: [], recurrence: null })
    }); vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup(); renderAt('/solicitudes/nueva')
    await user.type(screen.getByLabelText('Nombre y apellidos'), 'Ana Martín')
    await user.click(screen.getByRole('button', { name: 'Guardar borrador' }))
    expect(await screen.findByRole('heading', { name: 'Solicitud sin enviar' })).toBeInTheDocument()
    expect(fetchMock.mock.calls.some((call) => String(call[0]).includes('/drafts'))).toBe(true)
    const body = JSON.parse(String(fetchMock.mock.calls[0][1]?.body))
    expect(body.patient).toMatchObject({ firstName: 'Ana', lastName: 'Martín' })
  })

  it('permite configurar una recurrencia con días y vuelta', async () => {
    vi.stubGlobal('fetch', vi.fn(() => json(backendRequest)))
    const user = userEvent.setup(); renderAt('/solicitudes/nueva')
    await user.click(screen.getByRole('button', { name: 'Recurrente' }))
    expect(screen.getByLabelText('Desde *')).toBeRequired()
    await user.click(screen.getByLabelText('Lunes'))
    await user.click(screen.getByLabelText('Incluir vuelta recurrente'))
    expect(screen.getByLabelText('Recogida de vuelta')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Revisar y enviar solicitud' })).toBeEnabled()
  })

  it('navega del trayecto a la solicitud padre y muestra sus trayectos activos', async () => {
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL) => String(input).includes('/journeys/') ? json(backendJourney) : json(backendRequest)))
    const user = userEvent.setup(); renderAt('/trayectos/journey-1')
    await user.click(await screen.findByText(/Ver solicitud SOL-2026-0019/))
    expect(await screen.findByRole('heading', { name: 'SOL-2026-0019' })).toBeInTheDocument()
    expect(screen.getByText('Los activos se muestran por defecto.')).toBeInTheDocument()
  })

  it('previsualiza recurrencia antes de ofrecer su confirmación', async () => {
    vi.stubGlobal('fetch', vi.fn((input: RequestInfo | URL) => String(input).includes('/preview') ? json({ additions: 4, cancellations: 2, exceptions: 1 }) : json(backendRequest)))
    const user = userEvent.setup(); renderAt('/solicitudes/request-1')
    await user.click(await screen.findByRole('button', { name: 'Previsualizar impacto' }))
    expect(await screen.findByText('+4 altas')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Conservar excepciones' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Sobrescribir excepciones' })).toBeInTheDocument()
  })
})
