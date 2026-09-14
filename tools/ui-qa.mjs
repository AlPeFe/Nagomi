// QA de navegador por escenarios: navega, interactúa y falla si algo se rompe.
// API simulada (page.route), sin backend.  node tools/ui-qa.mjs [baseUrl]
import { chromium } from 'playwright-core'

const BASE = process.argv[2] ?? 'http://localhost:5183'
const exe = 'C:/Users/alexlocal/AppData/Local/ms-playwright/chromium-1223/chrome-win64/chrome.exe'
const OUT = 'C:/Users/alexlocal/projects/Nagomi/.hermes-shots'

const today = new Date().toISOString().slice(0, 10)
const tmr = new Date(Date.now() + 86400000).toISOString().slice(0, 10)

const journeys = [0, 1, 2, 3].map((i) => ({
  journeyId: `j-${i}`, journeyPublicId: `TRA-2026-00${40 + i}`, requestId: `r-${i % 2}`, requestPublicId: `SOL-2026-00${10 + (i % 2)}`,
  operationalAt: `${i > 2 ? tmr : today}T${String(8 + i).padStart(2, '0')}:30:00+02:00`, pickupTimePending: i === 3,
  patientName: `Paciente ${i}`, patientPhone: '600 123 456', origin: `Origen ${i}`, destination: `Destino ${i}`,
  direction: i % 2 ? 'Return' : 'Outbound', reason: 'Diálisis', requirements: 'Wheelchair',
  status: ['Scheduled', 'PatientOnBoard', 'Completed', 'Cancelled'][i], provider: 'Flota propia', contractCode: 'SELF',
  retrievalState: 'Retrieved', externallyModified: false,
}))

const backendJourney = {
  id: 'j-0', transportRequestId: 'r-0', publicId: 'TRA-2026-0040', direction: 'Outbound', serviceDate: today,
  origin: { type: 'HealthcareFacility', name: 'Hospital La Paz', street: 'Castellana 261', municipality: 'Madrid' },
  destination: { type: 'PrivateAddress', name: 'Residencia Los Olivos', street: 'Mayor 8', municipality: 'Alcobendas' },
  requirements: { mobility: 'Wheelchair', requiresOxygen: false, companionRequired: true },
  schedule: { scheduledStartAt: `${today}T09:30:00+02:00`, pickupTimePending: false },
  currentStatus: 'Scheduled', providerReference: 'EXT-1', retrievalState: 'Retrieved', vehicleId: 'v-0', vehicle: { name: 'AMB-01' },
  driverName: 'Jordi R.', statusHistory: [{ id: 'e-1', status: 'Scheduled', occurredAt: `${today}T09:00:00+02:00`, actor: 'Nagomi', source: 0 }],
}
const backendRequest = {
  id: 'r-0', publicId: 'SOL-2026-0010', status: 'Active', patient: { firstName: 'Ana', lastName: 'Martín', phone: '600 123 456' },
  reason: { description: 'Alta hospitalaria' }, defaultOrigin: backendJourney.origin, defaultDestination: backendJourney.destination,
  requirements: backendJourney.requirements, contractCode: 'SELF', providerName: 'Flota propia', updatedAt: `${today}T09:12:00Z`,
  recurrence: { startDate: today, endDate: tmr, utcOffset: '02:00:00', weekdaySchedules: [{ dayOfWeek: 1, outboundAppointmentTime: '10:00:00' }] },
  journeyRecords: [backendJourney], deliveries: [{ id: 'd-1', state: 'Retrieved', createdAt: `${today}T09:00:00Z`, attempts: 1 }],
}

const fixtures = {
  'operations/journeys': journeys,
  'operations/requests': [backendRequest],
  coordination: [{
    journeyId: 'j-0', journeyPublicId: 'TRA-2026-0040', requestId: 'r-0', requestPublicId: 'SOL-2026-0010',
    direction: 'Outbound', operationalAt: `${today}T09:00:00+02:00`, patientName: 'Ana Martín', origin: 'Hospital La Paz',
    destination: 'Hospital Clínic', status: 'Scheduled', statusPoints: [],
  }],
  'journeys/j-0': backendJourney,
  'transport-requests/r-0': backendRequest,
  'transport-requests/r-0/recurrence/preview': { additions: 4, cancellations: 1, exceptions: 2 },
  vehicles: [{ id: 'v-0', publicId: 'VHI-0000', code: 'AMB-01', name: 'AMB-01', vehicleType: 'Conventional', isActive: true, createdAt: `${today}T00:00:00Z` }],
  'admin/patients': [{ id: 'p-0', publicId: 'PAC-0000', firstName: 'Ana', lastName: 'Martín', phone: '600123456', isActive: true, createdAt: `${today}T00:00:00Z` }],
  'admin/users': [{ id: 'u-0', email: 'admin@nagomi.local', displayName: 'Admin', roles: ['admin'], isActive: true, createdAt: `${today}T00:00:00Z` }],
  'admin/identity/clients': [{ clientId: 'dispatch-worker', displayName: 'Dispatch', isConfidential: true, permissions: [] }],
  'admin/tenant/clients': [{ id: 'c-0', publicId: 'CLI-0000', name: 'Mutua', isActive: true, createdAt: `${today}T00:00:00Z` }],
  'admin/tenant/capabilities': { publishesRequests: true, executesTransports: true, handlesEmergencies: true },
  queue: [{ providerId: 'prov-0', providerName: 'Ambulancias Centro', providerCode: 'AMB', queueName: 'q.provider.amb', messages: 2, consumers: 1 }],
  routes: [{ id: 'rt-0', publicId: 'RUT-2026-000001', serviceDate: today, vehicleId: 'v-0', vehicleName: 'AMB-01', driverName: 'Jordi R.', status: 'Planned', stops: [{ journeyId: 'j-0', journeyPublicId: 'TRA-2026-0040', order: 1, patientName: 'Ana Martín', origin: 'A', destination: 'B', status: 'Scheduled' }] }],
  'emergency-transports': [],
  'help-chat/status': { available: true, enabled: true },
  'help-chat/messages': { reply: 'Respuesta del asistente' },
  'reference-data/autonomous-communities': [],
}

function fixtureFor(url) {
  const key = Object.keys(fixtures).sort((a, b) => b.length - a.length).find((k) => url.includes(`/api/${k}`))
  return key ? fixtures[key] : []
}

const results = []
const fail = (n, d) => results.push({ check: n, ok: false, detail: d })
const pass = (n) => results.push({ check: n, ok: true })

const browser = await chromium.launch({ executablePath: exe, headless: true })

async function ctxFor(roles) {
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 } })
  const consoleErrors = []
  ctx.on('console', (m) => {
    // "Failed to load resource" es el log del navegador para cualquier respuesta
    // HTTP de error: se espera en los escenarios de 401/500, no es un bug de la app.
    if (m.type() === 'error' && !m.text().includes('Failed to load resource')) consoleErrors.push(m.text().slice(0, 180))
  })
  ctx.on('pageerror', (e) => consoleErrors.push(`PAGEERROR ${String(e).slice(0, 180)}`))
  await ctx.addInitScript((r) => {
    localStorage.setItem('nagomi_token', 'fake-token')
    sessionStorage.setItem('nagomi_roles', JSON.stringify(r))
    sessionStorage.setItem('nagomi_user', 'Tester')
  }, roles)
  return { ctx, consoleErrors }
}

async function mock(page, overrides = {}) {
  const requests = []
  page.on('request', (r) => { if (r.url().includes('/api/')) requests.push(r.url()) })
  await page.route('**/api/**', (route) => {
    const url = route.request().url()
    const key = Object.keys(overrides).find((k) => url.includes(`/api/${k}`))
    if (key) return route.fulfill({ status: overrides[key].status, contentType: 'application/json', body: JSON.stringify(overrides[key].body ?? {}) })
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(fixtureFor(url)) })
  })
  return requests
}

// ---------------------------------------------------------------- scenarios
{
  // 1. Anon redirige a login
  const ctx = await browser.newContext()
  const page = await ctx.newPage()
  await mock(page)
  await page.goto(`${BASE}/trayectos`, { waitUntil: 'domcontentloaded' })
  await page.waitForTimeout(600)
  page.url().includes('/login') ? pass('anon /trayectos → /login') : fail('anon /trayectos → /login', page.url())
  await ctx.close()
}

{
  // 2. No-admin: páginas admin redirigen y no aparecen en el menú
  const { ctx, consoleErrors } = await ctxFor(['default'])
  const page = await ctx.newPage()
  await mock(page)
  for (const route of ['/vehiculos', '/identidad', '/configuracion']) {
    await page.goto(`${BASE}${route}`, { waitUntil: 'domcontentloaded' })
    await page.waitForTimeout(500)
    page.url().includes('/trayectos') ? pass(`no-admin ${route} → /trayectos`) : fail(`no-admin ${route} → /trayectos`, page.url())
  }
  const leaked = await page.locator('.sidebar-nav a', { hasText: /Pacientes|Vehículos|Identidad|Configuración/ }).count()
  leaked === 0 ? pass('no-admin: menú sin secciones admin') : fail('no-admin: menú sin secciones admin', `${leaked} enlaces`)
  consoleErrors.length ? fail('no-admin sin errores de consola', consoleErrors.join(' | ')) : pass('no-admin sin errores de consola')
  await ctx.close()
}

{
  // 3. Admin: páginas + detalle de trayecto + solicitud con recurrencia
  const { ctx, consoleErrors } = await ctxFor(['admin'])
  const page = await ctx.newPage()
  await mock(page)
  for (const [route, marker] of [['/trayectos', 'TRA-2026-0040'], ['/trayectos/j-0', 'TRA-2026-0040'], ['/solicitudes', 'SOL-2026-0010'], ['/solicitudes/r-0', 'TRA-2026-0040'], ['/historico', 'Histórico'], ['/rutas', 'RUT-2026-000001'], ['/vehiculos', 'AMB-01'], ['/pacientes', 'Ana'], ['/identidad', 'admin@nagomi.local'], ['/configuracion', 'Capacidades'], ['/urgencias', 'urgencias']]) {
    await page.goto(`${BASE}${route}`, { waitUntil: 'domcontentloaded' })
    await page.waitForTimeout(700)
    const body = await page.locator('body').innerText()
    body.includes(marker) ? pass(`render ${route}`) : fail(`render ${route}`, `no aparece "${marker}"`)
    const h1 = await page.locator('h1').count()
    h1 === 1 ? pass(`un solo h1 en ${route}`) : fail(`un solo h1 en ${route}`, `${h1} h1`)
  }
  // recurrencia: previsualizar impacto
  await page.goto(`${BASE}/solicitudes/r-0`, { waitUntil: 'domcontentloaded' })
  await page.waitForTimeout(700)
  const preview = page.getByRole('button', { name: 'Previsualizar impacto' })
  if (await preview.count()) {
    await preview.click()
    await page.waitForTimeout(600)
    const txt = await page.locator('body').innerText()
    txt.includes('+4 altas') ? pass('recurrencia: preview muestra impacto') : fail('recurrencia: preview muestra impacto', 'sin impacto')
  } else fail('recurrencia: preview muestra impacto', 'botón ausente')
  consoleErrors.length ? fail('admin sin errores de consola', [...new Set(consoleErrors)].join(' | ')) : pass('admin sin errores de consola')
  await ctx.close()
}

{
  // 4. Filtros: "Más filtros" y búsqueda enviada a la API
  const { ctx } = await ctxFor(['admin'])
  const page = await ctx.newPage()
  const requests = await mock(page)
  await page.goto(`${BASE}/trayectos`, { waitUntil: 'domcontentloaded' })
  await page.waitForTimeout(700)
  await page.getByText('Más filtros').click()
  await page.waitForTimeout(300)
  const gridVisible = await page.locator('.filter-grid').isVisible()
  gridVisible ? pass('filtros: "Más filtros" despliega') : fail('filtros: "Más filtros" despliega', 'oculto')
  await page.fill('input[type="search"]', 'Martín')
  await page.getByRole('button', { name: 'Aplicar filtros' }).click()
  await page.waitForTimeout(700)
  requests.some((u) => u.includes('search=Mart')) ? pass('filtros: search llega a la API') : fail('filtros: search llega a la API', requests.join(' '))
  // La mesa diaria ya no filtra por estado ni dirección.
  // Playwright usa getByLabel (getByLabelText es de Testing Library).
  const hasEstado = await page.getByLabel('Estado', { exact: true }).count()
  const hasDireccion = await page.getByLabel('Dirección', { exact: true }).count()
  hasEstado === 0 && hasDireccion === 0
    ? pass('operación: sin filtros de estado ni dirección')
    : fail('operación: sin filtros de estado ni dirección', `estado=${hasEstado} dirección=${hasDireccion}`)
  // exportar CSV genera descarga
  const dl = page.waitForEvent('download', { timeout: 5000 }).catch(() => null)
  await page.getByRole('button', { name: /Exportar CSV/ }).click()
  const download = await dl
  download ? pass('exportar CSV descarga fichero') : fail('exportar CSV descarga fichero', 'sin evento download')
  await ctx.close()
}

{
  // 5. 401 → limpia sesión y va a login
  const ctx = await browser.newContext()
  const page = await ctx.newPage()
  await mock(page, { 'operations/journeys': { status: 401, body: {} } })
  // La sesión se siembra DESPUÉS de cargar: con addInitScript se volvía a crear en
  // cada navegación y el redirect a /login no podía limpiarla (falso positivo).
  await page.goto(`${BASE}/login`, { waitUntil: 'domcontentloaded' })
  await page.evaluate(() => {
    localStorage.setItem('nagomi_token', 'expired')
    sessionStorage.setItem('nagomi_roles', JSON.stringify(['admin']))
  })
  await page.goto(`${BASE}/trayectos`, { waitUntil: 'domcontentloaded' })
  await page.waitForTimeout(900)
  const token = await page.evaluate(() => localStorage.getItem('nagomi_token'))
  page.url().includes('/login') && token === null ? pass('401 → /login y token limpio') : fail('401 → /login y token limpio', `${page.url()} token=${token}`)
  await ctx.close()
}

{
  // 6. Error de API → ErrorState con Reintentar, sin crash
  const { ctx, consoleErrors } = await ctxFor(['admin'])
  const page = await ctx.newPage()
  await mock(page, { 'operations/journeys': { status: 500, body: { detail: 'Fallo simulado del servidor' } } })
  await page.goto(`${BASE}/trayectos`, { waitUntil: 'domcontentloaded' })
  await page.waitForTimeout(900)
  const txt = await page.locator('body').innerText()
  txt.includes('Fallo simulado del servidor') ? pass('500 → error visible con detalle') : fail('500 → error visible con detalle', txt.slice(0, 120))
  const retry = await page.getByRole('button', { name: 'Reintentar' }).count()
  retry ? pass('500 → botón Reintentar') : fail('500 → botón Reintentar', 'ausente')
  consoleErrors.length ? fail('500 sin errores de consola', consoleErrors.join(' | ')) : pass('500 sin errores de consola')
  await ctx.close()
}

{
  // 7. Logout desde la sidebar
  const { ctx } = await ctxFor(['admin'])
  const page = await ctx.newPage()
  await mock(page)
  await page.goto(`${BASE}/trayectos`, { waitUntil: 'domcontentloaded' })
  await page.waitForTimeout(600)
  await page.getByRole('button', { name: 'Salir' }).click()
  await page.waitForTimeout(600)
  const token = await page.evaluate(() => localStorage.getItem('nagomi_token'))
  page.url().includes('/login') && token === null ? pass('logout → /login y token limpio') : fail('logout → /login y token limpio', `${page.url()} token=${token}`)
  await ctx.close()
}

{
  // 7b. Histórico: arranca vacío y solo consulta al buscar
  const { ctx } = await ctxFor(['admin'])
  const page = await ctx.newPage()
  const requests = await mock(page)
  await page.goto(`${BASE}/historico`, { waitUntil: 'domcontentloaded' })
  await page.waitForTimeout(900)
  const antes = requests.filter((u) => u.includes('/operations/journeys')).length
  const vacio = (await page.locator('body').innerText()).includes('Aún no hay resultados')
  antes === 0 && vacio ? pass('histórico: arranca vacío y no consulta') : fail('histórico: arranca vacío y no consulta', `${antes} consultas, vacío=${vacio}`)
  await page.getByRole('button', { name: 'Buscar' }).click()
  await page.waitForTimeout(900)
  const despues = requests.filter((u) => u.includes('/operations/journeys')).length
  const conFilas = (await page.locator('body').innerText()).includes('TRA-2026-0040')
  despues > 0 && conFilas ? pass('histórico: busca al aplicar filtros') : fail('histórico: busca al aplicar filtros', `${despues} consultas, filas=${conFilas}`)
  await ctx.close()
}

{
  // 8. Móvil: drawer, navegación, y sin desbordamiento horizontal
  const ctx = await browser.newContext({ viewport: { width: 390, height: 844 } })
  const page = await ctx.newPage()
  await ctx.addInitScript(() => {
    localStorage.setItem('nagomi_token', 't'); sessionStorage.setItem('nagomi_roles', JSON.stringify(['admin']))
  })
  await mock(page)
  await page.goto(`${BASE}/trayectos`, { waitUntil: 'domcontentloaded' })
  await page.waitForTimeout(700)
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
  overflow <= 1 ? pass('móvil sin scroll horizontal') : fail('móvil sin scroll horizontal', `${overflow}px`)
  await page.getByRole('button', { name: 'Abrir menú' }).click()
  await page.waitForTimeout(400)
  await page.locator('.mobile-drawer a', { hasText: 'Rutas' }).click()
  await page.waitForTimeout(700)
  const drawerGone = (await page.locator('.mobile-drawer').count()) === 0
  const onCoord = page.url().includes('/rutas')
  drawerGone && onCoord ? pass('móvil: navegar cierra el drawer') : fail('móvil: navegar cierra el drawer', `${page.url()} drawer=${!drawerGone}`)
  await ctx.close()
}

{
  // 9. Chat de ayuda: abre, envía y pinta la respuesta
  const { ctx, consoleErrors } = await ctxFor(['admin'])
  const page = await ctx.newPage()
  await mock(page)
  await page.goto(`${BASE}/trayectos`, { waitUntil: 'domcontentloaded' })
  await page.waitForTimeout(800)
  await page.locator('.help-chat-fab').click()
  await page.waitForTimeout(300)
  const input = page.locator('.help-chat-form input')
  if (await input.count()) {
    await input.fill('¿Cómo asigno un vehículo?')
    await page.getByRole('button', { name: 'Enviar' }).click()
    await page.waitForTimeout(900)
    const txt = await page.locator('.help-chat-body').innerText()
    txt.includes('Respuesta del asistente') ? pass('chat: muestra respuesta') : fail('chat: muestra respuesta', txt.slice(0, 120))
  } else fail('chat: muestra respuesta', 'input no encontrado')
  consoleErrors.length ? fail('chat sin errores de consola', consoleErrors.join(' | ')) : pass('chat sin errores de consola')
  await ctx.close()
}

await browser.close()
const failed = results.filter((r) => !r.ok)
console.log(`\n${results.length - failed.length}/${results.length} OK`)
failed.forEach((f) => console.log(`FAIL  ${f.check}  → ${f.detail}`))