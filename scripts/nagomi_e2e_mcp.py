"""E2E probe: Nagomi MCP endpoint (/mcp, streamable HTTP) against the live stack.
1. POST /mcp without token -> 401.
2. Login as admin, initialize handshake with Bearer -> ok.
3. tools/list -> the 6 domain tools present.
4. tools/call buscar_vehiculos -> fleet rows.
"""
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request

BASE = os.environ.get("NAGOMI_BASE", "http://localhost:8080")
ENV_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".env")

def env_value(key):
    with open(ENV_PATH, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if line.startswith(key + "="):
                return line.split("=", 1)[1].strip().strip('"')
    return None

def post_form(path, data):
    body = urllib.parse.urlencode(data).encode()
    req = urllib.request.Request(BASE + path, data=body, method="POST")
    req.add_header("Content-Type", "application/x-www-form-urlencoded")
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            return json.load(resp)
    except urllib.error.HTTPError as e:
        return {"error": e.code, "body": e.read().decode(errors="replace")[:400]}

def mcp(token, payload):
    req = urllib.request.Request(BASE + "/mcp", method="POST")
    req.add_header("Content-Type", "application/json")
    req.add_header("Accept", "application/json, text/event-stream")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    req.data = json.dumps(payload).encode()
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            return resp.status, resp.read().decode(errors="replace")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode(errors="replace")[:600]

def check(name, ok, detail=""):
    print(("PASS " if ok else "FAIL ") + name + (f" :: {detail}" if detail and not ok else ""))
    return ok

results = []
email = env_value("NAGOMI_ADMIN_EMAIL") or "admin@nagomi.local"
password = env_value("NAGOMI_ADMIN_PASSWORD")
if not password:
    print("FAIL: NAGOMI_ADMIN_PASSWORD not found in .env"); sys.exit(1)

# 1. No token -> 401
status, body = mcp(None, {"jsonrpc": "2.0", "id": 1, "method": "initialize",
    "params": {"protocolVersion": "2025-06-18", "capabilities": {}, "clientInfo": {"name": "probe", "version": "1.0"}}})
results.append(check("mcp without token 401", status in (401, 403), f"status={status} body={body[:120]}"))

# 2. Login + initialize
tok = post_form("/connect/token", {"grant_type": "password", "username": email, "password": password})
if "error" in tok:
    print("FAIL: login ::", tok); sys.exit(1)
token = tok["access_token"]
results.append(check("login admin", bool(token)))

status, body = mcp(token, {"jsonrpc": "2.0", "id": 1, "method": "initialize",
    "params": {"protocolVersion": "2025-06-18", "capabilities": {}, "clientInfo": {"name": "probe", "version": "1.0"}}})
results.append(check("mcp initialize", status == 200 and ("serverInfo" in body or "result" in body),
                     f"status={status} body={body[:200]}"))

# 3. tools/list
status, body = mcp(token, {"jsonrpc": "2.0", "id": 2, "method": "tools/list", "params": {}})
found = all(name in body for name in ["buscar_pacientes", "consultar_paciente", "buscar_solicitudes",
                                      "consultar_solicitud", "listar_coordinacion", "buscar_vehiculos"])
results.append(check("tools/list has 6 tools", status == 200 and found, f"status={status} body={body[:400]}"))

# 4. tools/call buscar_vehiculos
status, body = mcp(token, {"jsonrpc": "2.0", "id": 3, "method": "tools/call",
    "params": {"name": "buscar_vehiculos", "arguments": {}}})
results.append(check("tools/call buscar_vehiculos", status == 200 and "vehiculos" in body.lower() or "nombre" in body.lower(),
                     f"status={status} body={body[:300]}"))

ok = all(results)
print(f"\n{'ALL PASS' if ok else 'SOME FAILED'} ({sum(results)}/{len(results)})")
sys.exit(0 if ok else 1)
