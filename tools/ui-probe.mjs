// Dev probe: real-UI walkthrough of every Nagomi page (login + routes), capturing
// console errors, failed HTTP requests and visible content. Run from tools/:
//   node ui-probe.mjs [baseUrl] [username] [password]
import { chromium } from 'playwright-core'
import fs from 'node:fs'

const BASE = process.argv[2] ?? 'http://127.0.0.1:8080'
const USER = process.argv[3] ?? 'e2e-admin'
const PASS = process.argv[4] ?? 'E2eAdminSegura2026!'

const ROUTES = [
  ['/', 'landing (anon redirect)'],
  ['/trayectos', 'Operación'],
  ['/trayectos/JRN-2026-000001', 'Detalle trayecto'],
  ['/coordinacion', 'Coordinación'],
  ['/rutas', 'Rutas colectivas'],
  ['/setup-android', 'App móvil QR'],
  ['/solicitudes', 'Solicitudes'],
  ['/solicitudes/nueva', 'Nueva solicitud'],
  ['/solicitudes/REQ-2026-000001', 'Detalle solicitud'],
  ['/urgencias', 'Urgencias'],
  ['/vehiculos', 'Vehículos (admin)'],
  ['/pacientes', 'Pacientes (admin)'],
  ['/identidad', 'Identidad (admin)'],
  ['/configuracion', 'Configuración (admin)'],
]

const exe =
  'C:/Users/alexlocal/AppData/Local/ms-playwright/chromium_headless_shell-1223/chrome-headless-shell-win64/chrome-headless-shell.exe'

const browser = await chromium.launch({ executablePath: exe, headless: true })
const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 } })
const page = await ctx.newPage()

const consoleErrors = []
const pageErrors = []
const failed = []
page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text().slice(0, 300)) })
page.on('pageerror', (e) => pageErrors.push(String(e).slice(0, 300)))
page.on('requestfailed', (r) => failed.push(`REQFAIL ${r.method()} ${r.url()} ${r.failure()?.errorText}`))
page.on('response', (r) => {
  if (r.status() >= 400) failed.push(`HTTP ${r.status()} ${r.request().method()} ${r.url()}`)
})

const out = []
const snap = () => {
  const errs = [...new Set(consoleErrors)]
  const fail = [...new Set(failed)]
  consoleErrors.length = 0
  failed.length = 0
  return { errs, fail }
}

// --- login ---
await page.goto(`${BASE}/login`, { waitUntil: 'domcontentloaded' })
await page.waitForTimeout(600)
await page.fill('input[name="username"], input#username, input[type="text"]', USER).catch(() => {})
await page.fill('input[type="password"]', PASS).catch(() => {})
await page.click('button[type="submit"], button:has-text("Entrar"), button:has-text("Iniciar")').catch(() => {})
await page.waitForTimeout(2500)
out.push({ route: 'LOGIN', finalUrl: page.url(), bodySnippet: (await page.locator('body').innerText().catch(() => '')).slice(0, 160).replace(/\n+/g, ' | '), ...snap() })

for (const [route, label] of ROUTES) {
  consoleErrors.length = 0
  failed.length = 0
  const perPageErrors = []
  const perPageFailed = []
  try {
    await page.goto(`${BASE}${route}`, { waitUntil: 'domcontentloaded' })
    await page.waitForTimeout(1800)
    const text = await page.locator('body').innerText().catch(() => '')
    const h1 = await page.locator('h1').first().innerText().catch(() => '')
    const hasMain = (await page.locator('main').count()) > 0
    const empty = text.trim().length < 25 && h1.trim().length === 0
    out.push({
      route, label, finalUrl: page.url(), h1: h1.slice(0, 90), empty,
      errs: [...new Set(consoleErrors)], fail: [...new Set(failed)].slice(0, 12),
      textSnippet: text.slice(0, 200).replace(/\n+/g, ' | '),
    })
  } catch (e) {
    out.push({ route, label, exception: String(e).slice(0, 200) })
  }
}
await browser.close()

const file = 'ui-probe-report.json'
fs.writeFileSync(file, JSON.stringify(out, null, 1))
console.log(JSON.stringify(out.map(({ errs, fail, ...r }) => ({ ...r, errs: errs?.length, fail: fail?.length })), null, 1))
