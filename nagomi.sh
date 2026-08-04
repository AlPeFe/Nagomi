#!/usr/bin/env bash
# Nagomi — levanta o gestiona todo el stack con un solo comando.
#
#   ./nagomi.sh up        construye y arranca postgres + rabbitmq + backend + frontend
#   ./nagomi.sh down      detiene el stack (los datos persisten)
#   ./nagomi.sh status    estado de los servicios
#   ./nagomi.sh logs      sigue los logs de todos los servicios
#   ./nagomi.sh restart   reinicia los servicios
#
# La primera vez crea .env desde .env.example con contraseñas aleatorias.
# Revisa NAGOMI_ADMIN_EMAIL / NAGOMI_ADMIN_PASSWORD antes de exponer el servicio.

set -euo pipefail
cd "$(dirname "$0")"

ENV_FILE=".env"
COMPOSE=(docker compose)

if [[ ! -f "$ENV_FILE" ]]; then
  echo "→ Creando ${ENV_FILE} desde .env.example con contraseñas aleatorias…"
  cp .env.example "$ENV_FILE"
  sed -i "s/change-me-long-random-postgres-password/$(openssl rand -hex 24)/" "$ENV_FILE"
  sed -i "s/change-me-long-random-rabbitmq-password/$(openssl rand -hex 24)/" "$ENV_FILE"
  sed -i "s/change-me-admin-password-123/$(openssl rand -base64 18 | tr -d '/+=')/" "$ENV_FILE"
  echo "→ ${ENV_FILE} creado."
  echo "  • Admin web inicial: $(grep '^NAGOMI_ADMIN_EMAIL=' "$ENV_FILE" | cut -d= -f2) (contraseña generada)"
  echo "  • Revisa OAUTH_ISSUER si vas a exponer el servicio."
fi

case "${1:-up}" in
  up)
    "${COMPOSE[@]}" up -d --build
    echo "→ Nagomi en http://localhost:$(grep '^FRONTEND_PORT=' "$ENV_FILE" | cut -d= -f2)"
    ;;
  down)
    "${COMPOSE[@]}" down
    ;;
  status)
    "${COMPOSE[@]}" ps
    ;;
  logs)
    "${COMPOSE[@]}" logs -f --tail=100
    ;;
  restart)
    "${COMPOSE[@]}" restart
    ;;
  *)
    echo "Uso: $0 [up|down|status|logs|restart]"
    exit 1
    ;;
esac
