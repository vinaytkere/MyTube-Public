#!/usr/bin/env bash
set -eo pipefail

# ==============================================================================
# MyTube SQLite Online Hot Backup Utility
# ==============================================================================
# Safely creates an atomic, consistent database snapshot while MyTube is active
# using SQLite's VACUUM INTO command, and validates backup integrity.
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/../.." && pwd)"
cd "${ROOT_DIR}"

TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BACKUP_DIR="${ROOT_DIR}/data/backups"
BACKUP_FILE="mytube_backup_${TIMESTAMP}.db"

mkdir -p "${BACKUP_DIR}"

echo "📦 Initiating atomic SQLite hot backup..."
if docker ps --format '{{.Names}}' | grep -q '^mytube$'; then
    # Run hot backup from inside the container using VACUUM INTO
    docker exec mytube sqlite3 /app/data/mytube.db "VACUUM INTO '/app/data/backups/${BACKUP_FILE}';"
    
    echo "🔍 Validating backup integrity with PRAGMA integrity_check..."
    INTEGRITY=$(docker exec mytube sqlite3 "/app/data/backups/${BACKUP_FILE}" "PRAGMA integrity_check;")
    
    if [ "$INTEGRITY" == "ok" ]; then
        echo "✅ Backup successfully verified: ${BACKUP_DIR}/${BACKUP_FILE}"
    else
        echo "❌ Integrity check failed: $INTEGRITY"
        exit 1
    fi
else
    # Host-based fallback if container is stopped
    if [ -f "./data/mytube.db" ]; then
        sqlite3 "./data/mytube.db" "VACUUM INTO '${BACKUP_DIR}/${BACKUP_FILE}';"
        sqlite3 "${BACKUP_DIR}/${BACKUP_FILE}" "PRAGMA integrity_check;"
        echo "✅ Offline backup verified: ${BACKUP_DIR}/${BACKUP_FILE}"
    else
        echo "❌ Error: ./data/mytube.db not found."
        exit 1
    fi
fi

