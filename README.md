# MyTube Public Edition

[![Build and Test](https://github.com/mytube-project/mytube/actions/workflows/ci.yml/badge.svg)](https://github.com/mytube-project/mytube/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![React 19](https://img.shields.io/badge/React-19-cyan.svg)](https://react.dev/)
[![SQLite](https://img.shields.io/badge/Database-SQLite-blue.svg)](https://www.sqlite.org/)

**MyTube** is a modern, self-hosted personal media streaming hub and downloader. It provides video management, YouTube/online video downloading (via yt-dlp and ffmpeg), IPTV live streaming, audio extraction, and optional AI-assisted metadata generation.

Designed from the ground up for privacy, simplicity, and zero manual configuration:
- **No external database server needed:** Powered by an embedded SQLite engine with Write-Ahead Logging (WAL) concurrency.
- **Single container core:** ASP.NET Core backend serves both high-performance REST APIs and the responsive React Single Page Application.
- **Fail-closed VPN integration:** Optional torrent downloader isolated behind a WireGuard VPN tunnel with an automated firewall killswitch.
- **Privacy & security first:** All API secrets mounted safely via Docker Compose secrets (`/run/secrets/`); SSRF protection with strict internal IP filtering; unprivileged container execution.
- **Zero-key graceful degradation:** Fully functional out of the box without any third-party AI keys or paid services.

---

## Quick Start (Interactive Setup Wizard)

MyTube includes an automated setup wizard that creates directory structures, generates cryptographically secure secrets, prompts for your desired optional services, and starts your containers.

### Linux / macOS

```bash
git clone https://github.com/your-username/mytube.git
cd mytube

# Make the setup script executable and run
chmod +x deploy/scripts/setup.sh
./deploy/scripts/setup.sh
```

### Windows (PowerShell / Docker Desktop)

```powershell
git clone https://github.com/your-username/mytube.git
cd mytube

# Run the interactive PowerShell wizard
.\deploy\scripts\setup.ps1
```

Once complete, open your browser at **`http://localhost:5003`** to access your MyTube dashboard.

---

## Feature Matrix

| Feature | Core Deployment | Optional Extension | Description |
| ------- | :-------------: | :----------------: | ----------- |
| **Media Player & Library** | :white_check_mark: | - | Stream, organize, and search local videos with auto-generated thumbnails |
| **yt-dlp Video Downloader** | :white_check_mark: | - | Download videos, playlists, and audio directly to your media library |
| **IPTV Live Stream Relay** | :white_check_mark: | - | Play M3U / HLS live channels with SSRF-validated streaming relays |
| **Single-File SQLite Storage** | :white_check_mark: | - | Embedded database with automatic migrations and WAL concurrency |
| **WireGuard Torrent Client** | - | :package: (`compose.torrent`) | Transmission torrent client routed strictly through Gluetun VPN |
| **AI Summaries & Categorization** | - | :key: (Env / Secrets) | Optional integrations for Gemini, Groq, DeepSeek, or local Ollama |
| **Jellyfin Media Server** | - | :package: (`compose.jellyfin`) | Centralized multi-device media library indexing and transcoding |

---

## Manual Docker Compose Deployment

If you prefer deploying via standard Docker Compose commands without the wizard:

1. **Copy configuration:**
   ```bash
   cp .env.example .env
   mkdir -p secrets data media downloads
   ```

2. **Generate secure secrets:**
   ```bash
   # Generate 32-character random JWT signing key
   openssl rand -hex 32 > secrets/jwt_secret.secret
   ```

3. **Start the core stack:**
   ```bash
   docker compose -f deploy/docker-compose.yml up -d
   ```

4. **(Optional) Enable Torrents with WireGuard:**
   ```bash
   docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.torrent.yml up -d
   ```

5. **(Optional) Enable Jellyfin:**
   ```bash
   docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.jellyfin.yml up -d
   ```

---

## Documentation

Detailed operational guides are available in the [`docs/`](docs/) directory:

- [Installation Guide](docs/installation.md) - Platform requirements and setup steps for Linux, macOS, and Windows.
- [Configuration Reference](docs/configuration.md) - Detailed reference for `.env` variables and `/run/secrets/` paths.
- [VPN & Torrent Setup](docs/vpn-setup.md) - WireGuard configuration, DNS leak prevention, and fail-closed killswitch testing.
- [AI Integrations Guide](docs/ai-setup.md) - Configuring free API keys (Gemini, Groq) or local Ollama models.
- [Backup and Restore](docs/backup-restore.md) - Hot SQLite backups (`VACUUM INTO`), data migration, and recovery procedures.
- [Architecture Overview](docs/architecture.md) - System design, SQLite concurrency, and security threat model.
- [Troubleshooting](docs/troubleshooting.md) - Common issues, file permissions, and diagnostic logs.

---

## Technology Stack

- **Backend:** C# / ASP.NET Core 10 (Minimal APIs, Clean Architecture, MediatR CQRS, EF Core with SQLite)
- **Frontend:** React 19, TypeScript, Vite, Tailwind CSS, Lucide Icons
- **Media Engine:** FFmpeg, FFprobe, yt-dlp, Deno runtime
- **VPN / Networking:** Gluetun (WireGuard client with iptables killswitch), Transmission daemon
- **Containerization:** Multi-arch Docker (`linux/amd64`, `linux/arm64`)

---

## Security

Please review our [Security Policy](SECURITY.md) for information regarding vulnerability reporting, secret isolation, and network safeguards.

---

## License

This project is open-source under the [MIT License](LICENSE).

