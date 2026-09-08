// SignalR dispatch isolation probe (run from repo tools/ against the Docker stack).
// Verifies the core requirement: when work is assigned to vehicle A, ONLY vehicle A's
// group receives the notification — vehicle B (also connected) must NOT receive it.
import * as signalR from '@microsoft/signalr'

const BASE = process.env.NAGOMI_BASE || 'http://localhost:8080'
const ADMIN_USER = process.env.E2E_ADMIN_USER || 'e2e-admin'
const ADMIN_PASS = process.env.E2E_ADMIN_PASS || 'E2eAdminSegura2026!'

async function api(method, path, token, payload) {
  const res = await fetch(BASE + path, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: payload ? JSON.stringify(payload) : undefined,
  })
  const text = await res.text()
  return { status: res.status, body: text ? JSON.parse(text) : null }
}

async function login(user, password) {
  const form = new URLSearchParams({ grant_type: 'password', username: user, password: password })
  const res = await fetch(BASE + '/connect/token', { method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, body: form })
  const body = await res.json()
  if (!body.access_token) throw new Error(`login failed for ${user}: ${JSON.stringify(body)}`)
  return body.access_token
}

function connect(token, vehicleCode) {
  const conn = new signalR.HubConnectionBuilder()
    .withUrl(`${BASE}/hubs/dispatch`, {
      accessTokenFactory: () => token,
      transport: signalR.HttpTransportType.LongPolling, // WebSockets can be flaky through nginx; LongPolling is reliable for the probe
    })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build()
  const received = []
  conn.on('WorkAssigned', (n) => received.push(n))
  return { conn, received, vehicleCode }
}

const waitFor = (ms) => new Promise((r) => setTimeout(r, ms))

async function main() {
  const token = await login(ADMIN_USER, ADMIN_PASS)
  console.log('PASS admin login')

  // Provision two vehicles.
  const suffix = Math.random().toString(36).slice(2, 6).toUpperCase()
  const vA = (await api('POST', '/api/admin/vehicles', token, { name: `SigA ${suffix}`, code: `VHI-A${suffix}` })).body
  const vB = (await api('POST', '/api/admin/vehicles', token, { name: `SigB ${suffix}`, code: `VHI-B${suffix}` })).body
  console.log(`PASS vehicles ${vA.publicId} / ${vB.publicId}`)

  // Both vehicles connect and subscribe to their own group.
  const a = connect(token, vA.publicId)
  const b = connect(token, vB.publicId)
  await a.conn.start()
  await b.conn.start()
  await a.conn.invoke('SubscribeVehicle', vA.publicId)
  await b.conn.invoke('SubscribeVehicle', vB.publicId)
  console.log('PASS both vehicles subscribed to their own groups')

  // Create one journey today and assign it ONLY to vehicle A.
  const draft = {
    patient: { firstName: 'Iso', lastName: 'Test' },
    reason: { code: 'CONSULTA', description: 'Consulta externa' },
    defaultOrigin: { type: 'HealthcareFacility', name: 'H A', address: 'C 1' },
    defaultDestination: { type: 'HealthcareFacility', name: 'H B', address: 'C 2' },
    requirements: { mobility: 'Autonomous' },
  }
  const req = (await api('POST', '/api/transport-requests/drafts', token, draft)).body
  const appt = new Date(Date.now() + 2 * 3600 * 1000).toISOString()
  await api('POST', `/api/transport-requests/${req.id}/submit/one-off`, token,
    { outbound: { appointmentAt: appt, scheduledStartAt: null, pickupTimePending: false }, return: null })
  const detail = (await api('GET', `/api/transport-requests/${req.id}`, token)).body
  const journeyId = detail.journeyRecords[0].id
  console.log('PASS journey ready')

  const assign = await api('PUT', `/api/journeys/${journeyId}/vehicle`, token, { vehicleId: vA.id })
  if (assign.status !== 200) throw new Error(`assignment failed: ${assign.status} ${JSON.stringify(assign.body)}`)
  console.log(`PASS journey assigned to ${vA.publicId}`)

  // Wait briefly for the push to arrive.
  await waitFor(2500)

  const aGot = a.received.filter((n) => n.vehicleCode === vA.publicId)
  const bGot = b.received.length
  const ok = aGot.length >= 1 && bGot === 0

  console.log(`A received: ${aGot.length} (${aGot.length ? aGot[0].kind + ' ' + aGot[0].publicId : 'none'})`)
  console.log(`B received: ${bGot} (must be 0)`)
  console.log(ok ? '\nALL PASS — assignment notified ONLY to the adjudicated vehicle' : '\nFAIL — isolation broken')

  await a.conn.stop().catch(() => {})
  await b.conn.stop().catch(() => {})
  process.exit(ok ? 0 : 1)
}

main().catch((err) => { console.error('FAIL', err); process.exit(1) })
