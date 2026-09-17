// Verifica que un reset fallido NO deja la cuenta sin contraseña.
const base = process.argv[2] ?? 'http://localhost:8080';
const USER = 'e2e-admin';
const PASS = process.env.CURRENT_PW;

async function login(pass) {
  const r = await fetch(base + '/connect/token', {
    method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ grant_type: 'password', username: USER, password: pass }),
  });
  return r.status;
}

console.log('1) login antes:', await login(PASS));
const token = (await (await fetch(base + '/connect/token', {
  method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
  body: new URLSearchParams({ grant_type: 'password', username: USER, password: PASS }),
})).json()).access_token;
const H = { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token };

const users = await (await fetch(base + '/api/admin/users', { headers: H })).json();
const me = (Array.isArray(users) ? users : users.items ?? []).find((u) => u.email === 'e2e@empresa.es');

const weak = await fetch(base + `/api/admin/users/${me.id}`, {
  method: 'PUT', headers: H,
  body: JSON.stringify({ displayName: me.displayName, userName: USER, email: me.email, password: 'corta', isActive: true, roles: me.roles }),
});
const body = await weak.text();
console.log('2) reset con contraseña débil:', weak.status, '|', body.slice(0, 120).replace(/\s+/g, ' '));
console.log('3) login después (debe seguir funcionando):', await login(PASS));