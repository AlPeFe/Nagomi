// Diagnóstico del login POR LA INTERFAZ (lo que hace el usuario en el navegador).
import { chromium } from 'playwright-core';

const base = process.argv[2] ?? 'http://192.168.31.223:8080';
const user = process.argv[3] ?? 'e2e-admin';
const pass = process.argv[4];
const exe = 'C:/Users/alexlocal/AppData/Local/ms-playwright/chromium-1223/chrome-win64/chrome.exe';
const browser = await chromium.launch({ executablePath: exe, headless: true });
const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });
const console_ = [];
const requests = [];
page.on('console', (m) => { if (m.type() === 'error') console_.push(m.text().slice(0, 160)); });
page.on('pageerror', (e) => console_.push('PAGEERROR ' + String(e).slice(0, 160)));
page.on('response', (r) => { if (r.url().includes('/connect/') || r.url().includes('/api/')) requests.push(`${r.status()} ${r.request().method()} ${new URL(r.url()).pathname}`); });

await page.goto(base + '/login', { waitUntil: 'domcontentloaded' });
await page.waitForTimeout(1200);
console.log('URL tras cargar:', page.url());

const fields = await page.evaluate(() => [...document.querySelectorAll('input')].map((i) => `${i.type}:${i.getAttribute('autocomplete') ?? '-'}:${i.name || '-'}`));
console.log('campos del formulario:', JSON.stringify(fields));

await page.fill('input[autocomplete="username"]', user);
await page.fill('input[autocomplete="current-password"]', pass ?? process.env.PW);
await page.click('button[type="submit"]');
await page.waitForTimeout(4000);

console.log('URL después de enviar:', page.url());
const body = (await page.locator('body').innerText()).replace(/\s+/g, ' ');
console.log('texto visible:', body.slice(0, 240));
console.log('peticiones HTTP:', JSON.stringify(requests.slice(0, 6)));
console.log('errores de consola:', console_.length, console_.slice(0, 3));
await page.screenshot({ path: '.hermes-shots/login-diagnostico.png' });
await browser.close();