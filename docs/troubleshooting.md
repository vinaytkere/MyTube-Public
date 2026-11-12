# Troubleshooting Guide

This guide covers common issues, diagnostic procedures, and resolutions for MyTube Public Edition.

---

## 1. Permission Denied Errors on Linux / NAS

### Symptom:
Container logs display `System.UnauthorizedAccessException` or `Permission denied` when writing to `/app/data`, `/app/media`, or `/app/downloads`.

### Cause:
The container runs as an unprivileged user (`mytube`, UID `1000`, GID `1000`), but the host folders were created as `root`.

### Resolution:
Assign ownership of the application data directories to UID 1000 on the host:
```bash
sudo chown -R 1000:1000 data media downloads secrets
sudo chmod -R 750 data media downloads secrets
```

---

## 2. Port Conflicts (Port 5003 or 8096 Already In Use)

### Symptom:
Docker returns an error similar to `Error response from daemon: driver failed programming external connectivity on endpoint ... bind: address already in use`.

### Resolution:
1. Identify which service is using the port:
   ```bash
   # On Linux/macOS
   sudo lsof -i :5003

   # On Windows
   netstat -ano | findstr :5003
   ```
2. Change the host port in your `.env` file:
   ```env
   APP_PORT=5050
   ```
3. Restart the containers:
   ```bash
   docker compose -f deploy/docker-compose.yml up -d
   ```

---

## 3. SQLite "Database is Locked" Errors

### Symptom:
Log entries show `Microsoft.Data.Sqlite.SqliteException: SQLite Error 5: 'database is locked'`.

### Cause:
Multiple operations attempting simultaneous writes without sufficient busy-timeout headroom, or the SQLite database is located on a network share (NFS/SMB) that does not support POSIX advisory locks properly.

### Resolution:
1. Ensure the SQLite database file resides on a **local filesystem** (ext4, XFS, APFS, or NTFS) rather than an NFS or CIFS network mount.
2. If running on Windows with Docker Desktop, place the project inside the WSL2 Linux distribution filesystem rather than `/mnt/c/`.
3. Verify that WAL mode is enabled:
   ```bash
   docker exec mytube sqlite3 /app/data/mytube.db "PRAGMA journal_mode;"
   # Expected output: wal
   ```

---

## 4. WireGuard VPN Fails to Connect / Torrent Inactive

### Symptom:
Gluetun logs show `wireguard handshake timeout` or `cannot configure wg0`.

### Resolution:
1. **Verify Secret Contents:** Ensure `secrets/wireguard_private_key.secret` and `secrets/wireguard_addresses.secret` do not contain trailing spaces or newlines.
2. **TUN Device Permissions:** Gluetun requires access to `/dev/net/tun` and the `NET_ADMIN` capability. Check that `deploy/docker-compose.torrent.yml` includes:
   ```yaml
   cap_add:
     - NET_ADMIN
   devices:
     - /dev/net/tun:/dev/net/tun
   ```
3. **Endpoint Reachability:** Verify that your firewall allows UDP outbound on your WireGuard port (typically 51820).

---

## 5. IPTV Stream Fails to Play (SSRF Protection)

### Symptom:
IPTV channel returns `400 Bad Request: Invalid or forbidden stream address`.

### Cause:
MyTube's SSRF validator blocks requests to:
- Private IP addresses (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`)
- Loopback addresses (`127.0.0.1`, `localhost`)
- Link-local and cloud metadata addresses (`169.254.169.254`)

### Resolution:
Ensure your IPTV playlist points to publicly routable internet URLs. If you are proxying through a local server on your LAN, configure the stream URL using your public domain or authorized proxy.

---

## 6. Exporting Sanitized Diagnostic Reports

If you need to report an issue or review system state:
1. Open the MyTube web dashboard in your browser.
2. Navigate to **System Health** in the sidebar.
3. Click the **Export Report** button.
4. The downloaded `mytube-diagnostics-<timestamp>.json` automatically masks all passwords, API keys, and sensitive tokens.

