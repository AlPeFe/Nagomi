// Verifica el mantenimiento de clientes con cola opcional (crear, leer, actualizar).
const base = 'http://localhost:8080';
const login = await fetch(base + '/connect/token', {
  method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
  body: new URLSearchParams({ grant_type: 'password', username: 'e2e-admin', password: 'E2eAdminSegura2026!' }),
});
const { access_token: token } = await login.json();
const H = { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token };
const name = 'Cliente con cola ' + Date.now().toString().slice(-5);

const created = await fetch(base + '/api/admin/tenant/clients', {
  method: 'POST', headers: H,
  body: JSON.stringify({ name, taxId: 'B12345678', contactPerson: 'Ana', phone: '600000000', email: 'ana@acme.test', address: 'C/ Mayor 1', rabbitQueue: 'nagomi.acme', isActive: true }),
});
const client = await created.json();
console.log('alta HTTP', created.status, '| publicId', client.publicId, '| rabbitQueue', client.rabbitQueue);

const list = await (await fetch(base + '/api/admin/tenant/clients', { headers: H })).json();
const found = list.find((c) => c.id === client.id);
console.log('relectura  | rabbitQueue', found?.rabbitQueue, '| nombre', found?.name);

// quedarse sin cola también debe ser posible (opcional)
const cleared = await fetch(base + `/api/admin/tenant/clients/${client.id}`, {
  method: 'PUT', headers: H,
  body: JSON.stringify({ name, taxId: 'B12345678', contactPerson: 'Ana', phone: '600000000', email: 'ana@acme.test', address: 'C/ Mayor 1', rabbitQueue: '', isActive: true }),
});
const without = await cleared.json();
console.log('sin cola  | HTTP', cleared.status, '| rabbitQueue', JSON.stringify(without.rabbitQueue));
console.log('clientes con cola en la lista:', list.filter((c) => c.rabbitQueue).length);