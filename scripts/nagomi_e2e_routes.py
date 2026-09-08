"""E2E: collective routes on the live stack. Completes onboarding if the DB is still at the
bootstrap state (admin/Admin), then creates two active journeys for today, groups them into a
route, assigns a vehicle, and completes the route. Leaves the app usable with the created admin.
"""
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request
import datetime

BASE = os.environ.get("NAGOMI_BASE", "http://localhost:8080")

ADMIN_USER = "e2e-admin"
ADMIN_PASS = "E2eAdminSegura2026!"
ADMIN_EMAIL = "e2e@empresa.es"

def api(method, path, token=None, payload=None):
    req = urllib.request.Request(BASE + path, method=method)
    if token:
        req.add_header("Authorization", "Bearer " + token)
    if payload is not None:
        req.data = json.dumps(payload).encode()
        req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            raw = resp.read().decode()
            return resp.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode(errors="replace")[:400]

def login(user, password):
    body = urllib.parse.urlencode({"grant_type": "password", "username": user, "password": password}).encode()
    req = urllib.request.Request(BASE + "/connect/token", data=body, method="POST")
    req.add_header("Content-Type", "application/x-www-form-urlencoded")
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            return resp.status, json.load(resp)
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode(errors="replace")[:300]

def check(name, ok, detail=""):
    print(("PASS " if ok else "FAIL ") + name + (f" :: {detail}" if detail and not ok else ""))
    return ok

results = []

# 1. Login as e2e admin or complete onboarding if bootstrap is still active.
status, body = login(ADMIN_USER, ADMIN_PASS)
if status != 200:
    s, bt = login("admin", "Admin")
    if s != 200:
        print("FAIL: neither e2e admin nor bootstrap admin available"); sys.exit(1)
    token = bt["access_token"]
    s2, _ = api("POST", "/api/auth/onboarding", token, {
        "displayName": "E2E Admin", "email": ADMIN_EMAIL, "userName": ADMIN_USER, "password": ADMIN_PASS})
    if s2 != 200:
        print(f"FAIL: onboarding {s2}"); sys.exit(1)
    status, body = login(ADMIN_USER, ADMIN_PASS)

if status != 200 or "access_token" not in body:
    print("FAIL: admin login"); sys.exit(1)
token = body["access_token"]
results.append(check("admin session", True))

# 2. Create a vehicle for assignment.
suffix = os.urandom(3).hex()
status, vehicle = api("POST", "/api/admin/vehicles", token, {"name": f"AMB {suffix}", "code": f"AMB-{suffix[:3]}"})
results.append(check("create vehicle", status == 201, f"status={status}"))
vid = vehicle["id"]

# 3. Create + submit two one-off journeys today.
today = datetime.date.today().isoformat()
def make_journey(name):
    draft = {"patient": {"firstName": name, "lastName": "Colectivo"}, "reason": {"code": "CONSULTA", "description": "Consulta externa"},
             "defaultOrigin": {"type": "HealthcareFacility", "name": "Hospital A", "address": "Calle 1"},
             "defaultDestination": {"type": "HealthcareFacility", "name": "Hospital B", "address": "Calle 2"},
             "requirements": {"mobility": "Autonomous"}}
    s, req = api("POST", "/api/transport-requests/drafts", token, draft)
    appt = (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(hours=2)).isoformat()
    s2, _ = api("POST", f"/api/transport-requests/{req['id']}/submit/one-off", token,
                {"outbound": {"appointmentAt": appt, "scheduledStartAt": None, "pickupTimePending": False}, "return": None})
    s3, detail = api("GET", f"/api/transport-requests/{req['id']}", token)
    return detail["journeyRecords"][0]["id"]

j1 = make_journey(f"Ana {suffix[:2]}")
j2 = make_journey(f"Luis {suffix[2:4]}")
results.append(check("created two journeys", bool(j1) and bool(j2)))

# 4. Create the collective route.
status, route = api("POST", "/api/routes", token, {"serviceDate": today, "journeyIds": [j1, j2]})
results.append(check("create route RUT-", status == 201 and route.get("publicId", "").startswith("RUT-") and len(route.get("stops", [])) == 2,
                     f"status={status} body={str(route)[:200]}"))
rid = route["id"]

# 5. Assign vehicle + complete.
status, _ = api("PUT", f"/api/routes/{rid}/vehicle", token, {"vehicleId": vid})
results.append(check("assign vehicle", status == 200, f"status={status}"))
status, completed = api("POST", f"/api/routes/{rid}/complete", token)
results.append(check("complete route", status == 200 and completed.get("status") == 2, f"status={status}"))

# 6. List by date shows it.
status, lst = api("GET", f"/api/routes?date={today}", token)
results.append(check("list by date", status == 200 and any(r.get("id") == rid for r in (lst or [])), f"status={status}"))

ok = all(results)
print(f"\n{'ALL PASS' if ok else 'SOME FAILED'} ({sum(results)}/{len(results)})")
print(f"\nAdmin creado para la instancia: {ADMIN_USER} / {ADMIN_PASS} ({ADMIN_EMAIL})")
sys.exit(0 if ok else 1)
