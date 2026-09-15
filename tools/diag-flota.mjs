// Diagnóstico crudo: qué responde cada endpoint (sin parsear JSON).
const base = 'http://localhost:8080';
const login = await fetch(base + '/connect/token', {
  method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
  body: new URLSearchParams({ grant_type: 'password', username: 'e2e-admin', password: 'E2eAdminSegura2026!' }),
});
const { access_token: token } = await login.json();
const H = { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token };

async function probe(label, url, init) {
  const response = await fetch(base + url, init);
  const text = await response.text();
  console.log(`${label}: HTTP ${response.status} | ${response.headers.get('content-type') ?? ''} | ${text.slice(0, 220).replace(/\s+/g, ' ')}`);
}

await probe('GET  /api/patients', '/api/patients', { headers: H });
await probe('GET  /api/vehicles', '/api/vehicles', { headers: H });
await probe('POST /api/admin/vehicles', '/api/admin/vehicles', {
  method: 'POST', headers: H,
  body: JSON.stringify({ name: 'Ambulancia diag', externalCode: 'EXT-DIAG', vehicleType: 0, isActive: true, plate: '1234 ABC', capacity: 2, notes: 'nota' }),
});
await probe('POST /api/patients', '/api/patients', {
  method: 'POST', headers: H,
  body: JSON.stringify({ firstName: 'Paciente', lastName: 'Diagnostico', documentNumber: 'DNI-DIAG', address: 'Calle Mayor 3', birthDate: '1985-04-12' }),
});