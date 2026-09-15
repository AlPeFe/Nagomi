// Diagnóstico responsive de la pantalla de Solicitudes: solapamiento y desborde por ancho.
import { chromium } from 'playwright-core';

const base = process.argv[2] ?? 'http://localhost:8080';
const user = process.argv[3] ?? 'e2e-admin';
const pass = process.argv[4];
if (!pass) { console.error('falta la contrasena'); process.exit(1); }

const exe = 'C:/Users/alexlocal/AppData/Local/ms-playwright/chromium-1223/chrome-win64/chrome.exe';
const browser = await chromium.launch({ executablePath: exe, headless: true });

for (const width of [1440, 1100, 820, 640, 390]) {
  const page = await browser.newPage({ viewport: { width, height: 900 } });
  const errors = [];
  page.on('pageerror', (e) => errors.push(String(e).slice(0, 120)));
  await page.goto(base + '/login', { waitUntil: 'domcontentloaded' });
  await page.fill('input[autocomplete="username"]', user);
  await page.fill('input[autocomplete="current-password"]', pass);
  await page.click('button[type="submit"]');
  await page.waitForURL(/trayectos/, { timeout: 20000 });
  await page.waitForTimeout(1200);
  await page.goto(base + '/solicitudes', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(2000);

  const report = await page.evaluate(() => {
    const root = document.documentElement;
    const overflow = Math.max(0, root.scrollWidth - window.innerWidth);
    // Elementos que se salen de su contenedor por la derecha
    const spill = [...document.querySelectorAll('.page *, .request-card *')]
      .filter((n) => n.getBoundingClientRect().right > window.innerWidth + 1)
      .map((n) => `${n.className || n.tagName}`.slice(0, 46));
    // Solapamiento dentro de la primera tarjeta
    const card = document.querySelector('.request-card');
    const hits = [];
    if (card) {
      const nodes = [...card.querySelectorAll('span, strong')].filter((n) => (n.textContent ?? '').trim());
      const boxes = nodes.map((n) => ({ t: (n.textContent ?? '').slice(0, 18), r: n.getBoundingClientRect() }));
      for (let i = 0; i < boxes.length; i++) for (let j = i + 1; j < boxes.length; j++) {
        const a = boxes[i].r, b = boxes[j].r;
        if (boxes[i].t.trim() === boxes[j].t.trim()) continue;
        const ox = Math.min(a.right, b.right) - Math.max(a.left, b.left);
        const oy = Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top);
        if (ox > 2 && oy > 2) hits.push(`${boxes[i].t} ✕ ${boxes[j].t}`);
      }
    }
    const heights = [...new Set([...document.querySelectorAll('.request-card')].map((c) => Math.round(c.getBoundingClientRect().height)))];
    return { overflow, spill: [...new Set(spill)].slice(0, 6), hits: hits.slice(0, 4), heights, cards: document.querySelectorAll('.request-card').length };
  });
  console.log(`\n== ${width}px == tarjetas:${report.cards} desborde:${report.overflow}px alturas:${JSON.stringify(report.heights)}`);
  console.log('   solapamiento:', report.hits.length ? JSON.stringify(report.hits) : 'ninguno');
  console.log('   se salen:', report.spill.length ? JSON.stringify(report.spill) : 'nada');
  if (errors.length) console.log('   errores JS:', errors.slice(0, 2));
  if (width === 390) await page.screenshot({ path: '.hermes-shots/solicitudes-movil.png', fullPage: true });
  await page.close();
}
await browser.close();