# IntervalsMcp

Servidor MCP (Model Context Protocol) en C#/.NET que expone los datos de entrenamiento de
[Intervals.icu](https://intervals.icu) como herramientas para Claude: perfil del atleta, zonas,
actividades, streams de sensores (potencia/FC/cadencia), wellness (HRV, sueño, FC en reposo,
CTL/ATL) y eventos planificados.

> **Sobre "tiempo real":** Intervals.icu no ofrece telemetría en vivo — los datos llegan cuando
> Garmin/Strava/Wahoo/etc. sincronizan la actividad. Este servidor siempre devuelve el último
> dato ya sincronizado en el momento en que Claude llama a la herramienta, no un stream en vivo
> del entrenamiento mientras ocurre.

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download) o superior
- Una cuenta de Intervals.icu con una **API Key** (Settings → Developer Settings, en
  `https://intervals.icu/settings`)
- Tu **Athlete ID** (se ve en la URL de tu perfil, ej. `i12345678`)

## Configuración

El servidor soporta dos transportes MCP, controlados por variables de entorno:

| Variable | Obligatoria | Descripción |
|---|---|---|
| `INTERVALS_API_KEY` | Sí | Tu API key personal de Intervals.icu |
| `INTERVALS_ATHLETE_ID` | No | Tu athlete id (ej. `i12345678`). Si se omite, se usa `0`, que Intervals.icu resuelve como "el atleta autenticado por la API key" |
| `MCP_TRANSPORT` | No | `stdio` (default, para Claude Code/Desktop local) o `http` (para exponerlo en un VPS y usarlo desde cualquier dispositivo) |
| `MCP_AUTH_TOKEN` | Solo si `MCP_TRANSPORT=http` | Token secreto que cualquier cliente debe presentar como `Authorization: Bearer <token>` para poder usar el servidor. Generalo con `openssl rand -hex 32` — es una contraseña, tratalo como tal |

## Compilar y ejecutar localmente

```bash
dotnet build
```

```bash
INTERVALS_API_KEY=tu_api_key INTERVALS_ATHLETE_ID=i12345678 dotnet run --project IntervalsMcp
```

El servidor habla MCP por **stdio** (stdin/stdout), pensado para que un cliente MCP (Claude Code,
Claude Desktop) lo lance como subproceso — no es un servidor HTTP que se visite en el navegador.

## Conectarlo a Claude Code / Claude Desktop

Agregá esta entrada a tu configuración de servidores MCP (por ejemplo con `claude mcp add`, o
editando el `mcpServers` de tu config):

```json
{
  "mcpServers": {
    "intervals-icu": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/Proyects/MCP/IntervalsMcp", "--no-build"],
      "env": {
        "INTERVALS_API_KEY": "tu_api_key",
        "INTERVALS_ATHLETE_ID": "i12345678"
      }
    }
  }
}
```

Para producción conviene apuntar `command` directo al ejecutable publicado (ver más abajo) en vez
de pasar por `dotnet run`.

## Herramientas disponibles

| Herramienta | Descripción |
|---|---|
| `get_athlete_profile` | Perfil del atleta (nombre, peso, timezone, thresholds) |
| `get_sport_settings` | Zonas de potencia/FC/ritmo y FTP/LTHR por deporte |
| `list_activities` | Lista de actividades registradas (por defecto, últimos 90 días) |
| `get_activity` | Detalle completo de una actividad, con intervalos/laps |
| `get_activity_streams` | Series de tiempo (potencia, FC, cadencia, altitud, GPS) de una actividad, con downsampling automático |
| `list_wellness` | HRV, FC en reposo, sueño, peso, CTL/ATL/ramp rate por rango de fechas |
| `get_wellness` | Entrada de wellness de una fecha puntual |
| `list_events` | Eventos planificados en el calendario (próximos entrenos, carreras) |
| `get_event` | Detalle de un evento puntual del calendario |
| `create_event` | Crea un entreno planificado, carrera o nota en el calendario |
| `update_event` | Actualiza un evento existente (solo los campos que se pasan) |
| `delete_event` | Elimina un evento del calendario (no se puede deshacer) |

Todas devuelven el JSON crudo de la API de Intervals.icu (salvo `get_activity_streams`, que
recorta streams largos a un máximo configurable de puntos para no saturar el contexto).

Las últimas cuatro son de **escritura**: modifican tu calendario real en Intervals.icu.
`create_event` acepta la sintaxis de texto plano de Intervals.icu para describir entrenos
estructurados (ej. `"- 15m 55% Warmup\n3x\n- 1m 150%\n- 1m 50%\n- 15m 55% Cooldown"`), que la API
parsea automáticamente a pasos con potencia/duración.

## Publicar en un VPS y usarlo desde cualquier dispositivo

Este modo levanta el servidor como un endpoint HTTP (`/mcp`) protegido por un token Bearer propio,
para que puedas agregarlo como conector MCP remoto en Claude (web, mobile, desktop) y pedirle
feedback de un entreno recién sincronizado desde donde estés.

### Con Dokploy (recomendado)

El repo incluye un [`Dockerfile`](Dockerfile) multi-stage (build con el SDK de .NET, runtime con
la imagen de ASP.NET) ya probado localmente con `docker build` + `docker run`. Dokploy se encarga
del reverse proxy y de HTTPS automático (Traefik + Let's Encrypt) — no hace falta Caddy ni systemd.

1. En Dokploy, creá una nueva **Application** apuntando a este repo de GitHub, con build type
   **Dockerfile** (`Dockerfile` en la raíz del repo).
2. En **Environment Variables**, cargá:

   | Variable | Valor |
   |---|---|
   | `INTERVALS_API_KEY` | tu API key de Intervals.icu |
   | `INTERVALS_ATHLETE_ID` | tu athlete id, ej. `i12345678` |
   | `MCP_AUTH_TOKEN` | un token largo y aleatorio, generado con `openssl rand -hex 32` |

   (`MCP_TRANSPORT` y `ASPNETCORE_URLS` ya vienen fijados en el Dockerfile, no hace falta tocarlos.)
3. En **Ports**, exponé el contenedor en el puerto `8080` (el que expone el Dockerfile).
4. Configurá el **Domain** (subdominio con registro DNS apuntando al VPS) y activá HTTPS — Dokploy
   emite el certificado solo.
5. Deploy. Los logs del contenedor en Dokploy deberían mostrar `Now listening on: http://0.0.0.0:8080`.

### Sin Dokploy (systemd + Caddy, manual)

Si en algún momento corrés esto en un VPS sin Dokploy:

1. **Publicar el binario**:

   ```bash
   dotnet publish IntervalsMcp -c Release -r linux-x64 --self-contained false -o out
   ```

   Copiá el contenido de `out/` a `/opt/intervalsmcp` en el VPS.

2. **Variables de entorno**: copiá [`deploy/intervalsmcp.env.example`](deploy/intervalsmcp.env.example)
   a `/etc/intervalsmcp.env`, completá los valores reales y restringí permisos:

   ```bash
   sudo chmod 600 /etc/intervalsmcp.env
   ```

3. **Servicio systemd**: copiá [`deploy/intervalsmcp.service`](deploy/intervalsmcp.service) a
   `/etc/systemd/system/intervalsmcp.service` y activalo:

   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable --now intervalsmcp
   ```

4. **HTTPS con Caddy**: instalá [Caddy](https://caddyserver.com/), usá
   [`deploy/Caddyfile`](deploy/Caddyfile) como base (reemplazando el dominio) y recargalo.
5. **Firewall**: solo 80/443 (Caddy) deberían estar abiertos al público; el puerto 5170 de Kestrel
   no debería ser alcanzable desde afuera del VPS.

### Conectarlo a Claude desde el celular u otro dispositivo

En la app de Claude (web o mobile), agregá un **conector MCP remoto** apuntando a:

- URL: `https://mcp.tudominio.com/mcp`
- Autenticación: header `Authorization: Bearer <tu MCP_AUTH_TOKEN>`

Una vez agregado, cualquier conversación de Claude en ese dispositivo puede llamar a las
herramientas de este servidor — por ejemplo, después de un entreno le pedís feedback y Claude
consulta Intervals.icu en tiempo real a través del VPS.

### Notas de seguridad

- `MCP_AUTH_TOKEN` es la única barrera entre internet y tu servidor: guardalo como una contraseña,
  no lo compartas ni lo subas a git. En Dokploy vive solo en las Environment Variables de la app,
  nunca en el repo.
- Si sospechás que el token se filtró, generá uno nuevo, actualizalo en Dokploy (o en
  `/etc/intervalsmcp.env` + `sudo systemctl restart intervalsmcp` en el modo manual) y redeployá;
  el conector viejo dejará de funcionar de inmediato.
- Tu `INTERVALS_API_KEY` nunca viaja hacia el cliente MCP: vive solo en el servidor y se usa para
  hablarle a la API de Intervals.icu internamente.
