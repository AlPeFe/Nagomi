// Deja la cuenta del administrador con una contraseña limpia y verificada.
const base = 'http://192.168.31.223:8080';
const NEW = 'NagomiAcceso2026';   // cumple la política (>=12, mayúscula, minúscula y dígito) y se teclea fácil

async function tokenOf(user, pass) {
  const r = await fetch(base + '/connect/token', {
    method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ grant_type: 'password', username: user, password: pass }),
  });
  const t = await r.text();
  return { status: r.status, body: t };
}

const current = await tokenOf('e2e-admin', 'E2eAdminSegura2026!');
console.log('login con la contraseña actual:', current.status);
if (current.status !== 200) { console.log(current.body.slice(0, 200)); process.exit(1); }
const token = JSON.parse(current.body).access_token;
const H = { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token };

const users = await (await fetch(base + '/api/admin/users', { headers: H })).json();
const list = Array.isArray(users) ? users : (users.items ?? []);
console.log('cuentas:', JSON.stringify(list.map((u) => `${u.email} (${u.roles}) activo=${u.isActive}`)));
// El usuario se llama e2e-admin pero su correo es e2e@empresa.es: se localiza por correo.
const target = list.find((u) => u.email === 'e2e@empresa.es');
console.log('usuario:', target?.id, target?.userName, '| activo:', target?.isActive);

const reset = await fetch(base + `/api/admin/users/${target.id}`, {
  method: 'PUT', headers: H,
  body: JSON.stringify({ displayName: target.displayName ?? 'E2E Admin', userName: 'e2e-admin', email: target.email, password: NEW, isActive: true, roles: target.roles }),
});
console.log('reset:', reset.status, (await reset.text()).slice(0, 160).replace(/\s+/g, ' '));

const after = await tokenOf('e2e-admin', NEW);
console.log('login con la NUEVA contraseña:', after.status, after.status === 200 ? 'OK' : after.body.slice(0, 160));
console.log('usuario a teclear: e2e-admin');
console.log('contraseña nueva:', NEW);