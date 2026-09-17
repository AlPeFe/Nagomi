// Prueba los casos que fallan al marcar estados y si el error es VISIBLE para el usuario.
import { chromium } from 'playwright-core';

const base = process.argv[2] ?? 'http://localhost:8080';
const exe = 'C:/Users/alexlocal/AppData/Local/ms-playwright/chromium-1223/chrome-win64/chrome.exe';
const browser = await chromium.launch({ executablePath: exe, headless: true });
const page = await browser.newPage({ viewport: { width: 1400, height: 1100 } });
const api = [];
page.on('response', async (r) => {
  const u = r.url();
  if (u.includes('/statuses') && r.request().method() === 'POST') {
    api.push(`${r.status()} | ${(await r.text().catch(() => '')).slice(0, 240).replace(/\s+/g, ' ')}`);
  }
});

await page.goto(base + '/login', { waitUntil: 'domcontentloaded' });
await page.fill('input[autocomplete="username"]', 'e2e-admin');
await page.fill('input[autocomplete="current-password"]', process.env.PW);
await page.click('button[type="submit"]');
await page.waitForURL(/trayectos/, { timeout: 20000 });
await page.waitForTimeout(1200);

const token = await page.evaluate(() => sessionStorage.getItem('nagomi_token') ?? localStorage.getItem('nagomi_token'));
const list = await (await fetch(base + '/api/operations/journeys?from=2026-09-01&to=2026-09-30', { headers: { Authorization: 'Bearer ' + token } })).json();
const rows = Array.isArray(list) ? list : (list.items ?? []);
const target = rows.find((r) => r.status === 0) ?? rows[0];

await page.goto(`${base}/trayectos/${target.journeyId ?? target.id}`, { waitUntil: 'domcontentloaded' });
await page.waitForTimeout(2000);
const card = page.locator('section', { hasText: 'Operación manual' }).first();
const selects = card.locator('select');
console.log('selectores:', await selects.count());

// 1) Elegir una fase que NO es la siguiente (Completado estando Programado)
const phaseSelect = selects.nth(1);
const options = await phaseSelect.locator('option').allInnerTexts();
console.log('fases ofrecidas:', JSON.stringify(options));
await phaseSelect.selectOption({ label: options[options.length - 1] });
await page.waitForTimeout(300);
api.length = 0;
await card.locator('button', { hasText: /^Marcar / }).first().click();
await page.waitForTimeout(3000);
console.log('1) fase NO siguiente ->', JSON.stringify(api));

// ¿Se ve el error en pantalla?
const visible = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
const alerts = await page.locator('.alert, .form-error, .inline-message, [role="alert"]').allInnerTexts();
console.log('    avisos visibles:', JSON.stringify(alerts.map((a) => a.slice(0, 120))));
console.log('    ¿el texto menciona error?', /no se pudo|error|inválid|no válid/i.test(visible));

// 2) Marcar sin vehículo adjudicado, la siguiente fase
const phase2 = selects.nth(1);
const opts2 = await phase2.locator('option').allInnerTexts();
await phase2.selectOption({ label: opts2[0] });
await page.waitForTimeout(200);
api.length = 0;
await card.locator('button', { hasText: /^Marcar / }).first().click();
await page.waitForTimeout(2500);
console.log('2) siguiente fase ->', JSON.stringify(api.slice(0, 2)));
await browser.close();