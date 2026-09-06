"""E2E probe: Nagomi vehicle type + driver features against the live Docker stack.
Login as the real admin, create a vehicle with a type, create+submit a request today,
assign vehicle + driver to its journey, verify via coordination and detail.
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

tok = post_form("/connect/token", {"grant_type": "password", "username": email, "password": password})
if "error" in tok:
    print("FAIL: login ::", tok); sys.exit(1)
token = tok["access_token"]
results.append(check("login admin", bool(token)))

# 1. Create vehicle with type Sva
suffix = os.urandom(3).hex()
status, vehicle = api("POST", "/api/admin/vehicles", token, {
    "name": f"SVA E2E {suffix}", "code": f"SVA-{suffix.upper()}",
    "externalCode": f"EXT-{suffix}", "vehicleType": "Sva"})
results.append(check("create vehicle with type", status == 201 and (vehicle or {}).get("vehicleType") == 1,
                     f"status={status} body={vehicle}"))
vid = vehicle["id"]

# 2. List reflects the type
status, lst = api("GET", "/api/admin/vehicles", token)
results.append(check("list has vehicleType", status == 200 and any(
    v.get("id") == vid and v.get("vehicleType") == 1 for v in (lst or [])), f"status={status}"))

# 3. Create + submit a one-off request for today (so it lands in coordination)
status, request = api("POST", "/api/transport-requests/drafts", token, {
    "patient": {"firstName": "Mapa", "lastName": "E2E", "documentNumber": f"E2E-{suffix.upper()}"},
    "reason": {"code": "CONSULTA", "description": "Consulta externa"},
    "defaultOrigin": {"type": "HealthcareFacility", "name": "Hospital A", "address": "Calle 1"},
    "defaultDestination": {"type": "HealthcareFacility", "name": "Hospital B", "address": "Calle 2"},
    "requirements": {"mobility": "Autonomous"}})
results.append(check("create draft", status == 201, f"status={status} body={request}"))
rid = request["id"]

import datetime
appt = (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(hours=2)).isoformat()
status, submitted = api("POST", f"/api/transport-requests/{rid}/submit/one-off", token, {
    "outbound": {"appointmentAt": appt, "scheduledStartAt": None, "pickupTimePending": False},
    "return": None})
results.append(check("submit one-off", status == 200, f"status={status} body={submitted}"))

# 4. Find the journey in coordination and assign vehicle + driver
status, coord = api("GET", "/api/coordination", token)
rows = [r for r in (coord or []) if r.get("requestId") == rid]
results.append(check("journey in coordination", status == 200 and len(rows) == 1,
                     f"status={status} rows={len(rows)}"))
if rows:
    jid = rows[0]["journeyId"]
    status, _ = api("PUT", f"/api/journeys/{jid}/vehicle", token, {"vehicleId": vid})
    results.append(check("assign vehicle", status == 200, f"status={status}"))
    status, driver = api("PUT", f"/api/journeys/{jid}/driver", token, {"driverName": "Carlos Ruiz"})
    results.append(check("assign driver", status == 200 and driver == "Carlos Ruiz", f"status={status} driver={driver}"))

    status, coord2 = api("GET", "/api/coordination", token)
    row = next((r for r in (coord2 or []) if r.get("requestId") == rid), None)
    results.append(check("coordination shows vehicle+driver",
                         row is not None and row.get("vehicleId") == vid and row.get("driverName") == "Carlos Ruiz",
                         f"row={row}"))

# 5. Clear driver
if rows:
    status, cleared = api("PUT", f"/api/journeys/{rows[0]['journeyId']}/driver", token, {"driverName": ""})
    results.append(check("clear driver", status == 200 and cleared is None, f"status={status} cleared={cleared}"))

ok = all(results)
print(f"\n{'ALL PASS' if ok else 'SOME FAILED'} ({sum(results)}/{len(results)})")
sys.exit(0 if ok else 1)
