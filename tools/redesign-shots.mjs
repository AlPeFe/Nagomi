// Redesign review harness: renders every Nagomi page against mocked API data and
// captures full-page screenshots. No backend needed — /api/** is intercepted.
//   node redesign-shots.mjs [baseUrl] [outDir]
import { chromium } from 'playwright-core'
import fs from 'node:fs'
import path from 'node:path'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5183'
const OUT = process.argv[3] ?? 'C:/Users/alexlocal/projects/Nagomi/.hermes-shots'
const exe = 'C:/Users/alexlocal/AppData/Local/ms-playwright/chromium-1223/chrome-win64/chrome.exe'

fs.mkdirSync(OUT, { recursive: true })

const today = new Date().toISOString().slice(0, 10)
const tmr = new Date(Date.now() + 86400000).toISOString().slice(0, 10)

const ops = (i, status, direction, extra = {}) => ({
  journeyId: `j-${i}`, journeyPublicId: `TRA-2026-00${40 + i}`, requestId: `r-${i % 4}`, requestPublicId: `SOL-2026-00${10 + (i % 4)}`,
  operationalAt: `${i > 4 ? tmr : today}T${String(7 + i).padStart(2, '0')}:30:00+02:00`, pickupTimePending: i === 3,
  patientName: ['Ana Martín', 'Luis Ferrer', 'Marta Ruiz', 'José Peña', 'Carmen Solís', 'Iván Bravo', 'Nuria Cano', 'Pau Serra'][i % 8],
  patientPhone: '600 123 456', origin: ['Hospital La Paz', 'Residencia Los Olivos', 'CAP Gràcia', 'Clínica Teknon'][i % 4],
  destination: ['Residencia Los Olivos', 'Hospital Clínic', 'Hospital La Paz', 'CAP Sants'][i % 4],
  direction, reason: ['Alta hospitalaria', 'Diálisis', 'Consulta externa', 'Rehabilitación'][i % 4],
  requirements: ['Wheelchair', 'Autonomous', 'Stretcher'][i % 3], status,
  provider: i % 3 === 0 ? 'Ambulancias Centro' : 'Flota propia', contractCode: i % 3 === 0 ? 'CTR-MAD-01' : 'SELF',
  providerReference: `EXT-88${i}`, retrievalState: ['Retrieved', 'Pending', 'NotPublished'][i % 3],
  vehicleId: i % 3 === 2 ? undefined : `v-${i % 2}`, vehicleName: i % 3 === 2 ? undefined : ['AMB-01', 'AMB-02'][i % 2],
  driverName: i % 3 === 2 ? undefined : ['Jordi R.', 'Marta S.'][i % 2],
  notes: i % 4 === 1 ? 'Paciente con oxígeno portátil: avisar en recepción del centro de destino.' : undefined,
  externallyModified: i === 2, providerCancelled: status === 'Cancelled', ...extra,
})

const backendJourney = {
  id: 'j-0', transportRequestId: 'r-0', publicId: 'TRA-2026-0040', direction: 'Outbound', serviceDate: today,
  origin: { type: 'HealthcareFacility', name: 'Hospital La Paz', street: 'Castellana 261', municipality: 'Madrid' },
  destination: { type: 'PrivateAddress', name: 'Residencia Los Olivos', street: 'Mayor 8', municipality: 'Alcobendas' },
  requirements: { mobility: 'Wheelchair', requiresOxygen: true, companionRequired: true },
  schedule: { scheduledStartAt: `${today}T09:30:00+02:00`, pickupTimePending: false },
  currentStatus: 'Activated', providerReference: 'EXT-1', retrievalState: 'Retrieved', vehicleId: 'v-0', vehicle: { name: 'AMB-01' },
  driverName: 'Jordi R.', providerVisibleNotes: 'Paciente con oxígeno portátil: avisar en recepción.',
  statusHistory: [{ id: 'e-1', status: 'Scheduled', occurredAt: `${today}T08:00:00+02:00`, actor: 'Nagomi', source: 0 }],
}
const backendRequestDetail = {
  id: 'r-0', publicId: 'SOL-2026-0010', status: 'Active', patient: { firstName: 'Ana', lastName: 'Martín', phone: '600 123 456' },
  reason: { description: 'Alta' }, defaultOrigin: backendJourney.origin, defaultDestination: backendJourney.destination,
  requirements: backendJourney.requirements, contractCode: 'SELF', providerName: 'Flota propia', updatedAt: `${today}T09:12:00Z`,
  journeyRecords: [backendJourney], deliveries: [],
}

const fixtures = {
  'journeys/j-0': backendJourney,
  'transport-requests/r-0': backendRequestDetail,
  'journeys/j-0/statuses': {},
  'operations/journeys': [
    ops(1, 'Scheduled', 'Outbound'), ops(1, 'Return', 'Return'), ops(2, 'PatientOnBoard', 'Outbound'),
    ops(3, 'Activated', 'Outbound'), ops(4, 'Completed', 'Return'), ops(5, 'Cancelled', 'Outbound'),
    ops(6, 'Scheduled', 'Outbound', { retrievalState: 'Dead' }), ops(7, 'EnRouteToDestination', 'Return'),
  ],
  'operations/requests': [0, 1, 2, 3, 4].map((i) => ({
    id: `r-${i}`, publicId: `SOL-2026-00${10 + i}`, status: ['Active', 'Draft', 'Completed', 'Cancelled', 'Active'][i],
    patient: { firstName: ['Ana', 'Luis', 'Marta', 'José', 'Carmen'][i], lastName: ['Martín', 'Ferrer', 'Ruiz', 'Peña', 'Solís'][i], phone: '600 123 456' },
    reason: { description: ['Alta hospitalaria', 'Diálisis', 'Consulta externa', 'Rehabilitación', 'Traslado intercentro'][i] },
    defaultOrigin: { name: 'Hospital La Paz', street: 'Paseo de la Castellana 261', municipality: 'Madrid', type: 1 },
    defaultDestination: { name: 'Residencia Los Olivos', street: 'Calle Mayor 8', municipality: 'Alcobendas', type: 0 },
    requirements: { mobility: 1, requiresOxygen: false, companionRequired: true }, contractCode: 'SELF', providerName: 'Flota propia',
    updatedAt: `${today}T09:12:00Z`, journeyRecords: [],
  })),
  coordination: [0, 1, 2, 3, 4, 5].map((i) => ({
    journeyId: `j-${i}`, journeyPublicId: `TRA-2026-00${40 + i}`, requestId: `r-${i}`, requestPublicId: `SOL-2026-00${10 + i}`,
    direction: i % 2 ? 'Return' : 'Outbound', operationalAt: `${i > 3 ? tmr : today}T${String(8 + i).padStart(2, '0')}:00:00+02:00`,
    patientName: ['Ana Martín', 'Luis Ferrer', 'Marta Ruiz', 'José Peña', 'Carmen Solís', 'Iván Bravo'][i],
    origin: ['Hospital La Paz', 'Residencia Los Olivos', 'CAP Gràcia'][i % 3], destination: ['Hospital Clínic', 'Hospital La Paz', 'CAP Sants'][i % 3],
    status: ['Scheduled', 'PatientOnBoard', 'Activated', 'ArrivedAtOrigin', 'Scheduled', 'Completed'][i],
    vehicleId: i % 2 ? 'v-1' : undefined, vehicleName: i % 2 ? 'AMB-01' : undefined, driverName: i % 3 ? 'Jordi R.' : undefined,
    statusPoints: i === 1 ? [{ id: 'p1', status: 'PatientOnBoard', occurredAt: `${today}T09:05:00+02:00`, latitude: 41.39, longitude: 2.16 }] : [],
  })),
  vehicles: [0, 1, 2].map((i) => ({ id: `v-${i}`, publicId: `VHI-000${i}`, code: `AMB-0${i + 1}`, name: ['AMB-01', 'AMB-02', 'COL-01'][i], externalCode: `EXT-V${i}`, vehicleType: ['Conventional', 'Sva', 'Collective'][i], isActive: i !== 2, createdAt: '2026-01-04T00:00:00Z' })),
  'admin/patients': [0, 1, 2].map((i) => ({ id: `p-${i}`, publicId: `PAC-000${i}`, firstName: ['Ana', 'Luis', 'Marta'][i], lastName: ['Martín', 'Ferrer', 'Ruiz'][i], documentNumber: `48${100000 + i}X`, healthCardNumber: `CAT${1000 + i}`, phone: '600 123 456', isActive: true, createdAt: '2026-02-01T00:00:00Z' })),
  'admin/users': [0, 1].map((i) => ({ id: `u-${i}`, email: i ? 'op@nagomi.local' : 'admin@nagomi.local', displayName: i ? 'Operador Demo' : 'Administrador', roles: [i ? 'default' : 'admin'], isActive: i === 0, createdAt: '2026-01-02T00:00:00Z' })),
  'admin/identity/clients': [{ clientId: 'dispatch-worker', displayName: 'Dispatch worker', isConfidential: true, permissions: ['dispatch'] }],
  'admin/tenant/clients': [0, 1].map((i) => ({ id: `c-${i}`, publicId: `CLI-000${i}`, name: ['Mutua Aseguradora', 'Hospital Clínic'][i], taxId: 'B12345678', contactPerson: 'Marta Soler', phone: '932 000 000', email: 'transporte@clinics.cat', address: 'Carrer de Villarroel 170', isActive: true, createdAt: '2026-01-08T00:00:00Z' })),
  'admin/tenant/capabilities': { publishesRequests: true, executesTransports: true, handlesEmergencies: true },
  queue: [{ name: 'transport-requests', messageCount: 3, consumerCount: 1 }],
  routes: [0, 1].map((i) => ({ id: `rt-${i}`, publicId: `RUT-2026-00000${i + 1}`, serviceDate: today, vehicleId: 'v-1', vehiclePublicId: 'VHI-0001', vehicleName: 'AMB-01', driverName: 'Jordi R.', notes: i ? 'Silla de ruedas plegable' : undefined, status: i ? 'InProgress' : 'Planned', stops: [{ journeyId: `j-${i}`, journeyPublicId: `TRA-2026-004${i}`, order: 1, patientName: 'Ana Martín', origin: 'Residencia Los Olivos', destination: 'Hospital La Paz', status: 'Scheduled' }] })),
  'emergency-transports': [],
  'help-chat/status': { available: false, enabled: false },
  'reference-data/autonomous-communities': [],
}

function fixtureFor(url) {
  const key = Object.keys(fixtures).sort((a, b) => b.length - a.length).find((k) => url.includes(`/api/${k}`))
  if (key) return fixtures[key]
  return []
}

const summary = []
const browser = await chromium.launch({ executablePath: exe, headless: true })

async function shoot(ctx, tag, routes, viewport) {
  const page = await ctx.newPage()
  const errors = []
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text().slice(0, 200)) })
  page.on('pageerror', (e) => errors.push(`PAGEERROR ${String(e).slice(0, 200)}`))
  await page.route('**/api/**', (route) => {
    const url = route.request().url()
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(fixtureFor(url)) })
  })
  for (const [route, name] of routes) {
    errors.length = 0
    await page.goto(`${BASE}${route}`, { waitUntil: 'domcontentloaded' })
    await page.waitForTimeout(900)
    const file = path.join(OUT, `${tag}-${name}.png`)
    await page.screenshot({ path: file, fullPage: true })
    // Also capture the first viewport for density review.
    await page.screenshot({ path: path.join(OUT, `${tag}-${name}-fold.png`) })
    summary.push({ page: `${tag}-${name}`, route, errors: [...errors] })
  }
  await page.close()
}

const authed = await browser.newContext({ viewport: { width: 1440, height: 900 } })
await authed.addInitScript(() => {
  localStorage.setItem('nagomi_token', 'fake-token')
  sessionStorage.setItem('nagomi_roles', JSON.stringify(['admin']))
  sessionStorage.setItem('nagomi_user', 'Administrador')
})

const anon = await browser.newContext({ viewport: { width: 1440, height: 900 } })

await shoot(anon, 'anon', [['/', 'landing'], ['/login', 'login']])
await shoot(authed, 'desk', [
  ['/trayectos', 'operacion'],
  ['/trayectos/j-0', 'detalle-trayecto'],
  ['/historico', 'historico'],
  ['/rutas', 'rutas'],
  ['/urgencias', 'urgencias'],
  ['/solicitudes', 'solicitudes'],
  ['/solicitudes/nueva', 'nueva-solicitud'],
  ['/vehiculos', 'vehiculos'],
  ['/pacientes', 'pacientes'],
  ['/identidad', 'identidad'],
  ['/configuracion', 'configuracion'],
  ['/setup-android', 'app-movil'],
])

const mobile = await browser.newContext({ viewport: { width: 390, height: 844 } })
await mobile.addInitScript(() => {
  localStorage.setItem('nagomi_token', 'fake-token')
  sessionStorage.setItem('nagomi_roles', JSON.stringify(['admin']))
  sessionStorage.setItem('nagomi_user', 'Administrador')
})
await shoot(mobile, 'mob', [['/trayectos', 'operacion'], ['/historico', 'historico']])
// Vista rápida (slide-over) desde la lista de Operación
{
  const page = await authed.newPage()
  await page.route('**/api/**', (r) => r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(fixtureFor(r.request().url())) }))
  await page.goto(`${BASE}/trayectos`, { waitUntil: 'domcontentloaded' })
  await page.waitForTimeout(900)
  await page.locator('.quick-open').first().click()
  await page.waitForTimeout(900)
  await page.screenshot({ path: path.join(OUT, 'desk-detalle-rapido.png'), fullPage: false })
  await page.close()
}

// Menú lateral minimizado (rail de iconos)
{
  const page = await authed.newPage()
  await page.route('**/api/**', (r) => r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(fixtureFor(r.request().url())) }))
  await page.goto(`${BASE}/trayectos`, { waitUntil: 'domcontentloaded' })
  await page.waitForTimeout(800)
  await page.getByRole('button', { name: 'Minimizar el menú' }).click()
  await page.waitForTimeout(500)
  await page.screenshot({ path: path.join(OUT, 'desk-menu-minimizado.png'), fullPage: false })
  await page.close()
}

// Drawer open
{
  const page = await mobile.newPage()
  await page.route('**/api/**', (r) => r.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(fixtureFor(r.request().url())) }))
  await page.goto(`${BASE}/trayectos`, { waitUntil: 'domcontentloaded' })
  await page.waitForTimeout(700)
  await page.getByRole('button', { name: 'Abrir menú' }).click()
  await page.waitForTimeout(450)
  await page.screenshot({ path: path.join(OUT, 'mob-drawer.png'), fullPage: false })
  await page.close()
}

const anonMobile = await browser.newContext({ viewport: { width: 390, height: 844 } })
await shoot(anonMobile, 'mobanon', [['/', 'landing'], ['/login', 'login']])

await browser.close()
console.log(JSON.stringify({ out: OUT, pages: summary }, null, 1))
