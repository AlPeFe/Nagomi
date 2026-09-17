// Deja lista la instancia recién reseteada: verifica login y restaura la config del asistente.
const base = process.argv[2] ?? 'http://localhost:8080';
const H_LOGIN = { 'Content-Type': 'application/x-www-form-urlencoded' };

const res = await fetch(base + '/connect/token', {
  method: 'POST', headers: H_LOGIN,
  body: new URLSearchParams({ grant_type: 'password', username: 'admin123', password: 'admin123' }),
});
const { access_token: token } = await res.json();
console.log('login admin123:', res.status);
const H = { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token };

// La configuración del gateway de IA se perdió con el reset: se vuelve a dejar como estaba.
const ai = await fetch(base + '/api/admin/tenant/ai', {
  method: 'PUT', headers: H,
  body: JSON.stringify({
    enabled: false, provider: 'hermes', baseUrl: 'http://192.168.31.223:9119/v1',
    model: 'hermes', username: 'alex', enableTools: true,
  }),
});
console.log('config de IA restaurada:', ai.status, JSON.stringify(await ai.json()).slice(0, 150));

// Estado limpio de la instalación
for (const [label, url] of [['solicitudes', '/api/operations/requests'], ['vehículos', '/api/vehicles'], ['pacientes', '/api/admin/patients'], ['clientes', '/api/admin/tenant/clients']]) {
  const r = await fetch(base + url, { headers: H });
  const body = await r.json().catch(() => []);
  const items = Array.isArray(body) ? body : (body.items ?? []);
  console.log(`${label}: ${r.status} · ${items.length} registros`);
}
const caps = await (await fetch(base + '/api/admin/tenant/capabilities', { headers: H })).json();
console.log('capacidades del tenant:', JSON.stringify(caps));