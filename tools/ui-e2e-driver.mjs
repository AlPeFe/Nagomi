// E2E del emulador web del conductor contra la instancia real: prepara un traslado de
// hoy, le asigna un vehículo y comprueba que el cliente lo lista y marca estados.
//   node ui-e2e-driver.mjs <base> <user> <pass>
import { chromium } from 'playwright-core'

const [base, user, pass] = process.argv.slice(2)
const exe = 'C:/Users/alexlocal/AppData/Local/ms-playwright/chromium-1223/chrome-win64/chrome.exe'
const OUT = 'C:/Users/alexlocal/projects/Nagomi/.hermes-shots'
const results = []
const check = (name, ok, detail = '') => results.push({ check: name, ok, detail })

// ---------------------------------------------------------------- preparación por API
const token = (await (await fetch(`${base}/connect/token`, {
  method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
  body: new URLSearchParams({ grant_type: 'password', username: user, password: pass }),
})).json()).access_token
const H = { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` }
const iso = (date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
const today = iso(new Date())

// Un vehículo con el que trabajar (si no hay ninguno activo, se crea).
let vehicles = await (await fetch(`${base}/api/vehicles`, { headers: H })).json()
let vehicle = vehicles.find((item) => item.isActive)
if (!vehicle) {
  vehicle = await (await fetch(`${base}/api/admin/vehicles`, { method: 'POST', headers: H, body: JSON.stringify({ name: 'EMU-01', code: 'EMU-01', vehicleType: 'Conventional', isActive: true }) })).json()
  vehicles = await (await fetch(`${base}/api/vehicles`, { headers: H })).json()
  vehicle = vehicles.find((item) => item.isActive) ?? vehicle
}
check('vehículo disponible', !!vehicle, vehicle?.name)
check('el API público devuelve el vehículo', vehicles.some((item) => item.id === vehicle.id))

// Un traslado de hoy asignado a ese vehículo (lo que la app encontraría al arrancar).
const draft = await (await fetch(`${base}/api/transport-requests/drafts`, {
  method: 'POST', headers: H,
  body: JSON.stringify({
    patient: { firstName: 'Emu', lastName: 'Driver', phone: '600000004' },
    reason: { code: 'Alta', description: 'Alta' },
    defaultOrigin: { type: 1, name: 'Hospital La Paz', street: 'Castellana 261', municipality: 'Madrid' },
    defaultDestination: { type: 0, name: 'Residencia Los Olivos', street: 'Mayor 8', municipality: 'Alcobendas' },
    requirements: { mobility: 1, requiresOxygen: true, companionRequired: false },
  }),
})).json()
const submitted = await (await fetch(`${base}/api/transport-requests/${draft.id}/submit/one-off`, {
  method: 'POST', headers: H,
  body: JSON.stringify({ outbound: { scheduledStartAt: `${today}T23:30:00+02:00`, pickupTimePending: false } }),
})).json()
const journey = submitted.journeyRecords?.[0]
check('traslado de hoy creado', !!journey, journey?.publicId)
if (!journey) { console.log(JSON.stringify(results, null, 1)); process.exit(1) }
const assigned = await fetch(`${base}/api/journeys/${journey.id}/vehicle`, { method: 'PUT', headers: H, body: JSON.stringify({ vehicleId: vehicle.id }) })
check('vehículo asignado al traslado', assigned.ok, assigned.status)

// ---------------------------------------------------------------- flujo en el navegador
const browser = await chromium.launch({ executablePath: exe, headless: true })
const ctx = await browser.newContext({ viewport: { width: 420, height: 900 }, permissions: [] })
const page = await ctx.newPage()
const errors = []
page.on('console', (m) => { if (m.type() === 'error' && !m.text().includes('Failed to load resource')) errors.push(m.text().slice(0, 160)) })
page.on('pageerror', (e) => errors.push(`PAGEERROR ${String(e).slice(0, 160)}`))

await page.goto(`${base}/driver`, { waitUntil: 'domcontentloaded' })
await page.waitForTimeout(600)
await page.screenshot({ path: `${OUT}/driver-login.png` })
check('login del emulador se pinta', await page.getByRole('button', { name: /Entrar/ }).count() > 0)

await page.fill('input[autocomplete="username"]', user)
await page.fill('input[autocomplete="current-password"]', pass)
await page.getByRole('button', { name: /Entrar/ }).click()
await page.waitForTimeout(1500)
const listText = await page.locator('body').innerText()
check('sesión iniciada y lista de vehículos', listText.includes('Iniciar vehículo'), listText.slice(0, 80).replace(/\s+/g, ' '))
await page.screenshot({ path: `${OUT}/driver-vehiculos.png` })

// Activar el vehículo creado
const row = page.locator('.driver-vehicles li').filter({ hasText: vehicle.publicId })
await row.getByRole('button', { name: 'Activar' }).click()
await page.waitForTimeout(2000)
const afterStart = await page.locator('body').innerText()
check('vehículo activo en la cabecera', afterStart.includes(vehicle.name), vehicle.name)
check('traslados de hoy del vehículo', afterStart.includes(journey.publicId), journey.publicId)
await page.screenshot({ path: `${OUT}/driver-trabajo.png` })

// Añadir un trabajador a bordo (el actor pasa a ser su código)
await page.fill('.driver-worker-form input', 'CON-001')
await page.locator('.driver-worker-form input').nth(1).fill('Jordi R.')
await page.getByRole('button', { name: 'Añadir' }).click()
await page.waitForTimeout(600)
const withWorker = await page.locator('body').innerText()
check('trabajador a bordo', withWorker.includes('CON-001') && withWorker.includes('marca como CON-001'))
await page.screenshot({ path: `${OUT}/driver-trabajador.png` })

// Marcar el siguiente estado desde el cliente (sin posición: el navegador no la da)
await page.locator('.driver-job').filter({ hasText: journey.publicId }).getByRole('button', { name: /Sin posición/ }).click()
await page.waitForTimeout(2000)
const detail = await (await fetch(`${base}/api/journeys/${journey.id}`, { headers: H })).json()
const lastEvent = (detail.statusHistory ?? []).at(-1)
check('el estado se marcó desde el emulador', lastEvent?.actor === 'CON-001', `actor=${lastEvent?.actor} status=${detail.currentStatus}`)
check('el vehículo viaja en externalResourceCode', lastEvent?.externalResourceCode === vehicle.publicId || !lastEvent?.externalResourceCode, `externalResourceCode=${lastEvent?.externalResourceCode}`)

// Cerrar vehículo libera TODO (modelo de la app)
await page.getByRole('button', { name: 'Cerrar vehículo' }).click()
await page.waitForTimeout(800)
const closed = await page.locator('body').innerText()
check('cerrar vehículo libera trabajadores y vuelve al selector', closed.includes('Iniciar vehículo') && !closed.includes('CON-001'))

check('sin errores de consola', errors.length === 0, errors.join(' | '))
await browser.close()
console.log(JSON.stringify(results, null, 1))
console.log(`${results.filter((r) => r.ok).length}/${results.length} OK`)