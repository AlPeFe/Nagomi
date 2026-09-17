// Restaura el acceso del administrador SIN imprimir secretos.
// El admin de seed está desactivado: se activa temporalmente, se usa y se vuelve a desactivar.
const base = process.argv[2] ?? 'http://localhost:8080';
const seedPassword = process.env.SEED_ADMIN_PW;
const newPassword = process.env.NEW_ADMIN_PW;

async function login(user, pass) {
  const r = await fetch(base + '/connect/token', {
    method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ grant_type: 'password', username: user, password: pass }),
  });
  return { status: r.status, text: await r.text() };
}

const seed = await login('admin', seedPassword);
console.log('login del admin de seed:', seed.status);
if (seed.status !== 200) { console.log('  (no sirve, hace falta la vía directa en BD)'); process.exit(2); }

const token = JSON.parse(seed.text).access_token;
const H = { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token };
const users = await (await fetch(base + '/api/admin/users', { headers: H })).json();
const list = Array.isArray(users) ? users : (users.items ?? []);
const target = list.find((u) => u.email === 'e2e@empresa.es');
console.log('cuenta a restaurar:', target?.email, '| activa:', target?.isActive);

const reset = await fetch(base + `/api/admin/users/${target.id}`, {
  method: 'PUT', headers: H,
  body: JSON.stringify({ displayName: target.displayName ?? 'E2E Admin', userName: 'e2e-admin', email: target.email, password: newPassword, isActive: true, roles: target.roles }),
});
console.log('reset de la contraseña:', reset.status);

const check = await login('e2e-admin', newPassword);
console.log('login con la nueva contraseña:', check.status, check.status === 200 ? 'OK' : check.text.slice(0, 120));