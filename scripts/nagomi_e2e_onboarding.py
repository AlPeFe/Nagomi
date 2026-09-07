"""E2E probe: first-run onboarding state on the live stack (does NOT complete onboarding —
leaves it for the user to experience in the browser). Verifies:
1. Bootstrap admin / Admin can log in.
2. /api/auth/me reports onboardingRequired=true.
3. A non-onboarding endpoint (operations) returns 403 for the bootstrap.
"""
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request

BASE = os.environ.get("NAGOMI_BASE", "http://localhost:8080")

def post_form(path, data):
    body = urllib.parse.urlencode(data).encode()
    req = urllib.request.Request(BASE + path, data=body, method="POST")
    req.add_header("Content-Type", "application/x-www-form-urlencoded")
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            return resp.status, json.load(resp)
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode(errors="replace")[:400]

def api(method, path, token=None):
    req = urllib.request.Request(BASE + path, method=method)
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            raw = resp.read().decode()
            return resp.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode(errors="replace")[:400]

def check(name, ok, detail=""):
    print(("PASS " if ok else "FAIL ") + name + (f" :: {detail}" if detail and not ok else ""))
    return ok

results = []

# 1. Bootstrap login (admin / Admin)
status, body = post_form("/connect/token", {"grant_type": "password", "username": "admin", "password": "Admin"})
token = body.get("access_token") if isinstance(body, dict) else None
results.append(check("bootstrap login admin/Admin", status == 200 and bool(token), f"status={status} body={str(body)[:150]}"))

if token:
    # 2. /me reports onboarding required
    status, me = api("GET", "/api/auth/me", token)
    ok = status == 200 and me is not None and me.get("onboardingRequired") is True
    results.append(check("me.onboardingRequired=true", ok, f"status={status} me={str(me)[:200]}"))

    # 3. Operations blocked for bootstrap
    status, _ = api("GET", "/api/operations/journeys", token)
    results.append(check("operations 403 for bootstrap", status == 403, f"status={status}"))

ok = all(results)
print(f"\n{'ALL PASS' if ok else 'SOME FAILED'} ({sum(results)}/{len(results)})")
print("\nSiguiente paso (en el navegador): http://localhost:8080 -> admin / Admin -> 'Primera configuración'.")
sys.exit(0 if ok else 1)
