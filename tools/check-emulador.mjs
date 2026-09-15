// Comprueba qué puede probar el usuario en el emulador: vehículo de pruebas creado por el e2e.
const base = 'http://192.168.31.223:8080';
const login = await fetch(base + '/connect/token', {
  method: 'POST',
  headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
  body: new URLSearchParams({ grant_type: 'password', username: 'e2e-admin', password: 'E2eAdminSegura2026!' }),
});
const { access_token: token } = await login.json();
const H = { Authorization: 'Bearer ' + token };

const vehicles = await (await fetch(base + '/api/vehicles', { headers: H })).json();
const list = Array.isArray(vehicles) ? vehicles : (vehicles.items ?? []);
console.log('vehiculos (publicId | tipo | activo):');
for (const v of list) console.log(' -', v.publicId ?? v.id, '|', v.vehicleType ?? v.type ?? '?', '|', v.isActive ?? v.active ?? '?');

const workers = await (await fetch(base + '/api/workers', { headers: H })).json();
const wl = Array.isArray(workers) ? workers : (workers.items ?? []);
console.log('trabajadores:', wl.slice(0, 6).map((w) => `${w.employeeCode ?? w.code ?? '?'} ${w.fullName ?? w.name ?? ''}`).join(' | '));