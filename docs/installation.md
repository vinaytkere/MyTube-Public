# MyTube Installation Guide

This guide covers system prerequisites, installation steps, and platform-specific configuration for deploying MyTube Public Edition.

---

## System Requirements

| Resource | Minimum | Recommended |
| -------- | ------- | ----------- |
| **CPU** | 2 cores (x86_64 or ARM64) | 4+ cores (for concurrent video downloads & FFmpeg transcoding) |
| **Memory** | 2 GB RAM | 4 GB+ RAM |
| **Storage** | 10 GB free space for OS & database | Dedicated storage disk/volume for media and downloads |
| **Software** | Docker Engine 24.0+ & Docker Compose v2 | Latest Docker Desktop or Linux Docker Engine |

---

## Supported Operating Systems

- **Linux:** Ubuntu 22.04+, Debian 12+, Arch Linux, Fedora, Alpine Linux
- **macOS:** macOS Sonoma (14+) or Ventura (13+) — Native support for Apple Silicon (M1/M2/M3/M4) and Intel (x86_64)
- **Windows:** Windows 10/11 64-bit with Docker Desktop and WSL2 backend

---

## 1. Quick Installation (Recommended)

The automated setup wizard guides you through environment verification, secret generation, optional service selection, and container startup.

### On Linux or macOS

```bash
# 1. Clone the repository
git clone https://github.com/vinaytkere/MyTube-Public.git
cd MyTube-Public

# 2. Grant execution permissions to setup scripts
chmod +x deploy/scripts/*.sh

# 3. Launch the setup wizard
./deploy/scripts/setup.sh
```

### On Windows (PowerShell)

```powershell
# 1. Clone the repository
git clone https://github.com/vinaytkere/MyTube-Public.git
Set-Location MyTube-Public

# 2. Run the PowerShell setup script
.\deploy\scripts\setup.ps1
```

The script will prompt for your preferences and then launch the containers via Docker Compose.

---

## 2. Manual Installation

If you prefer to configure everything manually without the interactive wizard:

### Step 1: Create Required Directories

```bash
mkdir -p data media downloads secrets
```

### Step 2: Configure Environment Variables

Copy the template configuration file:
```bash
cp .env.example .env
```

Edit `.env` to customize your ports and paths:
```env
APP_PORT=5003
DATA_PATH=./data
MEDIA_PATH=./media
DOWNLOADS_PATH=./downloads
```

### Step 3: Generate Security Secrets

Create the sensitive secret files under the `secrets/` directory:

```bash
# Generate a cryptographically secure 256-bit JWT secret (minimum 32 characters)
openssl rand -hex 32 > secrets/jwt_secret.secret
chmod 600 secrets/jwt_secret.secret
```

### Step 4: Launch Containers

To start the **core MyTube server**:
```bash
docker compose -f deploy/docker-compose.yml up -d
```

To include the **WireGuard VPN + Torrent downloader**:
```bash
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.torrent.yml up -d
```

To include **Jellyfin**:
```bash
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.jellyfin.yml up -d
```

---

## 3. Platform-Specific Notes

### Linux / NAS (UID and GID Permissions)
On Linux hosts, media directories mounted into containers must be writable by the container's unprivileged user (UID `1000`, GID `1000` by default):

```bash
# Assign ownership of application folders to user 1000
sudo chown -R 1000:1000 data media downloads secrets
```

### macOS
Docker Desktop for macOS handles file ownership through the VirtioFS or gRPC FUSE file-sharing system automatically. Ensure your `media` and `downloads` folders are included under Docker Desktop > **Settings > Resources > File Sharing**.

### Windows (WSL2 / Docker Desktop)
- Store your project repository and `data` directories inside the WSL2 Linux filesystem (e.g., `\\wsl$\Ubuntu\home\<user>\mytube`) rather than a Windows drive mount (`/mnt/c/`) for significantly improved I/O performance with SQLite.
- Ensure Docker Desktop is set to use the **WSL 2 based engine**.

