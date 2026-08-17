# Nagomi — Manual de usuario y guía técnica

> Coordinación de transporte sanitario para hospitales, clínicas, residencias y
> empresas de ambulancias: solicitudes, trayectos, urgencias y seguimiento de
> principio a fin.

Esta guía está pensada para **quien usa Nagomi a diario** (operadores,
coordinadores y administradores) y también como **punto de entrada técnica**
para quien va a **trabajar sobre el código** o **desplegar** el sistema. Al
final de cada apartado técnico se enlazan las guías detalladas del repositorio.

---

## 1. Qué es Nagomi

Nagomi es un sistema de código abierto para **planificar, coordinar y seguir
transportes de pacientes**. Nace para pequeñas organizaciones que necesitan
control de su actividad diaria sin depender de plataformas externas.

Se organiza en torno a dos ideas centrales:

- **Solicitud de transporte** (`REQ-…`): la petición operativa de mover a un
  paciente. Puede ser **puntual** (una cita concreta) o **recurrente** (diálisis
  semanal, rehabilitación…).
- **Trayecto / jornada** (`JRN-…`): cada movimiento individual. Una solicitud
  con ida y vuelta genera **dos trayectos independientes**, cada uno con su
  fecha, estado, vehículo y notas. Es normal que una pierna se complete y la
  otra se cancele por separado.

A estas dos piezas se añaden **urgencias** (`EMG-…`): traslados de emergencia
registrados con el **punto de incidencia geolocalizado** en un mapa.

---

## 2. Roles y acceso

| Rol | Qué puede hacer |
|---|---|
| **`default`** | Operación diaria: ver y crear solicitudes, consultar trayectos, coordinación y urgencias. |
| **`admin`** | Todo lo anterior, más **Vehículos**, **Identidad** (usuarios y clientes de API) y **Configuración** (capacidades y clientes facturables). |

El acceso es por correo y contraseña. El **primer administrador** se crea al
desplegar a partir de `NAGOMI_ADMIN_EMAIL` / `NAGOMI_ADMIN_PASSWORD`. Los
usuarios adicionales los crea un administrador desde **Identidad**.

> Datos sensibles: los identificadores del paciente (DNI, tarjeta sanitaria)
> solo se muestran en el **detalle** de una solicitud, nunca en los listados
> operativos, y no salen en la exportación CSV.

---

## 3. Revisión de funcionalidades por módulo

### 3.1 Mesa de operaciones — `/trayectos`

Ventana activa (de ayer a mañana) con **actualización automática cada 30 s**.

- Filtros por fechas, estado, dirección (ida/vuelta), proveedor, contrato,
  motivo, municipio de origen/destino, estado de recepción y búsqueda libre
  (solicitud, trayecto, referencia, paciente, documento o teléfono).
- **Exportación CSV** de los trayectos visibles.
- Cada fila muestra origen → destino, estado, vehículo asignado y par de ida/vuelta
  coloreado de forma consistente (un mismo trayecto de vuelta identifica a su ida).

### 3.2 Panel de coordinación — `/coordinacion`

Tablero de flota con tres bandas: **Trabajo actual / Hoy / Mañana**. Permite
**asignar un vehículo a cada trayecto** y ver el estado geolocalizado
(la ida/vuelta y dónde se marcaron los estados, sobre un mapa Leaflet).
Actualización automática cada 30 s.

> Un trayecto solo aparece en coordinación si su fecha operativa está entre
> ayer y mañana (es el tablero de trabajo diario). Para ver todo, usa `/trayectos`.

### 3.3 Solicitudes — `/solicitudes`

Listado de borradores y solicitudes enviadas, con búsqueda por identificador o
paciente. Desde aquí se abre el **detalle** (ruta base, contrato/proveedor,
patrón de recurrencia, trayectos, auditoría y entregas de integración).

**Nueva solicitud** (`/solicitudes/nueva`) guía en 5 secciones:

1. **Paciente y motivo** — datos del paciente (documento y tarjeta sanitaria
   solo en detalle) y motivo del transporte.
2. **Ruta operativa** — origen y destino. Los **centros sanitarios** se eligen
   del catálogo nacional de hospitales; los **domicilios**, por provincia y
   población de España.
3. **Necesidades** — movilidad (autónomo / silla / camilla, excluyentes),
   oxígeno (concentración y flujo), acompañante, personal sanitario,
   aislamiento, bariátrico, ayuda en escaleras.
4. **Programación** — una fecha o recurrente. La ida se planifica por **cita**;
   el inicio previsto se calcula (una hora antes) si se deja vacío. La vuelta
   puede quedar **con hora pendiente**.
5. **Facturación y notas** — **cliente facturable** (a quién se factura; vacío =
   "Yo mismo") y notas para el proveedor / privadas.

Al enviar se genera el/los trayectos. Si el tenant **ejecuta sus propios
traslados**, la solicitud se enruta a su propio contrato (`SELF`); si no, se
crea sin contrato (nada se publica a ninguna cola) y solo aparece en operación.

### 3.4 Urgencias — `/urgencias`

Registro mínimo de traslados de emergencia:

- **Nueva urgencia**: motivo, teléfono de contacto y **punto de incidencia**
  geolocalizado (buscando una dirección o marcando en el mapa).
- Pestaña **Activas** e **Historial** con filtros por estado y rango de fechas.
- Cada registro enlaza a su ubicación en OpenStreetMap y puede cancelarse
  mientras está activo.

Solo está disponible si el tenant tiene la capacidad **"Atención de urgencias"**
activada en Configuración.

### 3.5 Vehículos — `/vehiculos` (admin)

Gestiona la flota propia (`VHC-…`). Cada vehículo tiene un **código interno**
(p. ej. `AMB-01`; si se deja vacío se genera) y un **código externo** opcional
que casa con el `ExternalResourceCode` de los eventos de estado del proveedor
(el enlace "testimonial" que aparece en el mapa). Al eliminarlo no se borra de
los trayectos históricos: solo deja de estar disponible para asignación.

### 3.6 Identidad — `/identidad` (admin)

Sistema central sobre OpenIddict, solo administradores:

- **Usuarios**: crear, cambiar rol, activar/desactivar, restablecer contraseña,
  eliminar. No puedes cambiarte el rol ni borrarte a ti mismo.
- **Clientes de API (M2M)**: credenciales OAuth 2.0 `client_credentials` para
  sistemas externos que consuman la API. El **secreto solo se muestra una vez**
  al crear; puedes **rotar** (invalida los tokens anteriores) o **revocar**.

### 3.7 Configuración del tenant — `/configuracion` (admin)

- **Capacidades operativas** (acumulables, puedes activar varias):
  - *Publicar a proveedores* — modo peticionario: enrutar traslados a contratos externos.
  - *Ejecutar traslados propios* — modo empresa de ambulancias: la instalación se
    registra como proveedor de sí misma (contrato/cola `SELF`).
  - *Atención de urgencias* — expone el módulo de emergencias.
- **Clientes facturables**: organizaciones o particulares a los que se factura
  un traslado (p. ej. una mutua), sin integración ni cola. Activar/desactivar.
- **Colas de RabbitMQ**: estado de las colas de los proveedores (mensajes,
  consumidores) y **inspección no destructiva** del contenido publicado, útil
  para operativa y diagnóstico.

### 3.8 Chat de ayuda (widget)

Botón flotante de ayuda con un asistente (API de chat compatible con OpenAI).
El botón **solo aparece si el despliegue lo ha activado**
(`HELPCHAT_ENABLED=true` + `BaseUrl`/`ApiKey`/`Model`). El texto del sistema y
el historial se envían al proveedor; la clave de API **nunca sale del backend**.

---

## 4. Modelo funcional (clave para entender la aplicación)

| Concepto | Qué es | Ejemplo |
|---|---|---|
| **Cliente facturable** | A **quién se factura** el traslado. Opcional; vacío = te facturas tú. | Mutua Pepe (puede no tener software). |
| **Contrato** | **Quién ejecuta** / cola de RabbitMQ / ruta. Para auto-ejecución es el contrato propio (`SELF`). | Contrato propio de la empresa. |
| **Proveedor** | Sistema que ejecuta el traslado (incluido uno mismo como auto-proveedor). | `SELF` o una empresa externa. |

**Ejecución y facturación son independientes**: puedes facturar a un cliente
externo mientras ejecutas con tu propio contrato (eres la empresa de
ambulancias que factura a MAPFRE), o generar la petición con un contrato
externo que la ejecute (y facturarte a ti). Hoy la UI auto-ejecuta cuando el
tenant tiene esa capacidad; el contrato/proveedor externo se configura por API.

**Estados de un trayecto**: `Scheduled → Activated → EnRouteToOrigin →
ArrivedAtOrigin → PatientOnBoard → EnRouteToDestination →
ArrivedAtDestination → Completed`, más `Cancelled`. Se registran como eventos
con hora, actor y (opcional) geolocalización.

---

## 5. Flujos de trabajo típicos

**Operador de clínica (solo solicita):**
`/solicitudes/nueva` → rellena paciente, ruta, necesidades y programación →
"Revisar y enviar" → sigue el estado en `/trayectos` y en el detalle. Ve a quién
ejecutó y dónde se marcaron los estados (vista testimonial, sin gestionar flota).

**Empresa de ambulancias (ejecuta con su flota):**
Configuración → activa "Ejecutar traslados propios" y "Atención de urgencias".
Gestiona vehículos en `/vehiculos`. Usa `/coordinacion` para asignar la flota a
cada trayecto de hoy/mañana y `/trayectos` para el seguimiento. Los conductores
de campo usan la **app Android NagomiDriver** (identificada por vehículo) para
marcar estados y consumir sus trayectos.

**Emergencia:**
`/urgencias` → "Nueva urgencia" → motivo + punto de incidencia en el mapa →
registrar. Seguimiento en la pestaña Activas.

---

## 6. Stack y arquitectura

| Capa | Tecnología |
|---|---|
| Backend | ASP.NET Core minimal APIs (.NET 10), EF Core + Npgsql |
| Frontend | React 19 + TypeScript, Vite, mini-router propio (sin react-router) |
| Base de datos | PostgreSQL |
| Mensajería | RabbitMQ (solo señal de cambio *at-least-once*) |
| Auth | OpenIddict (client credentials para proveedores + password grant para web) |
| Observabilidad | OpenTelemetry |

RabbitMQ transporta **solo notificaciones ligeras** (identificador, tipo,
`retrievalUrl`) — **nunca** datos del paciente. El detalle se consulta por HTTP
autenticado. La escritura se confirma de forma transaccional con un **outbox**;
si el broker falla se reintenta y, si sigue fallando, queda en estado `Dead`
para republish manual.

Detalle completo: [`docs/development-guide.md`](development-guide.md).

---

## 7. Trabajar sobre el código

```sh
git clone https://github.com/AlPeFe/Nagomi.git   # branch: main
cd Nagomi
./nagomi.sh up        # build + up del stack (postgres + rabbitmq + backend + frontend)
#  ./nagomi.sh down | status | logs | restart
```

- El primer arranque copia `.env.example` → `.env` con contraseñas aleatorias.
- Frontend en `http://localhost:8080`. Admin inicial desde `NAGOMI_ADMIN_EMAIL` / `NAGOMI_ADMIN_PASSWORD`.
- `.env` está en `.gitignore`: **nunca se commitea**.

**Verificar antes de dar algo por hecho:**

```sh
dotnet build Nagomi.slnx -v q --nologo
dotnet test tests/Nagomi.UnitTests/Nagomi.UnitTests.csproj --nologo
dotnet test tests/Nagomi.IntegrationTests/Nagomi.IntegrationTests.csproj --nologo   # necesita Docker
cd frontend && npm ci && npm run build && npm run lint && npm test -- --run
```

Los cambios significativos se escriben primero como **spec OpenSpec**
(`openspec/specs/`) y se mapean a tests. Guía completa de desarrollo,
topología RabbitMQ, outbox y herramientas en
[`docs/development-guide.md`](development-guide.md). La integración con
proveedores (OAuth, REST, idempotencia) en [`docs/provider-integration.md`](provider-integration.md).

---

## 8. Desplegar en producción

```sh
cp .env.example .env
# 1) reemplaza cada change-me por secretos independientes de alta entropía
# 2) pon OAUTH_ISSUER a tu origin HTTPS externo exacto
docker compose config --quiet
docker compose build --pull
docker compose up -d
docker compose ps
```

- El stack escucha en `127.0.0.1:8080` por defecto; pon un **proxy inverso con
  TLS** delante. No expongas `0.0.0.0` sin TLS.
- **Migraciones**: se aplican al arrancar (`Database__MigrateOnStartup=true`).
  Tras un despliegue con `.env` antiguo, usa `${VAR:-default}` en el compose
  para las variables nuevas.
- **Backups**: `pg_dump` en formato custom + restore verificado; los mensajes
  de RabbitMQ no son fuente de verdad (el outbox de la BD sí).
- Los datos de paciente nunca van al broker ni a logs/telemetría.

Guía completa de despliegue, migraciones, imports, backups y recuperación en
[`docs/deployment.md`](deployment.md).

---

## 9. Seguridad y datos

- Nagomi trata información sanitaria y personal: **TLS obligatorio**,
  cifrado en reposo, secretos gestionados, colas de proveedores restringidas.
- El chat de ayuda y las integraciones externas requieren credenciales que
  **solo viven en el backend / `.env`** y nunca en el navegador ni en el repo.
- Rotación de secretos, y revocación de clientes de API desde **Identidad**.

---

## Índice de documentación

| Fichero | Contenido |
|---|---|
| `README.md` | Visión general y arranque rápido |
| `Product.md.md` | Visión y alcance de producto |
| `Entities.md.md` | Entidades del dominio |
| `docs/development-guide.md` | Guía técnica de desarrollo (arquitectura, RabbitMQ, outbox, tooling) |
| `docs/deployment.md` | Despliegue, migraciones, imports, backups y recuperación |
| `docs/provider-integration.md` | OAuth, REST, idempotencia y manejo de fallos para proveedores |
| `docs/tenant-capabilities.md` | Capacidades del tenant, auto-proveedor y colas |
| `docs/user-guide.md` | **Este fichero**: manual de usuario y guía técnica |
