// Smoke tras el cambio de contrato: la app sigue sirviendo datos y el asistente/recurrencia están sanos.
const base = 'http://localhost:8080';
const login = await fetch(base + '/connect/token', {
  method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
  body: new URLSearchParams({ grant_type: 'password', username: 'e2e-admin', password: 'E2eAdminSegura2026!' }),
});
const { access_token: token } = await login.json();
const H = { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token };

async function check(label, url, init) {
  const r = await fetch(base + url, init ?? { headers: H });
  const body = await r.text();
  console.log(`${label}: HTTP ${r.status} | ${body.slice(0, 120).replace(/\s+/g, ' ')}`);
  return r.status;
}

await check('token', '/connect/token', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: new URLSearchParams({ grant_type: 'password', username: 'e2e-admin', password: 'E2eAdminSegura2026!' }) });
await check('operaciones', '/api/operations/journeys');
await check('solicitudes', '/api/operations/requests');
await check('clientes', '/api/admin/tenant/clients');
await check('vehiculos', '/api/vehicles');
await check('pacientes', '/api/admin/patients');
await check('openapi', '/openapi/v1.json');
await check('asistente', '/api/help-chat/status');
await check('sin token (esperado 401)', '/api/operations/journeys', {});