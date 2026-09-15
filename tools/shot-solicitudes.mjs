// Captura la pantalla de Solicitudes (cabeceras) en la instancia real.
import { chromium } from 'playwright-core';

const base = process.argv[2] ?? 'http://localhost:8080';
const user = process.argv[3] ?? 'e2e-admin';
const pass = process.argv[4];
if (!pass) { console.error('falta la contrasena'); process.exit(1); }

const exe = 'C:/Users/alexlocal/AppData/Local/ms-playwright/chromium-1223/chrome-win64/chrome.exe';
const browser = await chromium.launch({ executablePath: exe, headless: true });
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
const errors = [];
page.on('console', (m) => { if (m.type() === 'error' && !m.text().includes('Failed to load resource')) errors.push(m.text().slice(0, 160)); });
page.on('pageerror', (e) => errors.push(`PAGEERROR ${String(e).slice(0, 160)}`));

await page.goto(base + '/login', { waitUntil: 'domcontentloaded' });
await page.fill('input[autocomplete="username"]', user);
await page.fill('input[autocomplete="current-password"]', pass);
await page.click('button[type="submit"]');
await page.waitForURL(/trayectos/, { timeout: 20000 });
await page.waitForTimeout(1500);

await page.goto(base + '/solicitudes', { waitUntil: 'domcontentloaded' });
await page.waitForTimeout(2500);
await page.screenshot({ path: '.hermes-shots/solicitudes.png', fullPage: true });

const cards = await page.locator('.request-card').count();
console.log('tarjetas de solicitud:', cards);
const first = await page.locator('.request-card').first().innerText();
console.log('--- primera cabecera ---');
console.log(first.replace(/\n+/g, ' | ').slice(0, 500));

// ¿Se solapa algo? Comparamos los rectángulos de los textos dentro de la primera tarjeta.
const overlap = await page.evaluate(() => {
  const card = document.querySelector('.request-card');
  if (!card) return 'sin tarjeta';
  const nodes = [...card.querySelectorAll('span, strong')].filter((n) => (n.textContent ?? '').trim());
  const boxes = nodes.map((n) => ({ text: (n.textContent ?? '').slice(0, 24), r: n.getBoundingClientRect() }));
  const hits = [];
  for (let i = 0; i < boxes.length; i++) for (let j = i + 1; j < boxes.length; j++) {
    const a = boxes[i].r, b = boxes[j].r;
    if (boxes[i].text.trim() === boxes[j].text.trim()) continue;
    const overlapX = Math.min(a.right, b.right) - Math.max(a.left, b.left);
    const overlapY = Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top);
    if (overlapX > 2 && overlapY > 2) hits.push(`${boxes[i].text} ✕ ${boxes[j].text}`);
  }
  return hits.length ? hits : 'sin solapamientos';
});
console.log('solapamiento:', JSON.stringify(overlap));
// Alturas uniformes: una ruta larga no debe hacer la tarjeta mas alta que las demas.
const heights = await page.evaluate(() => [...document.querySelectorAll('.request-card')]
  .map((c) => Math.round(c.getBoundingClientRect().height)));
console.log('alturas de tarjeta:', JSON.stringify([...new Set(heights)]), 'de', heights.length, 'tarjetas');
console.log('errores de consola:', errors.length, errors.slice(0, 3));
await browser.close();