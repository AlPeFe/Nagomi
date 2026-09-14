// Verificación e2e del asistente contra la instancia real de Nagomi.
// 1) login 2) pregunta al chat 3) limpia la contraseña temporal y deja el estado final.
const base = 'http://localhost:8080';

async function main() {
  const login = await fetch(base + '/connect/token', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({ grant_type: 'password', username: 'e2e-admin', password: 'E2eAdminSegura2026!' }),
  });
  const { access_token: token } = await login.json();
  if (!token) throw new Error('sin token');
  const H = { 'Content-Type': 'application/json', Authorization: 'Bearer ' + token };

  // Estado de la configuración de IA
  console.log('config IA:', JSON.stringify(await (await fetch(base + '/api/admin/tenant/ai', { headers: H })).json()));

  // Prueba de conexión: debe validar que la respuesta sea de verdad una completion
  const probe = await fetch(base + '/api/admin/tenant/ai/test', {
    method: 'POST', headers: H, body: JSON.stringify({}),
  });
  console.log('prueba conexion:', JSON.stringify(await probe.json()));

  // Pregunta real al asistente (mismo endpoint que usa el widget)
  const chat = await fetch(base + '/api/help-chat/messages', {
    method: 'POST',
    headers: H,
    body: JSON.stringify({ message: 'En una frase, para que sirve el menu de Operacion diaria?', history: [] }),
  });
  const body = await chat.text();
  console.log('chat HTTP', chat.status, '->', body.slice(0, 600));

  // Limpieza: la contraseña de prueba no debe quedarse guardada.
  const clean = await fetch(base + '/api/admin/tenant/ai', {
    method: 'PUT',
    headers: H,
    body: JSON.stringify({
      enabled: true, provider: 'hermes', baseUrl: 'http://192.168.31.223:9119/v1',
      model: 'hermes', username: 'alex', clearPassword: true, enableTools: true,
    }),
  });
  const final = await clean.json();
  console.log('final:', JSON.stringify(final));
  console.log('contrasena temporal borrada:', final.hasPassword === false);
}

main().catch((error) => { console.error('FALLO:', error.message); process.exitCode = 1; });
