# MyTube Public Edition

[![Build and Test](https://github.com/vinaytkere/MyTube-Public/actions/workflows/ci.yml/badge.svg)](https://github.com/vinaytkere/MyTube-Public/actions)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![React 19](https://img.shields.io/badge/React-19-cyan.svg)](https://react.dev/)
[![SQLite](https://img.shields.io/badge/Database-SQLite-blue.svg)](https://www.sqlite.org/)

**MyTube** is a modern, self-hosted personal media streaming hub and video downloader. It combines video library management, automated online video downloading (via yt-dlp and ffmpeg), IPTV live streaming, audio extraction, and optional AI-assisted metadata generation in a single unified system.

Designed from the ground up for privacy, portability, and zero manual configuration:
- **Zero External Database Overhead:** Powered by an embedded SQLite database engine configured with Write-Ahead Logging (WAL) concurrency and busy timeouts.
- **Single-Container Core:** High-performance ASP.NET Core 10 backend serves both REST APIs and the responsive React 19 Single Page Application on port `5003`.
- **Fail-Closed VPN Torrenting:** Optional Transmission torrent client isolated strictly behind a WireGuard VPN tunnel with an automated firewall killswitch.
- **Privacy & Security First:** File-based secrets mounted via `/run/secrets/`, SSRF protection with multi-hop redirect verification, non-root container execution (UID 1000), and hardened CORS policies.
- **Zero-Key Graceful Degradation:** Completely operational out of the box without requiring paid subscriptions, cloud services, or external AI API keys.

---

## Prerequisites for Self-Hosting

Before deploying MyTube, ensure your host environment meets the following requirements:

### 1. Hardware Requirements
| Resource | Minimum | Recommended | Notes |
| :--- | :--- | :--- | :--- |
| **CPU** | 2 cores (x86_64 or ARM64) | 4+ cores | Needed for concurrent video downloads and FFmpeg transcoding |
| **RAM** | 2 GB | 4 GB+ | Accommodates media buffering and in-memory caching |
| **Disk (OS/DB)**| 10 GB free space | Fast SSD/NVMe | Highly recommended for SQLite WAL and fast thumbnail caching |
| **Disk (Media)** | Sized per library | Dedicated storage drive | Host volume for media library and downloaded videos |

### 2. Operating System & Software
- **Linux:** Ubuntu 22.04+, Debian 12+, Fedora, Arch Linux, or Alpine Linux.
- **Windows:** Windows 10/11 64-bit with Docker Desktop and the **WSL2** backend.
- **macOS:** macOS Sonoma (14+) or Ventura (13+) with Docker Desktop (Native Apple Silicon M1/M2/M3/M4 & Intel x86_64).
- **Docker:** Docker Engine **24.0+** and Docker Compose **v2.20+**.

### 3. Network & Ports
- **Port 5003 (TCP):** MyTube Web Dashboard & API (Configurable in `.env`).
- **Port 8096 (TCP):** Optional Jellyfin media streaming server (if enabled).
- **Port 51820 (UDP):** Outbound UDP access for WireGuard VPN (if torrent integration is enabled).

---

## Self-Hosting Quickstart (Automated Setup Wizard)

MyTube includes an automated interactive wizard that performs system checks, generates cryptographically secure secrets, prompts for your desired optional services, silently sets up your initial administrator account, and starts the stack.

### On Linux or macOS

```bash
# 1. Clone the repository
git clone https://github.com/vinaytkere/MyTube-Public.git
cd MyTube-Public

# 2. Make scripts executable
chmod +x deploy/scripts/*.sh

# 3. Run the interactive setup wizard
./deploy/scripts/setup.sh
```

### On Windows (PowerShell / Docker Desktop)

```powershell
# 1. Clone the repository
git clone https://github.com/vinaytkere/MyTube-Public.git
Set-Location MyTube-Public

# 2. Run the interactive PowerShell wizard
.\deploy\scripts\setup.ps1
```

Once the wizard completes, open **`http://localhost:5003`** in your web browser.

---

## Manual Production Deployment (Docker Compose)

If you are running in a headless CI/CD environment or prefer managing containers manually:

### Step 1: Create Host Storage Directories
```bash
mkdir -p data data/keys media media/downloads deploy/secrets data/backups
```

### Step 2: Configure Environment Variables
Copy the reference configuration file:
```bash
cp .env.example .env
```
Key configuration settings in `.env`:
```env
PORT=5003
DATA_DIR=../data
MEDIA_DIR=../media
DOWNLOADS_DIR=../media/downloads
CORS_ALLOWED_ORIGINS=http://localhost:5003,http://127.0.0.1:5003
```

### Step 3: Generate Security Secrets
MyTube mounts sensitive keys via Docker secrets at `/run/secrets/`. Generate the required JWT signing key and application encryption key:

```bash
# On Linux / macOS
openssl rand -hex 32 > deploy/secrets/jwt_secret.txt
openssl rand -hex 32 > deploy/secrets/encryption_key.txt
touch deploy/secrets/gemini_api_key.txt deploy/secrets/groq_api_key.txt deploy/secrets/deepseek_api_key.txt deploy/secrets/wireguard_private_key.txt
chmod 600 deploy/secrets/*.txt

# On Windows (PowerShell)
function New-Secret { [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32) | ForEach-Object { "{0:x2}" -f $_ } }
(New-Secret) | Out-File -FilePath deploy/secrets/jwt_secret.txt -Encoding ascii -NoNewline
(New-Secret) | Out-File -FilePath deploy/secrets/encryption_key.txt -Encoding ascii -NoNewline
"" | Out-File -FilePath deploy/secrets/gemini_api_key.txt -Encoding ascii -NoNewline
"" | Out-File -FilePath deploy/secrets/groq_api_key.txt -Encoding ascii -NoNewline
"" | Out-File -FilePath deploy/secrets/deepseek_api_key.txt -Encoding ascii -NoNewline
"" | Out-File -FilePath deploy/secrets/wireguard_private_key.txt -Encoding ascii -NoNewline
```

### Step 4: Launch Containers

**1. Core Stack Only (Web UI, API, Downloader, SQLite):**
```bash
docker compose up -d
```

**2. Core Stack + WireGuard Torrent Client (Gluetun + Transmission):**
```bash
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.torrent.yml up -d
```

**3. Core Stack + Jellyfin:**
```bash
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.jellyfin.yml up -d
```

**4. Full Stack (Core + Torrents + Jellyfin):**
```bash
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.torrent.yml -f deploy/docker-compose.jellyfin.yml up -d
```

### Step 5: Verify Deployment
Check container health and logs:
```bash
docker compose -f deploy/docker-compose.yml ps
docker compose -f deploy/docker-compose.yml logs -f mytube
```

---

## Managing Your Instance

### Viewing Application Logs
```bash
docker compose -f deploy/docker-compose.yml logs -f mytube
```

### Stopping and Restarting
```bash
# Stop containers
docker compose -f deploy/docker-compose.yml down

# Restart containers
docker compose -f deploy/docker-compose.yml restart
```

### Updating to the Latest Release
```bash
git pull origin main
docker compose -f deploy/docker-compose.yml build --no-cache
docker compose -f deploy/docker-compose.yml up -d
```

### Online Hot Backups
MyTube uses SQLite's transactional `VACUUM INTO` command, allowing you to create consistent, defragmented backups while the instance is running:

```bash
./deploy/scripts/backup.sh
```
Backups are archived into `./backups/mytube-backup-<timestamp>.tar.gz`, containing the database, cryptographic keys, and secrets.

### Restoring Backups
```bash
./deploy/scripts/restore.sh backups/mytube-backup-YYYYMMDD_HHMMSS.tar.gz
```

---

## Feature Matrix

| Feature | Core Stack | Optional Extension | Description |
| :--- | :---: | :---: | :--- |
| **Media Player & Library** | :white_check_mark: | - | Stream, organize, and search local videos with auto-generated thumbnails |
| **yt-dlp Video Downloader** | :white_check_mark: | - | Download videos, playlists, and audio directly to your media library |
| **IPTV Live Stream Relay** | :white_check_mark: | - | Play M3U / HLS live channels with SSRF-validated streaming relays |
| **Single-File SQLite Storage** | :white_check_mark: | - | Embedded database with automatic migrations and WAL concurrency |
| **WireGuard Torrent Client** | - | :package: (`compose.torrent`) | Transmission torrent client routed strictly through Gluetun VPN |
| **AI Summaries & Categorization** | - | :key: (Env / Secrets) | Optional integrations for Gemini, Groq, DeepSeek, or local Ollama |
| **Jellyfin Media Server** | - | :package: (`compose.jellyfin`) | Centralized multi-device media library indexing and transcoding |

---

## Platform-Specific Self-Hosting Notes

### Linux / NAS (File Permissions)
The backend container runs as an unprivileged user (`mytube`, UID `1000`, GID `1000`). Ensure host directories are owned by UID 1000:
```bash
sudo chown -R 1000:1000 data media downloads secrets
sudo chmod -R 750 data media downloads secrets
```

### Windows (WSL2 / Docker Desktop)
- For maximum SQLite I/O performance, clone and run MyTube inside the **WSL2 virtual filesystem** (e.g., `\\wsl$\Ubuntu\home\<user>\MyTube-Public`) rather than a Windows drive mount (`/mnt/c/`).
- Verify that Docker Desktop is using the **WSL 2 based engine**.

### macOS (Apple Silicon / Intel)
- The container image is built natively for both `linux/arm64` and `linux/amd64`.
- Ensure your `media` and `downloads` folders are enabled under Docker Desktop > **Settings > Resources > File Sharing**.

---

## Documentation

Comprehensive guides are available in the [`docs/`](docs/) directory:

- [Installation Guide](docs/installation.md) - Platform requirements and detailed setup steps.
- [Configuration Reference](docs/configuration.md) - Detailed reference for `.env` variables and `/run/secrets/` files.
- [VPN & Torrent Setup](docs/vpn-setup.md) - WireGuard configuration, DNS leak prevention, and fail-closed killswitch testing.
- [AI Integrations Guide](docs/ai-setup.md) - Configuring free API keys (Gemini, Groq) or local Ollama models.
- [Backup and Restore](docs/backup-restore.md) - Hot SQLite backups (`VACUUM INTO`), data migration, and recovery procedures.
- [Architecture Overview](docs/architecture.md) - System design, SQLite concurrency, and security threat model.
- [Troubleshooting](docs/troubleshooting.md) - Common issues, file permissions, and diagnostic logs.

---

## Technology Stack

- **Backend:** C# / ASP.NET Core 10 (API Controllers, Clean Architecture, MediatR CQRS, EF Core with SQLite)
- **Frontend:** React 19, TypeScript, Vite, Tailwind CSS, Lucide Icons
- **Media Processing:** FFmpeg, FFprobe, yt-dlp, Deno runtime
- **VPN / Networking:** Gluetun (WireGuard client with iptables killswitch), Transmission daemon
- **Containerization:** Multi-arch Docker (`linux/amd64`, `linux/arm64`)

---

## Security

Please review our [Security Policy](SECURITY.md) for information regarding vulnerability reporting, secret isolation, and network safeguards.

---

## License

This project is open-source under the [Apache License 2.0](LICENSE).
