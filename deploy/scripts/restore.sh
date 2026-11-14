#!/usr/bin/env bash
set -eo pipefail

# ==============================================================================
# MyTube Database Restore Utility
# ==============================================================================
# Restores a verified SQLite backup, cleans stale WAL/SHM files, and restarts services.
# Usage: ./restore.sh <path_to_backup.db>
# ==============================================================================

if [ -z "$1" ]; then
    echo "Usage: $0 <path_to_backup.db>"
    echo "Example: $0 ./data/backups/mytube_backup_20261112_120000.db"
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/../.." && pwd)"

RESTORE_SOURCE="$1"
if [[ "$RESTORE_SOURCE" != /* ]]; then
    RESTORE_SOURCE="$(pwd)/${RESTORE_SOURCE}"
fi

cd "${ROOT_DIR}"

if [ ! -f "$RESTORE_SOURCE" ]; then
    echo "❌ Error: Backup file '$RESTORE_SOURCE' does not exist."
    exit 1
fi

echo "🔍 Validating backup integrity before restoration..."
INTEGRITY=$(sqlite3 "$RESTORE_SOURCE" "PRAGMA integrity_check;")
if [ "$INTEGRITY" != "ok" ]; then
    echo "❌ Refusing to restore: Integrity check failed ($INTEGRITY)."
    exit 1
fi
echo "✓ Backup integrity is verified (ok)."

echo "🛑 Stopping MyTube containers to release SQLite file locks..."
docker compose -f ./deploy/docker-compose.yml down 2>/dev/null || true

echo "📦 Restoring database file..."
cp -f "$RESTORE_SOURCE" "./data/mytube.db"

# Clear any stale Write-Ahead Log or Shared Memory files from previous execution
rm -f "./data/mytube.db-wal" "./data/mytube.db-shm"

echo "🚀 Restarting MyTube services..."
docker compose -f ./deploy/docker-compose.yml up -d

echo "✅ Restoration completed successfully."

