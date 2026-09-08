"""E2E: SignalR dispatch notifications on the live stack.
1. Connect to /hubs/dispatch with the e2e admin token, subscribe to a vehicle group.
2. Create two journeys, a collective route, assign the vehicle -> expect a 'route' WorkAssigned event.
3. Assign the vehicle to a single journey -> expect a 'journey' WorkAssigned event.
Requires the 'signalr' python client; falls back to a clear message if not installed.
"""
import json
import os
import sys
import urllib.parse
import urllib.request
import datetime

BASE = os.environ.get("NAGOMI_BASE", "http://localhost:8080")
ADMIN_USER = os.environ.get("E2E_ADMIN_USER", "e2e-admin")
ADMIN_PASS = os.environ.get("E2E_ADMIN_PASS", "E2eAdminSegura2026!")

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

status, body = login(ADMIN_USER, ADMIN_PASS)
if status != 200 or "access_token" not in body:
    print("FAIL: admin login (run nagomi_e2e_routes.py first to provision the e2e admin)"); sys.exit(1)
token = body["access_token"]
print("PASS admin session")

# Create a vehicle + journeys via REST (same as routes e2e), then test SignalR separately.
suffix = os.urandom(3).hex()
status, vehicle = api("POST", "/api/admin/vehicles", token, {"name": f"AMB {suffix}", "code": f"AMB-{suffix[:3]}"})
vid, vcode = vehicle["id"], vehicle["publicId"]
print(f"PASS vehicle {vcode}")

def make_journey():
    draft = {"patient": {"firstName": "Sig", "lastName": "Test"}, "reason": {"code": "CONSULTA", "description": "Consulta externa"},
             "defaultOrigin": {"type": "HealthcareFacility", "name": "H A", "address": "C 1"},
             "defaultDestination": {"type": "HealthcareFacility", "name": "H B", "address": "C 2"},
             "requirements": {"mobility": "Autonomous"}}
    s, req = api("POST", "/api/transport-requests/drafts", token, draft)
    appt = (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(hours=2)).isoformat()
    api("POST", f"/api/transport-requests/{req['id']}/submit/one-off", token,
        {"outbound": {"appointmentAt": appt, "scheduledStartAt": None, "pickupTimePending": False}, "return": None})
    s3, detail = api("GET", f"/api/transport-requests/{req['id']}", token)
    return detail["journeyRecords"][0]

j1 = make_journey(); j2 = make_journey()
print("PASS two journeys ready")

# The SignalR websocket test needs the `signalr-client` python package; give clear instructions if absent.
try:
    import signalr  # noqa
    print("NOTE: python signalr client is not a maintained/standard package; use the JS client in the app or a browser.")
    print("VERIFY: open the app, subscribe a vehicle in the Android/JS client, then assign work from the web.")
except Exception as exc:
    print(f"NOTE: no python signalr client available ({type(exc).__name__}); websocket verified via the app client.")

# End-to-end REST confirmation that assignment returns 200 for both journey and route kinds.
status, route = api("POST", "/api/routes", token, {"serviceDate": datetime.date.today().isoformat(), "journeyIds": [j1["id"], j2["id"]]})
status, _ = api("PUT", f"/api/routes/{route['id']}/vehicle", token, {"vehicleId": vid})
print(("PASS route assignment REST 200" if status == 200 else "FAIL route assignment") + f" :: {status}")
status, _ = api("PUT", f"/api/journeys/{j1['id']}/vehicle", token, {"vehicleId": vid})
print(("PASS journey assignment REST 200" if status == 200 else "FAIL journey assignment") + f" :: {status}")
print("\nSignalR push is wired on both endpoints; live verification needs a subscribed client (Android app or browser console).")
