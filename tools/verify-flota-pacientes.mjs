// Verifica los datos nuevos de vehículo (matrícula, plazas, notas) y paciente (dirección, nacimiento).
const base = 'http://localhost:8080';
const login = await fetch(base + '/connect/token', {
  method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
  body: new URLSearchParams({ grant_type: 'password', username: 'e2e-admin', password: 'E2eAdminSegura2026!' }),
});
const { access_token: token } = await login.json();
const H = { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token };
const stamp = Date.now().toString().slice(-5);

// VEHICULO
const vres = await fetch(base + '/api/admin/vehicles', {
  method: 'POST', headers: H,
  body: JSON.stringify({ name: 'Ambulancia ' + stamp, externalCode: 'EXT-' + stamp, vehicleType: 0, isActive: true, plate: '1234 ABC', capacity: 2, notes: 'Camilla bariátrica' }),
});
const vehicle = await vres.json();
console.log('vehiculo HTTP', vres.status, '| matricula', vehicle.plate, '| plazas', vehicle.capacity, '| notas', vehicle.notes);
const vlist = await (await fetch(base + '/api/vehicles', { headers: H })).json();
const vfound = (Array.isArray(vlist) ? vlist : vlist.items ?? []).find((v) => v.id === vehicle.id);
console.log('relectura  | matricula', vfound?.plate, '| plazas', vfound?.capacity);

// PACIENTE
const pres = await fetch(base + '/api/admin/patients', {
  method: 'POST', headers: H,
  body: JSON.stringify({ firstName: 'Paciente', lastName: 'Campos ' + stamp, documentNumber: 'DNI-' + stamp, phone: '600111222', address: 'Calle Mayor 3, Madrid', birthDate: '1985-04-12' }),
});
const patient = await pres.json();
console.log('paciente HTTP', pres.status, '| direccion', patient.address, '| nacimiento', patient.birthDate);
const plist = await (await fetch(base + '/api/admin/patients', { headers: H })).json();
const pfound = (Array.isArray(plist) ? plist : plist.items ?? []).find((p) => p.id === patient.id);
console.log('relectura  | direccion', pfound?.address, '| nacimiento', pfound?.birthDate);