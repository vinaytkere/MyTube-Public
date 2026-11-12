# Backup & Disaster Recovery Guide

This guide explains how to safely back up and restore your MyTube instance, database, encryption keys, and configurations.

---

## 1. Safe SQLite Hot Backups

MyTube operates SQLite in **Write-Ahead Logging (WAL)** mode. In WAL mode, recent transactions are written to a secondary file (`mytube.db-wal`) before being checkpointed back into `mytube.db`.

> **Warning:** Never use standard file-copy commands (`cp` or `rsync`) directly on `mytube.db` while the application container is running, as this may capture an inconsistent state or produce a corrupt backup file.

### Online Backup with `VACUUM INTO`

MyTube includes an automated hot-backup script (`deploy/scripts/backup.sh`) that triggers SQLite's transactional `VACUUM INTO` command. This ensures:
- The backup is taken safely while MyTube continues running and serving requests.
- All pending WAL frames are cleanly consolidated into a single standalone database file.
- Unused space is reclaimed and defragmented.

---

## 2. Running a Backup

Run the backup script from your host machine:

```bash
chmod +x deploy/scripts/backup.sh
./deploy/scripts/backup.sh
```

### What is Included in the Backup Archive?
- **SQLite Database:** A consolidated snapshot of `mytube.db`.
- **Data Protection Keys:** `/app/data/keys` containing ASP.NET Core cryptographic keys.
- **Secrets Directory:** `secrets/*.secret` (JWT keys, API credentials).
- **Environment Configuration:** Current `.env` file.

The resulting archive is saved as a timestamped tarball in `./backups/mytube-backup-YYYYMMDD_HHMMSS.tar.gz`.

*(Note: Media video files and downloads are excluded from the database archive to keep backup sizes minimal. Media directories should be backed up separately using standard file synchronization tools like `rsync` or `restic`).*

---

## 3. Restoring from a Backup

To restore MyTube from a previously created backup archive:

```bash
chmod +x deploy/scripts/restore.sh
./deploy/scripts/restore.sh backups/mytube-backup-20261112_120000.tar.gz
```

### Restoration Process:
1. Validates that the backup archive contains valid database and key structures.
2. Stops running MyTube containers to release file locks.
3. Restores `mytube.db`, cryptographic keys, and secret configurations.
4. Performs an automated integrity verification check:
   ```sql
   PRAGMA integrity_check;
   ```
5. Restarts the MyTube container.

---

## 4. Automated Daily Backups (Cron Example)

To schedule an automated daily backup on a Linux host at 3:00 AM:

```bash
# Open crontab editor
crontab -e

# Add the following line (adjust path to your installation directory):
0 3 * * * /opt/mytube/deploy/scripts/backup.sh >> /var/log/mytube-backup.log 2>&1
```

