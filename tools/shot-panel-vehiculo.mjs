// Captura la mesa diaria (vehículo compacto, sin columna proveedor) y el panel de vehículo.
import { chromium } from 'playwright-core';

const base = process.argv[2] ?? 'http://localhost:8080';
const exe = 'C:/Users/alexlocal/AppData/Local/ms-playwright/chromium-1223/chrome-win64/chrome.exe';
const browser = await chromium.launch({ executablePath: exe, headless: true });
const page = await browser.newPage({ viewport: { width: 1500, height: 1000 } });
const errors = [];
page.on('pageerror', (e) => errors.push(String(e).slice(0, 140)));

await page.goto(base + '/login', { waitUntil: 'domcontentloaded' });
await page.fill('input[autocomplete="username"]', process.argv[3]);
await page.fill('input[autocomplete="current-password"]', process.argv[4]);
await page.click('button[type="submit"]');
await page.waitForURL(/trayectos/, { timeout: 20000 });
await page.waitForTimeout(2500);

const headers = await page.locator('.operations-table thead th').allInnerTexts();
console.log('columnas de la mesa diaria:', JSON.stringify(headers));
console.log('vehículos en la tabla:', JSON.stringify(await page.locator('.vehicle-tag, .vehicle-cell-empty').allInnerTexts()));
console.log('desplegables de vehículo (debe ser 0):', await page.locator('.operations-table select').count());
await page.screenshot({ path: '.hermes-shots/mesa-diaria.png' });

const gavel = page.locator('.row-action-primary').first();
console.log('botones de adjudicar en acciones:', await page.locator('.row-action-primary').count());
await gavel.click();
await page.waitForTimeout(900);
await page.screenshot({ path: '.hermes-shots/panel-vehiculo.png' });
const panel = await page.locator('.assignment-panel').innerText();
console.log('--- panel ---');
console.log(panel.replace(/\n+/g, ' | ').slice(0, 600));
console.log('errores JS:', errors.length, errors.slice(0, 2));
await browser.close();