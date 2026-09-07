# Seguridad de Nagomi (guía de implementación y cumplimiento)

Estado: **implementado en la rama `security-hardening`** (merge pendiente de revisión).
Este documento describe lo que Nagomi hace hoy para proteger datos de salud y lo que
falta para alinearse con los marcos habituales en España/UE (RGPD/LOPDGDD, ISO 27001
Anexo A, ENS) cuando una organización lo necesite.

> El software no se "certifica ISO 27001": la certificación es de la **organización**
> (SGSI). Lo que Nagomi sí puede aportar son los **controles técnicos del Anexo A** y
> las garantías que pedirán los clientes (hospitales, mutuas) en sus pliegos.

---

## 1. Primer arranque (onboarding del administrador)

Una instalación nueva arranca con una **cuenta bootstrap**:

- **Usuario:** `admin` · **Contraseña:** `Admin` · Correo: `admin@nagomi.local`

Esa cuenta **solo** puede completar el alta inicial:

1. Entra con `admin` / `Admin`.
2. La aplicación le muestra la pantalla **"Primera configuración"** (nada más es accesible;
   el resto de la API devuelve `403 onboarding_required`).
3. Crea el **administrador real** de la organización (nombre, correo, usuario y contraseña
   de ≥12 caracteres).
4. Al completar el alta, la cuenta bootstrap se **desactiva** y la sesión pasa al nuevo
   administrador.

La cuenta `admin` / `Admin` deja de existir como vía de acceso. En instalaciones con
usuarios ya creados (actualizaciones) el flujo no se activa.

Implementación: `MustChangePassword` en `ApplicationUser`, claim `must_change_password`
en el token, `OnboardingGuardMiddleware` (bloquea al bootstrap fuera de `/api/auth/*`),
endpoint `POST /api/auth/onboarding` y `UserSeeder` (crea el bootstrap solo en BD vacía).

## 2. Contraseñas y acceso

- **Almacenamiento:** `PasswordHasher` de ASP.NET Identity = **PBKDF2** con salt aleatorio
  y ~100 000 iteraciones (cumple NIST 800-63B §5.1.1 y el Anexo A de ISO 27001 A.9.4.3).
- **Política:** mínimo **12 caracteres** (NIST 800-63B: la longitud importa más que la
  composición; no se exigen símbolos), con mayúscula, minúscula y número.
- **Bloqueo de cuenta:** tras **10 intentos fallidos**, la cuenta queda bloqueada **15
  minutos** (se aplica manualmente en `PasswordGrantHandler`, dado que OpenIddict usa un
  grant propio). Mitiga fuerza bruta por usuario.
- **Rate limiting del login:** máximo **10 intentos por IP y minuto** en `/connect/token`
  (`LoginRateLimitMiddleware`, ventana fija). Desactivado en desarrollo/tests
  (`RateLimiting:LoginPerMinute = 0` en `appsettings.Development.json`).
- **Autenticación:** OAuth 2.0 con OpenIddict 7.2. Tokens de acceso de vida corta para
  usuarios y clientes M2M (client credentials), rotación y revocación de secretos.
- **Roles:** `admin` / `default`; las escrituras de datos maestros y la gestión de
  usuarios/identidad exigen `admin`.

## 3. Datos de salud (RGPD / LOPDGDD, art. 9)

El tratamiento de datos de salud es de **categoría especial** y exige, además del software:

| Área | En Nagomi hoy | Acción organizativa |
|---|---|---|
| Acceso solo autorizado | Roles + OAuth; los listados operativos NO exponen DNI/tarjeta sanitaria | — |
| Minimización | El detalle solo muestra lo necesario; CSV de operaciones sin identificadores sensibles | — |
| Notificaciones sin datos | RabbitMQ solo lleva `messageId` + `retrievalUrl`; el detalle viaja por REST autenticado | — |
| Auditoría | `TransportAuditRecord` en solicitudes/trayectos/urgencias | Ampliar a accesos de lectura si un cliente lo pide |
| Cifrado en reposo | Depende del despliegue (volumen/BD) | **Pendiente**: cifrado de volumen o de columnas sensibles (DNI, tarjeta) |
| RAT (Registro de Actividades) | — | **Obligatorio**: mantener el RAT de la organización |
| Derechos ARCO/ARSULIPO | — | **Obligatorio**: procedimiento de acceso/rectificación/supresión |
| DPD | — | Designar delegado si aplica |

## 4. Despliegue (Docker)

El puerto se elige al desplegar (no está fijo en el código):

```bash
# .env
FRONTEND_BIND_ADDRESS=0.0.0.0    # escuchar en todas las interfaces (o 127.0.0.1 solo local)
FRONTEND_PORT=8443               # puerto público que prefieras
```

- **TLS/HTTPS:** Nagomi escucha HTTP por diseño y deja el TLS a la capa de terminación
  (proxy inverso como Caddy/nginx/Traefik, o el túnel de la plataforma). Decide en el
  hosting, no en la app. **No** se incluye HSTS en la app a propósito: es decisión del
  proxy y de si ya hay HTTPS.
- **Firewall:** si el frontend escucha en `0.0.0.0` para acceso desde la red local,
  abre el puerto elegido en el firewall del host.
- **Cifrado de BD:** el volumen de Postgres debe vivir en disco cifrado (LUKS/BitLocker/
  cifrado del proveedor cloud). Es la medida principal de "datos en reposo" para una pyme.

## 5. Checklist frente a marcos (estado real)

### ISO 27001 — Anexo A (controles técnicos aplicables al producto)
- [x] A.9.2.1 Registro y baja de usuarios (admin gestiona usuarios; soft state)
- [x] A.9.2.4 Gestión de derechos de acceso (roles admin/default)
- [x] A.9.3.1 Gestión de secretos (sin secretos en repo; .env externo)
- [x] A.9.4.2 Autenticación segura (OAuth 2.0 / OpenIddict; sin contraseñas en claro)
- [x] A.9.4.3 Gestión de contraseñas (PBKDF2 + salt + política ≥12 + lockout)
- [x] A.12.4.1 Registro de eventos (auditoría de entidades; falta login/logout audit)
- [x] A.13.1.1 Controles de red (cabeceras nginx: nosniff, referrer, frame)
- [~] A.8.2.3 Cifrado de activos (depende del despliegue; volumen cifrado)
- [ ] A.9.4.1 Restricción de acceso a la información (pendiente: 2FA voluntario para admin)
- [ ] A.12.4.3 Registros del administrador y operador (pendiente: login/logout audit)

### RGPD / LOPDGDD (datos de salud)
- [x] Minimización y acceso por roles
- [x] Datos fuera del broker
- [ ] Cifrado en reposo de columnas sensibles (o volumen cifrado documentado)
- [ ] RAT + análisis de riesgo + DPD + derechos ARCO/ARSULIPO (organizativo)

### ENS (solo si algún cliente es Administración Pública)
- [ ] Categorización del sistema y medidas [op.exp], [op.acc], [mp.info] según nivel
- [ ] Registro de actividad, gestión de incidentes y copias de seguridad

## 6. Decisiones tomadas (no implementadas a propósito)

- **Sin TOTP/2FA** por ahora: se puede añadir como mejora voluntaria para administradores.
- **Sin HSTS en la app**: se gestiona en el proxy/TLS del despliegue.
- **TLS y puerto**: responsabilidad del hosting (Docker expone `FRONTEND_PORT`).

## 7. Endpoints de seguridad relevantes

| Método | Ruta | Descripción |
|---|---|---|
| POST | `/connect/token` | Login OAuth (password grant, rate-limited por IP, lockout por cuenta) |
| GET | `/api/auth/me` | Usuario actual + `onboardingRequired` |
| POST | `/api/auth/onboarding` | Alta del primer administrador (solo bootstrap) |
| POST | `/api/auth/logout` | Cierre de sesión (frontend descarta el token) |
