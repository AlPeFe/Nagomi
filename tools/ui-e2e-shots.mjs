// Capturas de la instancia REAL (login de verdad contra el backend desplegado).
// Credenciales por argumentos o entorno: nodo tools/ui-e2e-shots.mjs [base] [user] [pass]
import { chromium } from 'playwright-core'
import path from 'node:path'

const BASE = process.argv[2] ?? 'http://localhost:8080'
const USER = process.argv[3] ?? process.env.NAGOMI_E2E_USER
const PASS = process.argv[4] ?? process.env.NAGOMI_E2E_PASS
const OUT = 'C:/Users/alexlocal/projects/Nagomi/.hermes-shots'
const exe = 'C:/Users/alexlocal/AppData/Local/ms-playwright/chromium-1223/chrome-win64/chrome.exe'

if (!USER || !PASS) { console.error('Faltan credenciales'); process.exit(2) }

const browser = await chromium.launch({ executablePath: exe, headless: true })
const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 } })
const page = await ctx.newPage()
const errors = []
page.on('console', (m) => { if (m.type() === 'error' && !m.text().includes('Failed to load resource')) errors.push(m.text().slice(0, 200)) })
page.on('pageerror', (e) => errors.push(`PAGEERROR ${String(e).slice(0, 200)}`))

await page.goto(`${BASE}/login`, { waitUntil: 'domcontentloaded' })
await page.fill('input[autocomplete="username"]', USER)
await page.fill('input[autocomplete="current-password"]', PASS)
await page.click('button[type="submit"]')
await page.waitForURL(/trayectos/, { timeout: 20000 })
await page.waitForTimeout(2500)

const report = []
for (const [route, name] of [['/trayectos', 'real-operacion'], ['/coordinacion', 'real-coordinacion'], ['/solicitudes', 'real-solicitudes'], ['/configuracion', 'real-configuracion']]) {
  await page.goto(`${BASE}${route}`, { waitUntil: 'domcontentloaded' })
  await page.waitForTimeout(2200)
  await page.screenshot({ path: path.join(OUT, `${name}.png`), fullPage: true })
  const text = await page.locator('main').innerText()
  report.push({ route, chars: text.length, hasError: /No se han podido cargar|Error desconocido/.test(text), sample: text.slice(0, 90).replace(/\s+/g, ' ') })
}

const me = await page.evaluate(() => sessionStorage.getItem('nagomi_user'))
console.log(JSON.stringify({ base: BASE, loggedUser: me, consoleErrors: errors, pages: report }, null, 1))
await browser.close()
