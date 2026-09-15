// Verifica la semántica asignar vs adjudicar contra la instancia real:
//   1) asignar NO crea notificación (nada va a Rabbit)
//   2) adjudicar SÍ la crea (el proveedor puede recuperar)
//   3) desadjudicar la retira
const base = process.argv[2] ?? 'http://localhost:8080';

const login = await fetch(base + '/connect/token', {
  method: 'POST',
  headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
  body: new URLSearchParams({ grant_type: 'password', username: process.argv[3], password: process.argv[4] }),
});
const { access_token: token } = await login.json();
const H = { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token };
const json = async (r) => ({ status: r.status, body: await r.json().catch(() => null) });

// Un traslado de la ventana de operación
const list = await (await fetch(base + '/api/operations/journeys', { headers: H })).json();
const rows = Array.isArray(list) ? list : (list.items ?? []);
const target = rows.find((row) => !['Completed', 'Cancelled'].includes(String(row.status))) ?? rows[0];
console.log('traslado de prueba:', target.journeyPublicId, '| vehículo previo:', target.vehicleId ? target.vehicleName : 'ninguno', '| adjudicado:', target.vehicleAdjudicated);

const vehicles = await (await fetch(base + '/api/vehicles', { headers: H })).json();
const vlist = Array.isArray(vehicles) ? vehicles : (vehicles.items ?? []);
const vehicle = vlist.find((v) => v.isActive !== false) ?? vlist[0];
if (!vehicle) { console.error('no hay vehículos'); process.exit(1); }
console.log('vehículo elegido:', vehicle.name, vehicle.publicId);

const journeyId = target.journeyId ?? target.id;
const notifications = async () => {
  const all = await (await fetch(base + '/api/provider-integration/notifications', { headers: H })).json().catch(() => null);
  const items = Array.isArray(all) ? all : (all?.items ?? []);
  return items.filter((n) => n.entityPublicId === target.journeyPublicId);
};

console.log('\n1) ASIGNAR (placeholder)');
console.log('   ', JSON.stringify(await json(await fetch(`${base}/api/journeys/${journeyId}/assign-vehicle`, { method: 'POST', headers: H, body: JSON.stringify({ vehicleId: vehicle.id, actor: 'e2e' }) }))).slice(0, 220));
console.log('   notificaciones de este traslado:', JSON.stringify(await notifications()));

console.log('\n2) ADJUDICAR (compromete y publica)');
console.log('   ', JSON.stringify(await json(await fetch(`${base}/api/journeys/${journeyId}/adjudicate-vehicle`, { method: 'POST', headers: H, body: JSON.stringify({ vehicleId: vehicle.id, actor: 'e2e' }) }))).slice(0, 260));
const afterAdjudicate = await notifications();
console.log('   notificaciones:', JSON.stringify(afterAdjudicate.map((n) => ({ tipo: n.messageType, estado: n.state }))));

console.log('\n3) DESADJUDICAR (retira)');
console.log('   ', JSON.stringify(await json(await fetch(`${base}/api/journeys/${journeyId}/unadjudicate-vehicle`, { method: 'POST', headers: H, body: JSON.stringify({ actor: 'e2e' }) }))).slice(0, 260));
const afterUnadjudicate = await notifications();
console.log('   notificaciones:', JSON.stringify(afterUnadjudicate.map((n) => ({ tipo: n.messageType, estado: n.state }))));