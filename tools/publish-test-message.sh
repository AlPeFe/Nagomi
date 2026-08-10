#!/usr/bin/env bash
# Publica un mensaje de prueba en el exchange real de Nagomi vía HTTP Management API.
# Útil para probar el consumidor de un proveedor sin tocar el stack.
#
# Uso:
#   ./tools/publish-test-message.sh [queue] [messageType] [entityPublicId] [contractCode]
#   ./tools/publish-test-message.sh prov.test.ambulancias
#   ./tools/publish-test-message.sh prov.test.ambulancias TransportRequestCreated REQ-2026-000042 CTR-MAD-01
#
# Lee credenciales del .env (RABBITMQ_USER / RABBITMQ_PASSWORD / RABBITMQ_VHOST).
# Requiere el management API expuesto en el host:
#   docker compose -f docker-compose.yml -f rabbit-local-override.yml up -d rabbitmq
# (127.0.0.1:15672; ver docs/development-guide.md §5).
set -euo pipefail

cd "$(dirname "$0")/.."
[ -f .env ] || { echo "No hay .env — ejecuta ./nagomi.sh up primero" >&2; exit 1; }

get_env() { grep -E "^$1=" .env | head -1 | cut -d= -f2- ; }

RABBIT_USER="$(get_env RABBITMQ_USER)"
RABBIT_PASSWORD="$(get_env RABBITMQ_PASSWORD)"
RABBIT_VHOST="$(get_env RABBITMQ_VHOST)"
MGMT_URL="${RABBIT_MGMT_URL:-http://127.0.0.1:15672}"

QUEUE="${1:-prov.test.ambulancias}"
MESSAGE_TYPE="${2:-TransportRequestCreated}"
ENTITY_PUBLIC_ID="${3:-REQ-2026-000041}"
CONTRACT_CODE="${4:-CTR-MAD-01}"
RETRIEVAL_URL="${5:-/api/provider/requests/$ENTITY_PUBLIC_ID}"
TIMESTAMP="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
MSG_ID="$(python -c 'import uuid; print(uuid.uuid4())')"
CORR_ID="$(python -c 'import uuid; print(uuid.uuid4())')"

BODY="$(python - "$QUEUE" "$MESSAGE_TYPE" "$ENTITY_PUBLIC_ID" "$CONTRACT_CODE" "$TIMESTAMP" "$RETRIEVAL_URL" "$MSG_ID" "$CORR_ID" <<'PY'
import json, sys
queue, mtype, eid, contract, ts, url, mid, cid = sys.argv[1:9]
payload = {
    "messageId": mid,
    "messageType": mtype,
    "entityPublicId": eid,
    "contractCode": contract,
    "timestamp": ts,
    "retrievalUrl": url,
}
body = {
    "properties": {
        "delivery_mode": 2,
        "content_type": "application/json",
        "message_id": mid,
        "correlation_id": cid,
        "type": mtype,
    },
    "routing_key": queue,
    "payload": json.dumps(payload),
    "payload_encoding": "string",
}
print(json.dumps(body))
PY
)"

echo "→ cola: $QUEUE · type: $MESSAGE_TYPE · entidad: $ENTITY_PUBLIC_ID"
echo "→ messageId: $MSG_ID"

RESP="$(curl -s --max-time 10 -u "$RABBIT_USER:$RABBIT_PASSWORD" \
  -H "content-type: application/json" \
  -d "$BODY" \
  "$MGMT_URL/api/exchanges/$RABBIT_VHOST/nagomi.provider.notifications/publish")"

if printf '%s' "$RESP" | grep -q '"routed":true'; then
  echo "OK → mensaje en cola (puedes consumirlo con tools/rabbit-consume en :8095)"
else
  echo "FALLO al publicar: $RESP" >&2
  exit 1
fi
