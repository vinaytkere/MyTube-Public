# MyTube Architecture & Technical Design

This document details the architectural principles, system topology, concurrency model, and security design of MyTube Public Edition.

---

## 1. System Topology

```
+---------------------------------------------------------------------------------+
|                                Host Environment                                 |
|                                                                                 |
|   +-------------------------------------------------------------------------+   |
|   |                      MyTube Core Container (Port 5003)                  |   |
|   |                                                                         |   |
|   |   +---------------------+         +---------------------------------+   |   |
|   |   |  Vite React 19 SPA  | <=====> |   ASP.NET Core 10 Web API       |   |   |
|   |   |  (Compiled Assets)  |         |   (Controllers + Middleware)    |   |   |
|   |   +---------------------+         +----------------+----------------+   |   |
|   |                                                    |                    |   |
|   |            +-------------------+-------------------+                    |   |
|   |            |                   |                   |                    |   |
|   |   +--------v--------+ +--------v--------+ +--------v--------+           |   |
|   |   |  EF Core SQLite | | Media Processor | |  SSRF Safe Relay|           |   |   |
|   |   |  (mytube.db)    | | (yt-dlp/ffmpeg) | |  (IPTV Streams) |           |   |   |
|   |   +--------+--------+ +--------+--------+ +-----------------+           |   |
|   +------------|-------------------|----------------------------------------+   |
|                |                   |                                            |
|   +------------v-------------------v----------------------------------------+   |
|   |                      Mounted Persistent Volumes                         |   |
|   |   /app/data (SQLite & Keys)  |  /app/media  |  /app/downloads           |   |
|   +-------------------------------------------------------------------------+   |
|                                                                                 |
|   +----------------------------- Optional Extensions -----------------------+   |
|   |                                                                         |   |
|   |   +-------------------------+                 +---------------------+   |   |
|   |   | Gluetun (WireGuard VPN) |                 | Jellyfin Server     |   |   |
|   |   |   ^                     |                 | (Port 8096)         |   |   |
|   |   |   | network_mode        |                 +---------------------+   |   |
|   |   | Transmission Client     |                                           |   |
|   |   +-------------------------+                                           |   |
|   +-------------------------------------------------------------------------+   |
+---------------------------------------------------------------------------------+
```

---

## 2. Backend Clean Architecture

The backend adheres strictly to Clean Architecture separation of concerns:

### `MyTube.Backend.Domain`
- Contains core domain entities (`Video`, `Playlist`, `User`, `AiModelQuota`, `IptvChannel`).
- Zero external package dependencies.
- Encapsulates enterprise rules and business validations.

### `MyTube.Backend.Application`
- Contains CQRS use cases structured with **MediatR**:
  - **Commands:** Create video, initiate download, update user quota, register account.
  - **Queries:** Get video stream info, list playlists, system health diagnostics.
- Pipeline behaviors for validation (**FluentValidation**) and performance logging.
- Defines abstract service interfaces (`IVideoRepository`, `ISsrfValidator`, `IAiProviderClient`).

### `MyTube.Backend.Infrastructure`
- Concrete implementations of core abstractions:
  - **Data Access:** `VideoDbContext` utilizing `Microsoft.EntityFrameworkCore.Sqlite`.
  - **Security:** `SecretManagementService` (resolving Docker secrets from `/run/secrets/`), `JwtTokenGenerator`.
  - **Media Processing:** Process orchestration wrapping `yt-dlp`, `ffmpeg`, and `ffprobe`.
  - **Network & IPTV:** `IptvStreamRelayService` with multi-hop redirect SSRF validation.
  - **AI Integration:** Clients for Google Gemini, Groq, DeepSeek, and Ollama with fallback resilience.

### `MyTube.Backend.Api`
- ASP.NET Core 10 Web host.
- Registers REST endpoints using ASP.NET Core API Controllers (`app.MapControllers()`) and Minimal API routes for health checks (`/health`).
- Serves pre-built Vite React SPA static assets (`UseDefaultFiles`, `UseStaticFiles`, `MapFallbackToFile`).
- Configures Data Protection key persistence, authentication middleware, and CORS policies.

---

## 3. SQLite Concurrency & Storage Model

To deliver robust performance without requiring an external database server like PostgreSQL:

1. **Write-Ahead Logging (WAL Mode):**
   - Configured via connection string and connection initialization PRAGMA:
     ```sql
     PRAGMA journal_mode = WAL;
     ```
   - Readers never block writers, and writers never block readers.
2. **Busy Timeout Handling:**
   - Database connections set `busy_timeout = 5000` (5 seconds). If a write lock is temporarily active, subsequent operations queue gracefully rather than throwing immediate `database is locked` errors.
3. **Foreign Key Constraints:**
   - Explicitly enabled on every connection via `PRAGMA foreign_keys = ON;`.
4. **Short Transaction Lifecycles:**
   - Long-running operations (media downloads, audio transcoding, IPTV streaming, and AI API requests) **never** hold open database transactions. State updates are dispatched via targeted, short-lived CQRS commands.

---

## 4. Security Boundaries

- **Secret Decoupling:** Sensitive credentials are mount-isolated via Docker Compose secrets under `/run/secrets/`. Host-side secret files use strict permissions (`0600`).
- **SSRF Hardening:** User-provided stream and manifest URLs are validated against private, reserved, and loopback IP ranges across up to 3 HTTP redirect hops.
- **Fail-Closed VPN:** Torrent downloads execute inside a container bound to Gluetun's network namespace with iptables killswitch protection.
- **Non-Root Execution:** Container processes execute under user `mytube` (UID 1000).

