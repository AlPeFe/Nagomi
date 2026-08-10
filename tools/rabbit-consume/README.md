# RabbitMQ consume client (dev tool)

Mini cliente web para **consumir mensajes raw de las colas de RabbitMQ** del
stack de Nagomi. Útil para inspeccionar lo que publica la integración con
proveedores (outbox → Rabbit) sin tocar el código.

## Qué hace

Página en `http://127.0.0.1:8095` con:

- **Selector de vhost y cola** (carga las colas reales del RabbitMQ vía su HTTP Management API).
- **Botón "Consumir"** → lee N mensajes de la cola con `ack` (se eliminan de la cola).
- **Botón "Reenqueue"** → lee N mensajes y los vuelve a poner en la cola (`reject_requeue_true`).
- Muestra el **raw que devuelve RabbitMQ**: payload (formateado si es JSON),
  exchange, routing key, redelivered, message_count, payload_bytes y properties.

Es un proxy Python de stdlib (sin dependencias): el navegador habla con
`127.0.0.1:8095`, y el proxy llama al management API de RabbitMQ. Así no hay
CORS y las credenciales nunca llegan al navegador.

## Requisitos

El management API de RabbitMQ debe ser accesible desde el host. El contenedor
del stack no publica puertos por defecto; para desarrollo:

```sh
docker compose -f docker-compose.yml -f rabbit-local-override.yml up -d rabbitmq
```

Eso expone `127.0.0.1:15672` (solo loopback). Para quitar el puerto después:

```sh
docker compose -f docker-compose.yml up -d rabbitmq
```

## Arrancar

```sh
# Windows (lee credenciales del .env automáticamente)
tools\rabbit-consume\start.cmd

# o a mano, con variables de entorno
export RABBIT_USER=... RABBIT_PASSWORD=... RABBIT_VHOST=nagomi
python tools/rabbit-consume/rabbit_consume.py
```

Abre `http://127.0.0.1:8095`.

## Acceso desde la LAN (móvil / otro PC)

El proxy escucha en `0.0.0.0` por defecto, así que la herramienta es accesible
desde la red local en `http://192.168.31.223:8095`. El management API de
RabbitMQ sigue en loopback: el navegador de la LAN solo ve el proxy, nunca las
credenciales.

La primera vez hay que abrir el puerto en el Firewall de Windows (acepta el
prompt de UAC):

```sh
powershell -NoProfile -Command "Start-Process powershell -Verb RunAs -Wait -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File','C:\Users\alexlocal\projects\Nagomi\tools\rabbit-consume\firewall.ps1'"
```

Para restringir a loopback: `BIND_ADDRESS=127.0.0.1 python tools/rabbit-consume/rabbit_consume.py`.

## Configuración (variables de entorno)

| Variable          | Default             | Descripción                          |
| ----------------- | ------------------- | ------------------------------------ |
| `RABBIT_MGMT_URL` | `http://127.0.0.1:15672` | URL del HTTP Management API    |
| `RABBIT_USER`     | `nagomi`            | Usuario de RabbitMQ                  |
| `RABBIT_PASSWORD` | *(obligatoria)*     | Password de RabbitMQ                 |
| `RABBIT_VHOST`    | `nagomi`            | Vhost por defecto                    |
| `PORT`            | `8095`              | Puerto del cliente web               |
| `BIND_ADDRESS`    | `0.0.0.0`           | Interfaz de escucha (`127.0.0.1` = solo local) |
