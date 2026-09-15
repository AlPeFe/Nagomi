// Diagnóstico responsive del DETALLE de solicitud y del cajón móvil del menú.
import { chromium } from 'playwright-core';

const base = process.argv[2] ?? 'http://localhost:8080';
const user = process.argv[3] ?? 'e2e-admin';
const pass = process.argv[4];
const exe = 'C:/Users/alexlocal/AppData/Local/ms-playwright/chromium-1223/chrome-win64/chrome.exe';
const browser = await chromium.launch({ executablePath: exe, headless: true });

const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
await page.goto(base + '/login', { waitUntil: 'domcontentloaded' });
await page.fill('input[autocomplete="username"]', user);
await page.fill('input[autocomplete="current-password"]', pass);
await page.click('button[type="submit"]');
await page.waitForURL(/trayectos/, { timeout: 20000 });
await page.goto(base + '/solicitudes', { waitUntil: 'domcontentloaded' });
await page.waitForTimeout(2000);
const href = await page.locator('.request-card').first().getAttribute('href');
console.log('detalle a revisar:', href);
await page.close();

for (const width of [1440, 820, 390]) {
  const p = await browser.newPage({ viewport: { width, height: 900 } });
  await p.goto(base + '/login', { waitUntil: 'domcontentloaded' });
  await p.fill('input[autocomplete="username"]', user);
  await p.fill('input[autocomplete="current-password"]', pass);
  await p.click('button[type="submit"]');
  await p.waitForURL(/trayectos/, { timeout: 20000 });
  await p.goto(base + href.replace(/^#/, ''), { waitUntil: 'domcontentloaded' });
  await p.waitForTimeout(1800);
  const r = await p.evaluate(() => {
    const root = document.documentElement;
    const overlapOf = (node) => {
      const nodes = [...node.querySelectorAll('span, strong, small, dt, dd, a, h2')].filter((n) => (n.textContent ?? '').trim());
      const boxes = nodes.map((n) => ({ t: (n.textContent ?? '').slice(0, 16), r: n.getBoundingClientRect() }));
      const hits = [];
      for (let i = 0; i < boxes.length; i++) for (let j = i + 1; j < boxes.length; j++) {
        const a = boxes[i].r, b = boxes[j].r;
        if (boxes[i].t === boxes[j].t) continue;
        const ox = Math.min(a.right, b.right) - Math.max(a.left, b.left);
        const oy = Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top);
        if (ox > 2 && oy > 2) hits.push(`${boxes[i].t} ✕ ${boxes[j].t}`);
      }
      return hits.slice(0, 5);
    };
    const main = document.querySelector('.page') ?? document.body;
    const spill = [...main.querySelectorAll('*')].filter((n) => n.getBoundingClientRect().right > window.innerWidth + 1)
      .map((n) => `${n.className || n.tagName}`.slice(0, 40));
    return { overflow: Math.max(0, root.scrollWidth - window.innerWidth), spill: [...new Set(spill)].slice(0, 6), hits: overlapOf(main) };
  });
  console.log(`\n== detalle ${width}px == desborde:${r.overflow}px`);
  console.log('   solapamiento:', r.hits.length ? JSON.stringify(r.hits) : 'ninguno');
  console.log('   se salen:', r.spill.length ? JSON.stringify(r.spill) : 'nada');
  if (width === 390) await p.screenshot({ path: '.hermes-shots/solicitud-detalle-movil.png', fullPage: true });
  await p.close();
}
await browser.close();