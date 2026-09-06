"""E2E probe: Nagomi patients feature against the live Docker stack.
Login as the real admin (from .env), then exercises patients CRUD + search + ensure.
Reads NAGOMI_ADMIN_EMAIL/NAGOMI_ADMIN_PASSWORD from .env. Exit 0 iff every check passes.
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

def api(method, path, token=None, payload=None):
    req = urllib.request.Request(BASE + path, method=method)
    if token:
        req.add_header("Authorization", "Bearer " + token)
    if payload is not None:
        data = json.dumps(payload).encode()
        req.data = data
        req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            raw = resp.read().decode()
            return resp.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode(errors="replace")[:500]

def check(name, ok, detail=""):
    print(("PASS " if ok else "FAIL ") + name + (f" :: {detail}" if detail and not ok else ""))
    return ok

results = []
email = env_value("NAGOMI_ADMIN_EMAIL") or "admin@nagomi.local"
password = env_value("NAGOMI_ADMIN_PASSWORD")
if not password:
    print("FAIL: NAGOMI_ADMIN_PASSWORD not found in .env"); sys.exit(1)

import os as _os
_suffix = _os.urandom(3).hex().upper()
DOC1 = f"E2E-{_suffix}"
DOC2 = f"E2E2-{_suffix}"

tok = post_form("/connect/token", {
    "grant_type": "password", "username": email, "password": password})
if "error" in tok:
    print("FAIL: login ::", tok); sys.exit(1)
token = tok["access_token"]
results.append(check("login admin", bool(token)))

# 1. Create a patient
status, created = api("POST", "/api/admin/patients", token, {
    "firstName": "E2E", "lastName": "Paciente", "documentNumber": DOC1,
    "phone": "600000000"})
results.append(check("create patient 201", status == 201 and (created or {}).get("publicId", "").startswith("PAT-"),
                     f"status={status}"))

# 2. Duplicate document rejected
status, _ = api("POST", "/api/admin/patients", token, {
    "firstName": "E2E", "lastName": "Dup", "documentNumber": DOC1})
results.append(check("duplicate document 400", status == 400, f"status={status}"))

# 3. List includes the patient
status, lst = api("GET", "/api/admin/patients", token)
results.append(check("list patients", status == 200 and any(p.get("documentNumber") == DOC1 for p in (lst or [])),
                     f"status={status}"))

# 4. Search by document (web endpoint, auth web)
status, found = api("GET", f"/api/patients/search?q={DOC1}", token)
results.append(check("search by document", status == 200 and any(p.get("lastName") == "Paciente" for p in (found or [])),
                     f"status={status}"))

# 5. Ensure reuses by document (idempotent)
status, ensured = api("POST", "/api/patients/ensure", token, {
    "firstName": "E2E", "lastName": "Paciente", "documentNumber": DOC1})
results.append(check("ensure idempotent reuses", status == 200 and ensured and ensured.get("id") == created.get("id"),
                     f"status={status}"))

# 6. Ensure creates when document is new
status, ensured2 = api("POST", "/api/patients/ensure", token, {
    "firstName": "E2E", "lastName": "Nuevo", "documentNumber": DOC2})
results.append(check("ensure creates new", status == 200 and ensured2 and ensured2.get("publicId", "").startswith("PAT-"),
                     f"status={status}"))

# 7. Update
status, updated = api("PUT", f"/api/admin/patients/{created['id']}", token, {
    "firstName": "E2E", "lastName": "Paciente Actualizado", "documentNumber": DOC1})
results.append(check("update patient", status == 200 and updated and updated.get("lastName") == "Paciente Actualizado",
                     f"status={status}"))

# 8. Delete (soft)
status, _ = api("DELETE", f"/api/admin/patients/{created['id']}", token)
results.append(check("delete patient 204", status == 204, f"status={status}"))

# 9. Hidden from default list after delete
status, lst = api("GET", "/api/admin/patients", token)
results.append(check("deleted hidden by default", status == 200 and not any(p.get("id") == created["id"] for p in (lst or [])),
                     f"status={status}"))

# 10. Role gate: /api/admin/patients requires admin
operador = post_form("/connect/token", {
    "grant_type": "password", "username": env_value("NAGOMI_OPERADOR_EMAIL") or "operador@nagomi.local",
    "password": env_value("NAGOMI_OPERADOR_PASSWORD") or "Operador1234"})
if "error" in operador:
    results.append(check("operador login (skip gate)", False, str(operador)))
else:
    status, _ = api("GET", "/api/admin/patients", operador["access_token"])
    results.append(check("admin gate 403 for default role", status == 403, f"status={status}"))

ok = all(results)
print(f"\n{'ALL PASS' if ok else 'SOME FAILED'} ({sum(results)}/{len(results)})")
sys.exit(0 if ok else 1)
