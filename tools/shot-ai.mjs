// Captura la tarjeta de IA de Configuracion en la instancia real. Requiere login.
import { chromium } from 'playwright-core';

const base = process.argv[2] ?? 'http://localhost:8080';
const user = process.argv[3] ?? 'e2e-admin';
const pass = process.argv[4];
if (!pass) { console.error('falta la contrasena'); process.exit(1); }

// Mismo binario que usan los demas harnesses de Nagomi en este host.
const exe = 'C:/Users/alexlocal/AppData/Local/ms-playwright/chromium-1223/chrome-win64/chrome.exe';
const browser = await chromium.launch({ executablePath: exe, headless: true });
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
const errors = [];
page.on('console', (message) => { if (message.type() === 'error') errors.push(message.text()); });

await page.goto(base + '/login', { waitUntil: 'domcontentloaded' });
await page.fill('input[autocomplete="username"]', user);
await page.fill('input[autocomplete="current-password"]', pass);
await page.click('button[type="submit"]');
await page.waitForURL(/trayectos/, { timeout: 20000 });
await page.waitForTimeout(2000);

await page.goto(base + '/configuracion', { waitUntil: 'domcontentloaded' });
await page.waitForTimeout(3000);
const card = page.locator('section.config-card', { hasText: 'Asistente de IA' }).first();
await card.scrollIntoViewIfNeeded();
await page.waitForTimeout(600);
await card.screenshot({ path: '.hermes-shots/ai-card.png' });

const text = await card.innerText();
console.log('--- tarjeta de IA ---');
console.log(text.slice(0, 900));
console.log('errores de consola:', errors.length);
await browser.close();
