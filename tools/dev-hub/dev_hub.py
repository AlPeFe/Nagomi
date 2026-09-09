"""Dev hub for Nagomi — small local dashboard with links + live service status.

Stdlib only. Serves:
  GET /            -> index.html (the dashboard)
  GET /api/status  -> JSON { services:[{id,name,ok,detail}], queues:[...], caps:{...} }

Reads RabbitMQ management creds from the repo .env (dev machine only).
Launch:  start.cmd   (or)   python dev_hub.py [--port 8096] [--env ../../.env]
"""
import argparse
import base64
import json
import os
import sys
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_PORT = 8096

parser = argparse.ArgumentParser()
parser.add_argument('--port', type=int, default=int(os.environ.get('PORT', DEFAULT_PORT)))
parser.add_argument('--bind', default=os.environ.get('BIND_ADDRESS', '127.0.0.1'))
parser.add_argument('--env', default=None)
args, _ = parser.parse_known_args()

ENV_FILE = args.env or os.path.join(HERE, '..', '..', '.env')


def load_env(path):
    env = {}
    try:
        with open(path, encoding='utf-8') as fh:
            for line in fh:
                line = line.strip()
                if not line or line.startswith('#') or '=' not in line:
                    continue
                k, _, v = line.partition('=')
                env[k.strip()] = v.strip().strip('"').strip("'")
    except OSError:
        pass
    return env


ENV = load_env(os.path.abspath(ENV_FILE))
MGMT = 'http://127.0.0.1:15672'
MGMT_USER = ENV.get('RABBITMQ_USER', 'nagomi')
MGMT_PASS = ENV.get('RABBITMQ_PASSWORD', '')
MGMT_VHOST = ENV.get('RABBITMQ_VHOST', 'nagomi')


def http_get(url, timeout=4, basic=None, headers=None):
    req = urllib.request.Request(url, headers=headers or {})
    if basic:
        token = base64.b64encode(f'{basic[0]}:{basic[1]}'.encode()).decode()
        req.add_header('Authorization', f'Basic {token}')
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            return resp.status, resp.read()
    except urllib.error.HTTPError as e:
        return e.code, b''
    except Exception:
        return None, b''


def collect_status():
    services = []

    code, body = http_get('http://127.0.0.1:8080/healthz')
    services.append({'id': 'web', 'name': 'Web (frontend :8080)', 'ok': code == 200,
                     'detail': f'HTTP {code}' if code else 'sin respuesta'})

    code, body = http_get('http://127.0.0.1:8080/api/auth/me')
    # 401 esperado: backend vivo pidiendo token
    services.append({'id': 'api', 'name': 'API backend (auth)', 'ok': code in (401, 200),
                     'detail': '401 esperado (vivo)' if code == 401 else f'HTTP {code}'})

    code, body = http_get('http://127.0.0.1:8080/mcp', headers={'Accept': 'application/json, text/event-stream'})
    services.append({'id': 'mcp', 'name': 'MCP server (/mcp)', 'ok': code is not None,
                     'detail': f'HTTP {code} (requiere sesión MCP)' if code else 'sin respuesta'})

    code, body = http_get(f'{MGMT}/api/overview', timeout=5, basic=(MGMT_USER, MGMT_PASS))
    services.append({'id': 'rabbit', 'name': 'RabbitMQ Management (:15672)', 'ok': code == 200,
                     'detail': f'HTTP {code}' if code else 'sin respuesta — usa rabbit-local-override.yml'})

    code, body = http_get('http://127.0.0.1:8095/')
    services.append({'id': 'consume', 'name': 'Visor de colas (:8095)', 'ok': code == 200,
                     'detail': f'HTTP {code}' if code else 'sin respuesta — tools/rabbit-consume'})

    queues = []
    code, body = http_get(f'{MGMT}/api/queues/{urllib.request.quote(MGMT_VHOST, safe="")}?columns=name,messages,messages_ready,messages_unacknowledged,consumers',
                          timeout=5, basic=(MGMT_USER, MGMT_PASS))
    if code == 200:
        try:
            for q in json.loads(body):
                queues.append({'name': q.get('name'), 'messages': q.get('messages', 0),
                               'ready': q.get('messages_ready', 0),
                               'unacked': q.get('messages_unacknowledged', 0),
                               'consumers': q.get('consumers', 0)})
        except Exception:
            queues = []

    return {'services': services, 'queues': queues, 'mgmt': MGMT, 'vhost': MGMT_VHOST}


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def _send(self, code, body, ctype):
        if isinstance(body, str):
            body = body.encode()
        self.send_response(code)
        self.send_header('Content-Type', ctype)
        self.send_header('Content-Length', str(len(body)))
        self.send_header('Cache-Control', 'no-store')
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        if self.path == '/api/status':
            self._send(200, json.dumps(collect_status()), 'application/json')
            return
        if self.path in ('/', '/index.html'):
            try:
                with open(os.path.join(HERE, 'index.html'), encoding='utf-8') as fh:
                    self._send(200, fh.read(), 'text/html; charset=utf-8')
            except OSError:
                self._send(404, 'index.html no encontrado', 'text/plain')
            return
        self._send(404, 'not found', 'text/plain')


if __name__ == '__main__':
    print(f'[dev-hub] http://{args.bind}:{args.port}  (env: {os.path.abspath(ENV_FILE)})')
    ThreadingHTTPServer((args.bind, args.port), Handler).serve_forever()
