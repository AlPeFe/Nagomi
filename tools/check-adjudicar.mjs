// Adjudica un traslado y comprueba si el traslado queda adjudicado y si se publica.
const base = 'http://localhost:8080';
const login = await fetch(base + '/connect/token', {
  method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
  body: new URLSearchParams({ grant_type: 'password', username: 'e2e-admin', password: 'E2eAdminSegura2026!' }),
});
const { access_token: token } = await login.json();
const H = { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token };

const list = await (await fetch(base + '/api/operations/journeys', { headers: H })).json();
const rows = Array.isArray(list) ? list : (list.items ?? []);
const row = rows.find((r) => !['Completed', 'Cancelled'].includes(String(r.status)));
const vehicles = await (await fetch(base + '/api/vehicles', { headers: H })).json();
const vehicle = (Array.isArray(vehicles) ? vehicles : (vehicles.items ?? [])).find((v) => v.isActive !== false);
const id = row.journeyId ?? row.id;

const response = await fetch(`${base}/api/journeys/${id}/adjudicate-vehicle`, {
  method: 'POST', headers: H, body: JSON.stringify({ vehicleId: vehicle.id, actor: 'e2e' }),
});
const body = await response.json();
console.log('HTTP', response.status);
console.log('adjudicatedAt:', body.adjudicatedAt, '| adjudicatedBy:', body.adjudicatedBy, '| vehicleId:', body.vehicleId);
console.log('publicId:', body.publicId);