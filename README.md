# homelab-monitor

[![CI](https://github.com/SrArkaitz/homelab-monitor/actions/workflows/ci.yml/badge.svg)](https://github.com/SrArkaitz/homelab-monitor/actions/workflows/ci.yml)

Monitor de los servicios de mi servidor de casa (**pc-de-bill**). Cada pocos segundos comprueba si Jellyfin, Immich, OpenCloud, Portainer y el resto responden, guarda el histórico y actualiza un panel web **en tiempo real**. Si algo se cae, avisa por Telegram.

**Stack:** ASP.NET Core (.NET 10) · Entity Framework Core + SQLite · SignalR · Angular 20 · Docker · GitHub Actions

---

## Qué hace

- **Comprobaciones HTTP y TCP.** HTTP mira el código de respuesta (2xx/3xx, y 401/403 porque el servicio responde aunque pida login). TCP solo comprueba que el puerto acepta conexiones (por ejemplo SSH en el 22).
- **Sin falsas alarmas.** Un fallo suelto no marca el servicio como caído: hacen falta 2 fallos seguidos (configurable). Un acierto lo marca como activo otra vez.
- **Panel en directo.** El navegador recibe cada comprobación por SignalR, sin estar preguntando a la API.
- **Histórico y estadísticas.** Uptime y latencia media de 24 h, gráfica de latencia y franja de disponibilidad de 6 h, 24 h o 7 días.
- **Avisos por Telegram** solo cuando cambia el estado (cae o se recupera), no en cada comprobación.
- **Limpieza automática** de comprobaciones de más de 30 días.

## Arquitectura

```
┌──────────────── Angular (nginx) ────────────────┐
│  Panel · Detalle · Formulario                   │
│  ServiciosStore (signals) ◄── SignalR ──┐       │
└──────────┬──────────────────────────────┼───────┘
           │ HTTP /api                    │ WebSocket /hubs/estado
┌──────────▼──────────────────────────────┴───────┐
│ ASP.NET Core                                    │
│  Controllers ──► ServiciosConsulta (lecturas)   │
│       │                                         │
│       └──────► MonitorService ◄── MonitorWorker │
│                  │    │    │      (BackgroundService, cada 5 s)
│        IComprobador  INotificador  IPublicadorTiempoReal
│        (HTTP / TCP)  (Log, Telegram) (SignalR)  │
│                  │                              │
│            EF Core ──► SQLite                   │
└─────────────────────────────────────────────────┘
```

### Decisiones que merece la pena explicar

| Decisión | Por qué |
|---|---|
| `MonitorWorker` es un `BackgroundService` singleton que abre un **scope por ciclo** | El `DbContext` es scoped y no es thread-safe; no se puede inyectar directamente en un singleton. |
| Las peticiones de red van en **paralelo** (`Task.WhenAll`) y el guardado en serie | Un servicio lento no retrasa a los demás, y el `DbContext` solo se usa desde un hilo. |
| Interfaces `IComprobador`, `INotificador`, `IPublicadorTiempoReal` | La lógica de `MonitorService` se prueba sin red, sin Telegram y sin SignalR. |
| `TimeProvider` inyectado en vez de `DateTime.UtcNow` | Los tests controlan el reloj y pueden "adelantar" 31 días para probar la limpieza. |
| `CalculadoraEstado` como función pura | La regla de "N fallos seguidos" se prueba con datos sin montar nada más. |
| Hub tipado (`Hub<IEstadoCliente>`) | El compilador comprueba los nombres de los mensajes que se envían al navegador. |
| Estadísticas con **una sola consulta agrupada** (`GroupBy` + `Count` + `Average`) | Se calculan en SQLite, no trayendo miles de filas a memoria. |
| Fechas guardadas en UTC con un `ValueConverter` | SQLite no guarda zona horaria; así el JSON siempre lleva la `Z` y Angular las muestra en hora local. |
| Errores en formato **ProblemDetails** | Respuestas de error estándar (RFC 9457) que el frontend sabe leer. |
| En Angular, un **store con signals** alimentado por SignalR | Todas las pantallas leen la misma lista y se actualizan solas. |

## Tests

```bash
cd backend
dotnet test
```

53 tests con xUnit:

- **Unitarios:** reglas de estado, cálculo de uptime, validación de destinos.
- **Comprobador:** respuestas HTTP simuladas con un `HttpMessageHandler` falso y conexiones TCP reales a un puerto local.
- **MonitorService:** contra **SQLite en memoria** (se comporta como la BD real, a diferencia del proveedor InMemory) con reloj, comprobador y notificador falsos.
- **Integración:** la API entera levantada con `WebApplicationFactory`, probando los endpoints de verdad (201, 400, 404, 409).

GitHub Actions compila y pasa los tests del backend y compila el frontend en cada push.

## Ejecutarlo en local

Necesitas el [SDK de .NET 10](https://dotnet.microsoft.com/download) y Node 22.

```bash
# API en http://localhost:5080 (documentación en /scalar)
cd backend/src/HomelabMonitor.Api
dotnet run

# Panel en http://localhost:4200 (redirige /api y /hubs a la API)
cd frontend
npm install
npm start
```

## Desplegarlo en el servidor

```bash
git clone https://github.com/SrArkaitz/homelab-monitor.git
cd homelab-monitor
docker compose up -d --build
```

El panel queda en `http://192.168.1.138:8090`. También se puede añadir como stack en Portainer apuntando a este repositorio.

**Avisos por Telegram (opcional):** crea un bot con [@BotFather](https://t.me/BotFather), consigue tu chat id y ponlos en un fichero `.env` junto al `docker-compose.yml`:

```
TELEGRAM_BOT_TOKEN=123456:ABC...
TELEGRAM_CHAT_ID=123456789
```

## API

| Método | Ruta | Qué hace |
|---|---|---|
| GET | `/api/servicios` | Lista con estado y estadísticas de 24 h |
| GET | `/api/servicios/{id}` | Un servicio |
| GET | `/api/servicios/{id}/comprobaciones?horas=24` | Histórico (máx. 7 días) |
| POST | `/api/servicios` | Crear (se comprueba al momento) |
| PUT | `/api/servicios/{id}` | Editar |
| DELETE | `/api/servicios/{id}` | Eliminar con su histórico |
| POST | `/api/servicios/{id}/comprobar` | Comprobar ya, sin esperar al intervalo |
| GET | `/api/resumen` | Totales para la cabecera |
| WS | `/hubs/estado` | SignalR: `ServicioActualizado`, `ServicioEliminado` |

## Siguientes pasos

- Migraciones de EF Core en lugar de `EnsureCreated`.
- Autenticación para poder publicarlo fuera de casa (por ejemplo `monitor.kaitzlab.com`).
- Comprobar también el certificado TLS y avisar antes de que caduque.
- Más canales de aviso (email, ntfy) implementando `INotificador`.
