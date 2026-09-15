// ¿El listado de solicitudes incluye los traslados hijos? (para los contadores de la cabecera)
const base = 'http://192.168.31.223:8080';
const login = await fetch(base + '/connect/token', {
  method: 'POST',
  headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
  body: new URLSearchParams({ grant_type: 'password', username: 'e2e-admin', password: 'E2eAdminSegura2026!' }),
});
const { access_token: token } = await login.json();
const H = { Authorization: 'Bearer ' + token };

for (const path of ['/api/operations/requests?search=']) {
  const response = await fetch(base + path, { headers: H });
  console.log(path, '->', response.status);
  if (!response.ok) continue;
  const body = await response.json();
  const items = body.items ?? body;
  console.log('  solicitudes:', items.length);
  const sample = items[0];
  console.log('  campos:', Object.keys(sample).join(', '));
  console.log('  journeyRecords del primero:', sample.journeyRecords === undefined ? 'NO VIENE' : `${sample.journeyRecords.length}`, JSON.stringify(sample.journeyRecords?.[0] ?? null).slice(0, 200));
  const withChildren = items.filter((r) => (r.journeyRecords ?? []).length > 0).length;
  console.log('  cabeceras con hijos en el listado:', withChildren, '/', items.length);
  const recurring = items.filter((r) => r.recurring).length;
  console.log('  cabeceras con periodicidad:', recurring, '/', items.length);
  break;
}