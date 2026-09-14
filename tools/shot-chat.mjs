// Captura el asistente de IA en la app real: botón flotante y panel abierto.
import { chromium } from 'playwright-core';

const base = process.argv[2] ?? 'http://localhost:8080';
const user = process.argv[3] ?? 'e2e-admin';
const pass = process.argv[4];
const out = process.argv[5] ?? 'chat';
if (!pass) { console.error('falta la contrasena'); process.exit(1); }

const exe = 'C:/Users/alexlocal/AppData/Local/ms-playwright/chromium-1223/chrome-win64/chrome.exe';
const browser = await chromium.launch({ executablePath: exe, headless: true });
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
const errors = [];
page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text().slice(0, 140)); });

await page.goto(base + '/login', { waitUntil: 'domcontentloaded' });
await page.fill('input[autocomplete="username"]', user);
await page.fill('input[autocomplete="current-password"]', pass);
await page.click('button[type="submit"]');
await page.waitForURL(/trayectos/, { timeout: 20000 });
await page.waitForTimeout(2500);

const fab = page.locator('.help-chat-fab');
console.log('boton flotante visible:', await fab.count());
await page.screenshot({ path: `.hermes-shots/${out}-boton.png` });

if (await fab.count()) {
  await fab.click();
  await page.waitForTimeout(900);
  await page.screenshot({ path: `.hermes-shots/${out}-panel.png` });
  const panel = page.locator('.help-chat-panel');
  await panel.screenshot({ path: `.hermes-shots/${out}-panel-solo.png` });
  console.log('--- texto del panel ---');
  console.log((await panel.innerText()).slice(0, 600));

  // Estado de conversación: burbujas + aviso si el proveedor no responde.
  if (process.argv[6] === 'send') {
    await page.fill('.help-chat-form input', '¿Qué traslados tengo hoy?');
    await page.click('.help-chat-send');
    await page.waitForTimeout(6000);
    await panel.screenshot({ path: `.hermes-shots/${out}-conversacion.png` });
    console.log('--- tras enviar ---');
    console.log((await panel.innerText()).slice(-400));
  }
}
console.log('errores de consola:', errors.length, errors.slice(0, 3).join(' | '));
await browser.close();
