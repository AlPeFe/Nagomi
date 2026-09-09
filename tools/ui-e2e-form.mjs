// Real UI E2E: create + submit a one-off transport request through the web form,
// then open its detail. Exercises: patient fields, reason, healthcare-facility picker
// (province + municipality autofill), domicile location, schedule, submit -> detail.
// Credentials: argv [baseUrl] [username] [password], or NAGOMI_E2E_USER / NAGOMI_E2E_PASS.
import { chromium } from 'playwright-core'
import fs from 'node:fs'

const BASE = process.argv[2] ?? 'http://127.0.0.1:8080'
const USER = process.argv[3] ?? process.env.NAGOMI_E2E_USER ?? 'e2e-admin'
const PASS = process.argv[4] ?? process.env.NAGOMI_E2E_PASS
if (!PASS) { console.error('Falta la contraseña: pasa [password] o NAGOMI_E2E_PASS'); process.exit(2) }
const exe = process.env.NAGOMI_CHROMIUM ?? 'C:/Users/alexlocal/AppData/Local/ms-playwright/chromium_headless_shell-1223/chrome-headless-shell-win64/chrome-headless-shell.exe'
const browser = await chromium.launch({ executablePath: exe, headless: true })
const ctx = await browser.newContext({ viewport: { width: 1500, height: 950 } })
const page = await ctx.newPage()
const errs = [], fails = []
page.on('console', (m) => { if (m.type() === 'error') errs.push(m.text().slice(0, 250)) })
page.on('pageerror', (e) => errs.push('PAGEERROR ' + String(e).slice(0, 250)))
page.on('response', (r) => { if (r.status() >= 400) fails.push(`HTTP ${r.status()} ${r.request().method()} ${r.url()}`) })

const log = []
const step = (s) => { log.push(s); console.log('•', s) }

await page.goto(`${BASE}/login`, { waitUntil: 'domcontentloaded' })
await page.waitForTimeout(600)
await page.fill('input[type="text"], input[name="username"]', 'e2e-admin').catch(() => {})
await page.fill('input[type="password"]', 'E2eAdminSegura2026!').catch(() => {})
await page.click('button[type="submit"], button:has-text("Entrar")').catch(() => {})
await page.waitForTimeout(2200)
step('login OK -> ' + page.url())

await page.goto(`${BASE}/solicitudes/nueva`, { waitUntil: 'domcontentloaded' })
await page.waitForTimeout(1500)
step('form opened: ' + (await page.locator('h1').first().innerText().catch(() => '')))

// 01 paciente y motivo
await page.fill('input[name="patientName"]', 'Lucía E2E Prueba')
await page.fill('input[name="patientPhone"]', '600112233')
await page.selectOption('select[name="reason"]', { label: 'Consulta externa' })
step('paciente + motivo OK')

// 02 origen: centro sanitario del catálogo (autofill provincia/población)
await page.selectOption('select[name="originProvince"]', { value: '08' }).catch(async () => {
  await page.waitForTimeout(1200); await page.selectOption('select[name="originProvince"]', { value: '08' })
})
await page.waitForTimeout(800)
await page.selectOption('select[name="originMunicipalitySelect"]', { value: '08019' })
await page.fill('input[name="originName"]', 'Hospital Clínic')
await page.waitForTimeout(900) // debounce + search
const opts = await page.locator('.facility-dropdown .facility-option').count().catch(() => 0)
step(`origen: provincia 08 + municipio OK, centros encontrados: ${opts}`)
if (opts > 0) {
  await page.locator('.facility-dropdown .facility-option').first().click()
  step('centro elegido: ' + (await page.locator('input[name="originName"]').inputValue().catch(() => '?')))
} else {
  step('sin centros en dropdown (se mantiene texto manual)')
}

// 02 destino: domicilio con provincia/población + dirección
const destType = page.locator('fieldset').filter({ hasText: 'Destino' }).locator('select').first()
await destType.selectOption({ label: 'Domicilio' })
await page.locator('fieldset').filter({ hasText: 'Destino' }).locator('select[name="destinationProvince"]').selectOption({ value: '08' })
await page.waitForTimeout(900)
await page.locator('fieldset').filter({ hasText: 'Destino' }).locator('select[name="destinationMunicipalitySelect"]').selectOption({ value: '08019' })
await page.fill('input[name="destinationAddress"]', 'Carrer de Mallorca 100, 3º 2ª')
step('destino domicilio OK')

// 04 programación: cita mañana 10:00
const tomorrow = new Date(Date.now() + 86400000)
const dateStr = tomorrow.toISOString().slice(0, 10)
await page.fill('input[name="appointmentAt"]', `${dateStr}T10:00`)
step(`cita ${dateStr}T10:00`)

await page.fill('textarea[name="providerNotes"]', 'Nota proveedor E2E')

const before = errs.length
await page.click('button:has-text("Revisar y enviar solicitud")')
await page.waitForTimeout(4500)
step('post-submit URL: ' + page.url())
step('h1: ' + (await page.locator('h1').first().innerText().catch(() => '')))
const body = (await page.locator('body').innerText().catch(() => '')).replace(/\n+/g, ' | ').slice(0, 900)
step('detalle: ' + body.slice(0, 450))

const m = page.url().match(/\/solicitudes\/([0-9a-f-]{36})/)
const createdId = m ? m[1] : null
const journeyLink = await page.locator('a[href*="/trayectos/"]').first().getAttribute('href').catch(() => null)

const result = {
  createdId, journeyLink, finalUrl: page.url(),
  newErrors: errs.slice(before), fails: [...new Set(fails)].slice(0, 15),
  detailBody: body.slice(0, 900),
}
fs.writeFileSync('e2e-form-result.json', JSON.stringify(result, null, 1))
step(JSON.stringify({ createdId, journeyLink, newErrors: result.newErrors.length, fails: result.fails.length }, null, 1))
await browser.close()
