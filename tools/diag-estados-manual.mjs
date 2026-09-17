// Intenta marcar un estado manual desde el detalle del trayecto y captura qué falla.
import { chromium } from 'playwright-core';

const base = process.argv[2] ?? 'http://localhost:8080';
const exe = 'C:/Users/alexlocal/AppData/Local/ms-playwright/chromium-1223/chrome-win64/chrome.exe';
const browser = await chromium.launch({ executablePath: exe, headless: true });
const page = await browser.newPage({ viewport: { width: 1400, height: 1000 } });
const errors = [];
const api = [];
page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text().slice(0, 220)); });
page.on('pageerror', (e) => errors.push('PAGEERROR ' + String(e).slice(0, 220)));
page.on('response', (r) => { const u = r.url(); if (u.includes('/api/')) api.push(`${r.status()} ${r.request().method()} ${new URL(u).pathname}`); });

await page.goto(base + '/login', { waitUntil: 'domcontentloaded' });
await page.fill('input[autocomplete="username"]', 'e2e-admin');
await page.fill('input[autocomplete="current-password"]', process.env.PW);
await page.click('button[type="submit"]');
await page.waitForURL(/trayectos/, { timeout: 20000 });
await page.waitForTimeout(1500);

// Un trayecto vivo de la ventana amplia
const token = await page.evaluate(() => sessionStorage.getItem('nagomi_token') ?? localStorage.getItem('nagomi_token'));
const list = await (await fetch(base + '/api/operations/journeys?from=2026-09-01&to=2026-09-30', { headers: { Authorization: 'Bearer ' + token } })).json();
const rows = Array.isArray(list) ? list : (list.items ?? []);
const target = rows.find((r) => r.status !== 8 && r.status !== 7) ?? rows[0];
console.log('trayecto:', target.journeyPublicId, '| estado:', target.status);

await page.goto(`${base}/trayectos/${target.journeyId ?? target.id}`, { waitUntil: 'domcontentloaded' });
await page.waitForTimeout(2500);

const card = page.locator('section', { hasText: 'Operación manual' }).first();
console.log('tarjeta "Operación manual" presente:', await card.count());
console.log('--- contenido de la tarjeta ---');
console.log((await card.innerText().catch(() => '(no se pudo leer)')).replace(/\n+/g, ' | ').slice(0, 420));

const buttons = await card.locator('button').allInnerTexts();
console.log('botones:', JSON.stringify(buttons));
const selects = await card.locator('select').count();
console.log('selectores en la tarjeta:', selects);

// Intentar marcar el siguiente estado
api.length = 0;
const marcar = card.locator('button', { hasText: /^Marcar / }).first();
if (await marcar.count()) {
  await marcar.click();
  await page.waitForTimeout(3000);
  console.log('HTTP tras Marcar:', JSON.stringify(api.slice(-5)));
  const after = (await card.innerText()).replace(/\n+/g, ' | ');
  console.log('texto tras Marcar:', after.slice(0, 260));
} else {
  console.log('NO hay botón Marcar en la tarjeta');
}
console.log('errores de consola:', errors.length, errors.slice(0, 4));
await page.screenshot({ path: '.hermes-shots/detalle-estados.png', fullPage: true });
await browser.close();