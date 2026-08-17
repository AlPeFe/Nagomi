#!/usr/bin/env bash
# E2E: help-chat status + message round-trip against the live Nagomi backend.
cd "$(dirname "$0")/.." || exit 1
PW="NagomiAdmin2026!"
curl -s -X POST http://localhost:8080/connect/token \
  -d "grant_type=password" -d "username=admin@nagomi.local" -d "password=$PW" > tok.txt
TOKEN=$(PYTHONPATH= /c/Python314/python.exe -c "import json;print(json.load(open('tok.txt'))['access_token'])")
echo "=== GET /api/help-chat/status (con token) ==="
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:8080/api/help-chat/status; echo
echo "=== POST /api/help-chat/messages (con token) ==="
curl -s -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"message":"¿cómo creo un traslado?","history":[{"role":"user","content":"¿cómo creo un traslado?"}]}' \
  http://localhost:8080/api/help-chat/messages; echo
echo "=== POST /api/help-chat/messages (sin token) ==="
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:8080/api/help-chat/messages \
  -H 'Content-Type: application/json' -d '{"message":"hola"}'
rm -f tok.txt
