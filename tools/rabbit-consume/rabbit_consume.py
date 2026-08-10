#!/usr/bin/env python3
"""
Mini cliente RabbitMQ para desarrollo: página web con botón "Consumir" que
lee mensajes raw de una cola vía el HTTP Management API de RabbitMQ.

Sirve la página en http://127.0.0.1:8095 y hace de proxy (evita CORS y no
expone credenciales al navegador). Configuración vía variables de entorno:

  RABBIT_MGMT_URL   (default http://127.0.0.1:15672)
  RABBIT_USER       (default nagomi)
  RABBIT_PASSWORD   (obligatoria)
  RABBIT_VHOST      (default nagomi)
  PORT              (default 8095)

Usa solo la stdlib de Python (http.server + urllib), sin dependencias.
"""
from __future__ import annotations

import base64
import json
import os
import urllib.parse
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

MGMT_URL = os.environ.get("RABBIT_MGMT_URL", "http://127.0.0.1:15672").rstrip("/")
USER = os.environ.get("RABBIT_USER", "nagomi")
PASSWORD = os.environ.get("RABBIT_PASSWORD", "")
VHOST = os.environ.get("RABBIT_VHOST", "nagomi")
PORT = int(os.environ.get("PORT", "8095"))

PAGE = """<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>RabbitMQ · Consumidor de prueba</title>
<style>
  :root { --teal:#087e7b; --ink:#172b38; --muted:#60747e; --line:#d4dfe1; --bg:#eef3f4; }
  * { box-sizing: border-box; }
  body { font-family: Inter, ui-sans-serif, system-ui, sans-serif; background: var(--bg); color: var(--ink); margin: 0; padding: 2rem; }
  main { max-width: 1100px; margin: 0 auto; }
  h1 { font-size: 1.5rem; margin: 0 0 .25rem; }
  .sub { color: var(--muted); margin-bottom: 1.5rem; font-size: .9rem; }
  .panel { background: #fff; border: 1px solid var(--line); border-top: 4px solid var(--teal); padding: 1rem 1.25rem; margin-bottom: 1rem; }
  .row { display: flex; gap: .75rem; flex-wrap: wrap; align-items: end; }
  label { display: flex; flex-direction: column; gap: .3rem; font-size: .75rem; font-weight: 700; color: #405965; }
  input, select { min-height: 2.4rem; padding: .5rem .6rem; border: 1px solid #b7c7ca; border-radius: 4px; font: inherit; }
  input[type=number] { width: 90px; }
  select { min-width: 240px; }
  button { min-height: 2.6rem; padding: .55rem 1.1rem; border: 0; border-radius: 5px; font-weight: 700; cursor: pointer; }
  #consume { background: var(--teal); color: #fff; }
  #requeue { background: #fff; color: var(--ink); border: 1px solid #afc1c5; }
  button:disabled { opacity: .5; cursor: not-allowed; }
  .stats { font-size: .8rem; color: var(--muted); margin-top: .6rem; }
  .stats b { color: var(--ink); }
  .msg { margin-bottom: .9rem; border: 1px solid var(--line); border-left: 4px solid var(--teal); background: #fbfdfd; }
  .msg-head { display: flex; gap: 1rem; flex-wrap: wrap; padding: .5rem .8rem; background: #f1f6f6; font-size: .72rem; color: var(--muted); border-bottom: 1px solid var(--line); }
  .msg-head b { color: var(--ink); }
  .msg-body { padding: .8rem; overflow-x: auto; }
  pre { margin: 0; font-size: .8rem; line-height: 1.5; white-space: pre-wrap; word-break: break-word; }
  .empty { color: var(--muted); font-style: italic; padding: .4rem 0; }
  .err { color: #8d2f29; background: #f8e7e5; border: 1px solid #e2b8b3; padding: .6rem .8rem; border-radius: 4px; margin-top: .6rem; font-size: .85rem; white-space: pre-wrap; }
  .empty-state { text-align:center; color: var(--muted); padding: 3rem 1rem; }
  .empty-state strong { display:block; color: var(--ink); font-size: 1.05rem; margin-bottom: .3rem; }
</style>
</head>
<body>
<main>
  <h1>RabbitMQ · Consumidor de prueba</h1>
  <p class="sub">Lee mensajes <b>raw</b> de una cola usando el HTTP Management API. Un botón por mensaje: <b>Consumir</b> (ack) o <b>Reenqueue</b> (vuelve a la cola).</p>

  <section class="panel">
    <div class="row">
      <label>Vhost
        <input id="vhost" value="__VHOST__" spellcheck="false">
      </label>
      <label>Cola
        <select id="queue"><option value="">Cargando colas…</option></select>
      </label>
      <label>Nº mensajes
        <input id="count" type="number" min="1" max="20" value="5">
      </label>
      <button id="consume" type="button">Consumir</button>
      <button id="requeue" type="button">Reenqueue</button>
    </div>
    <div class="stats" id="stats"></div>
    <div id="err" class="err" hidden></div>
  </section>

  <section class="panel" id="results">
    <div class="empty-state" id="placeholder">
      <strong>Sin mensajes todavía</strong>
      Selecciona una cola y pulsa <b>Consumir</b> para leer el raw que devuelve RabbitMQ.
    </div>
  </section>
</main>

<script>
const $ = (id) => document.getElementById(id);
const state = { messages: [], mode: 'ack', queue: '' };

async function api(path, options = {}) {
  const res = await fetch(path, {
    headers: { 'Content-Type': 'application/json' },
    ...options,
  });
  const text = await res.text();
  let data = null;
  try { data = text ? JSON.parse(text) : null; } catch { data = text; }
  if (!res.ok) throw new Error(data && data.detail ? data.detail : (typeof data === 'string' ? data : `HTTP ${res.status} ${res.statusText}`));
  return data;
}

function escapeHtml(s) {
  return String(s).replace(/[&<>"']/g, (c) => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;' }[c]));
}

function pretty(v) {
  if (v === null || v === undefined) return 'null';
  if (typeof v === 'string') {
    try { return JSON.stringify(JSON.parse(v), null, 2); } catch { return v; }
  }
  return JSON.stringify(v, null, 2);
}

async function loadQueues() {
  const vhost = $('vhost').value.trim() || '/';
  $('queue').innerHTML = '<option value="">Cargando…</option>';
  try {
    const queues = await api(`/api/queues?${new URLSearchParams({ vhost })}`);
    $('queue').innerHTML = queues.length
      ? queues.map((q) => `<option value="${escapeHtml(q.name)}">${escapeHtml(q.name)} · ${q.messages} msg · ${q.consumers} consumers</option>`).join('')
      : '<option value="">(sin colas en este vhost)</option>';
    $('stats').innerHTML = `<b>${queues.length}</b> colas en <b>${escapeHtml(vhost)}</b>`;
  } catch (e) {
    $('queue').innerHTML = '<option value="">(error cargando colas)</option>';
    showError(e.message);
  }
}

function showError(message) {
  $('err').textContent = message;
  $('err').hidden = false;
}

function render() {
  const results = $('results');
  if (!state.messages.length) {
    results.innerHTML = '<div class="empty-state"><strong>Sin mensajes todavía</strong>Selecciona una cola y pulsa <b>Consumir</b>.</div>';
    return;
  }
  results.innerHTML = state.messages.map((m, i) => {
    const payload = pretty(m.payload);
    const props = m.properties && Object.keys(m.properties).length ? pretty(m.properties) : null;
    return `<article class="msg">
      <div class="msg-head">
        <span><b>#${i + 1}</b> cola: <b>${escapeHtml(state.queue)}</b></span>
        <span>exchange: <b>${escapeHtml(m.exchange)}</b></span>
        <span>routing key: <b>${escapeHtml(m.routing_key)}</b></span>
        <span>redelivered: <b>${m.redelivered}</b></span>
        <span>msg count: <b>${m.message_count}</b></span>
        <span>payload bytes: <b>${m.payload_bytes}</b></span>
      </div>
      <div class="msg-body"><pre>${escapeHtml(payload)}</pre></div>
      ${props ? `<div class="msg-body" style="border-top:1px solid var(--line)"><pre>${escapeHtml(props)}</pre></div>` : ''}
    </article>`;
  }).join('');
}

async function consume() {
  const queue = $('queue').value;
  if (!queue) { showError('Selecciona una cola.'); return; }
  state.queue = queue;
  const vhost = $('vhost').value.trim() || '/';
  const count = Math.min(Math.max(parseInt($('count').value, 10) || 1, 1), 20);
  $('consume').disabled = true;
  $('requeue').disabled = true;
  $('err').hidden = true;
  try {
    const body = { count, ackmode: state.mode === 'ack' ? 'ack_requeue_false' : 'reject_requeue_true', encoding: 'auto', truncate: 50000 };
    state.messages = await api(`/api/queues/${encodeURIComponent(vhost)}/${encodeURIComponent(queue)}/get`, {
      method: 'POST',
      body: JSON.stringify(body),
    });
    $('stats').innerHTML = `<b>${state.messages.length}</b> mensaje(s) leídos de <b>${escapeHtml(queue)}</b> · modo: <b>${state.mode === 'ack' ? 'ack (se eliminan)' : 'reenqueue'}</b>`;
    render();
  } catch (e) {
    showError(e.message);
  } finally {
    $('consume').disabled = false;
    $('requeue').disabled = false;
  }
}

$('consume').addEventListener('click', () => { state.mode = 'ack'; consume(); });
$('requeue').addEventListener('click', () => { state.mode = 'requeue'; consume(); });
$('vhost').addEventListener('change', loadQueues);
loadQueues().catch(() => {});
</script>
</body>
</html>
"""


def rabbit_api(path: str, method: str = "GET", body: dict | None = None) -> tuple[int, str]:
    """Proxy a la HTTP Management API de RabbitMQ."""
    url = f"{MGMT_URL}{path}"
    data = json.dumps(body).encode() if body is not None else None
    token = base64.b64encode(f"{USER}:{PASSWORD}".encode()).decode()
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Authorization", f"Basic {token}")
    req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=15) as resp:
            return resp.status, resp.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()
    except Exception as e:  # noqa: BLE001
        return 502, json.dumps({"detail": str(e)})


class Handler(BaseHTTPRequestHandler):
    def _send(self, status: int, body, content_type: str = "application/json; charset=utf-8") -> None:
        if isinstance(body, str):
            body = body.encode()
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self) -> None:  # noqa: N802
        if self.path == "/" or self.path.startswith("/index"):
            self._send(200, PAGE.replace("__VHOST__", VHOST), "text/html; charset=utf-8")
            return
        if self.path.startswith("/api/queues"):
            parsed = urllib.parse.urlparse(self.path)
            params = urllib.parse.parse_qs(parsed.query)
            vhost = params.get("vhost", [VHOST])[0] or "/"
            vhost_enc = urllib.parse.quote(vhost, safe="")
            status, body = rabbit_api(f"/api/queues/{vhost_enc}")
            self._send(status, body.encode())
            return
        self._send(404, json.dumps({"detail": "Not found"}).encode())

    def do_POST(self) -> None:  # noqa: N802
        length = int(self.headers.get("Content-Length", "0"))
        raw = self.rfile.read(length) if length else b"{}"
        try:
            payload = json.loads(raw or b"{}")
        except json.JSONDecodeError:
            payload = {}
        # /api/queues/{vhost}/{queue}/get
        parts = [p for p in self.path.split("/") if p]
        if len(parts) == 5 and parts[0] == "api" and parts[1] == "queues" and parts[4] == "get":
            vhost = urllib.parse.unquote(parts[2])
            queue = urllib.parse.unquote(parts[3])
            vhost_enc = urllib.parse.quote(vhost, safe="")
            queue_enc = urllib.parse.quote(queue, safe="")
            status, body = rabbit_api(
                f"/api/queues/{vhost_enc}/{queue_enc}/get",
                method="POST",
                body=payload,
            )
            self._send(status, body.encode())
            return
        self._send(404, json.dumps({"detail": "Not found"}).encode())

    def log_message(self, fmt: str, *args) -> None:  # noqa: A003
        print(f"[rabbit-consume] {fmt % args}")


def main() -> None:
    if not PASSWORD:
        raise SystemExit("Falta RABBIT_PASSWORD (variable de entorno).")
    server = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    print(f"RabbitMQ consume client → http://127.0.0.1:{PORT}")
    print(f"Management API: {MGMT_URL}  vhost: {VHOST}  user: {USER}")
    server.serve_forever()


if __name__ == "__main__":
    main()
