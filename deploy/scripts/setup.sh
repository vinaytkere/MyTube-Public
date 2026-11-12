#!/usr/bin/env bash
set -eo pipefail

# ==============================================================================
# MyTube Public Edition - Interactive Setup Wizard
# ==============================================================================
# Cross-platform onboarding script for Linux, macOS, and WSL2.
# Sets up Docker Compose, persistent volumes, and secure secrets.
# ==============================================================================

BOLD="\033[1m"
GREEN="\033[0;32m"
CYAN="\033[0;36m"
YELLOW="\033[1;33m"
RED="\033[0;31m"
RESET="\033[0m"

echo -e "${CYAN}${BOLD}"
echo "  __  __       _______     _          "
echo " |  \/  |     |__   __|   | |         "
echo " | \  / |_   _   | |_   _ | |__   ___ "
echo " | |\/| | | | |  | | | | || '_ \ / _ \\"
echo " | |  | | |_| |  | | |_| || |_) |  __/"
echo " |_|  |_|\__, |  |_|\__,_||_.__/ \___|"
echo "          __/ |                       "
echo "         |___/    Public Edition      "
echo -e "${RESET}"
echo -e "${BOLD}Welcome to the MyTube Self-Hosted Media Platform Setup Wizard!${RESET}\n"

# Locate base directories
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/../.." && pwd)"
DEPLOY_DIR="${ROOT_DIR}/deploy"
SECRETS_DIR="${DEPLOY_DIR}/secrets"

cd "${ROOT_DIR}"

# ------------------------------------------------------------------------------
# Step 1: Check Docker and Docker Compose
# ------------------------------------------------------------------------------
echo -e "${CYAN}[1/16] Checking Docker and Docker Compose...${RESET}"
if ! command -v docker &> /dev/null; then
    echo -e "${RED}Error: Docker is not installed or not in PATH.${RESET}"
    echo "Please install Docker from https://docs.docker.com/get-docker/ and re-run setup."
    exit 1
fi

if ! docker compose version &> /dev/null; then
    echo -e "${RED}Error: Docker Compose v2 is required but not found.${RESET}"
    exit 1
fi
echo -e "${GREEN}✓ Docker and Docker Compose are ready.${RESET}"

# ------------------------------------------------------------------------------
# Step 2: Check Disk Space
# ------------------------------------------------------------------------------
echo -e "\n${CYAN}[2/16] Checking storage capacity...${RESET}"
FREE_KB=$(df -k . | awk 'NR==2 {print $4}')
FREE_GB=$((FREE_KB / 1024 / 1024))
if [ "$FREE_GB" -lt 2 ]; then
    echo -e "${YELLOW}Warning: Less than 2 GB of free disk space available (${FREE_GB} GB detected).${RESET}"
else
    echo -e "${GREEN}✓ Available disk space: ${FREE_GB} GB.${RESET}"
fi

# ------------------------------------------------------------------------------
# Step 3: Create Application Directories
# ------------------------------------------------------------------------------
echo -e "\n${CYAN}[3/16] Preparing application directories...${RESET}"
mkdir -p "${ROOT_DIR}/data" "${ROOT_DIR}/data/keys" "${ROOT_DIR}/media" "${ROOT_DIR}/media/downloads" "${SECRETS_DIR}"
chmod 700 "${SECRETS_DIR}"
echo -e "${GREEN}✓ Directories created with secure permissions.${RESET}"

# ------------------------------------------------------------------------------
# Step 4: Cryptographic Secret Generation
# ------------------------------------------------------------------------------
echo -e "\n${CYAN}[4/16] Generating cryptographic security keys...${RESET}"
generate_secret() {
    if command -v openssl &> /dev/null; then
        openssl rand -hex 32
    else
        head -c 32 /dev/urandom | xxd -p -c 32
    fi
}

JWT_SECRET_FILE="${SECRETS_DIR}/jwt_secret.txt"
if [ ! -f "${JWT_SECRET_FILE}" ] || [ ! -s "${JWT_SECRET_FILE}" ]; then
    generate_secret > "${JWT_SECRET_FILE}"
    chmod 600 "${JWT_SECRET_FILE}"
    echo -e "${GREEN}✓ Generated secure 64-character JWT signing secret.${RESET}"
else
    echo -e "${GREEN}✓ Existing JWT secret preserved.${RESET}"
fi

ENC_KEY_FILE="${SECRETS_DIR}/encryption_key.txt"
if [ ! -f "${ENC_KEY_FILE}" ] || [ ! -s "${ENC_KEY_FILE}" ]; then
    generate_secret > "${ENC_KEY_FILE}"
    chmod 600 "${ENC_KEY_FILE}"
    echo -e "${GREEN}✓ Generated AES application data protection key.${RESET}"
else
    echo -e "${GREEN}✓ Existing encryption key preserved.${RESET}"
fi

# ------------------------------------------------------------------------------
# Step 5: Initial Administrator Account
# ------------------------------------------------------------------------------
echo -e "\n${CYAN}[5/16] Initial Administrator Setup${RESET}"
echo "The first registered user automatically becomes the Administrator."
echo "You can create this account upon first visiting the Web UI, or pre-configure it now."
read -p "Pre-configure Administrator credentials now? [y/N]: " SETUP_ADMIN_PRE
SETUP_ADMIN_PRE=${SETUP_ADMIN_PRE:-N}

ADMIN_USER_CONFIG=""
ADMIN_PASS_CONFIG=""
if [[ "$SETUP_ADMIN_PRE" =~ ^[Yy]$ ]]; then
    read -p "Administrator Username: " ADMIN_USER
    read -s -p "Administrator Password (min 8 chars): " ADMIN_PASS
    echo ""
    ADMIN_USER_CONFIG="${ADMIN_USER}"
    ADMIN_PASS_CONFIG="${ADMIN_PASS}"
fi

# ------------------------------------------------------------------------------
# Step 6: Torrent Functionality
# ------------------------------------------------------------------------------
echo -e "\n${CYAN}[6/16] Torrent & WireGuard VPN Subsystem${RESET}"
echo "Enables Transmission BitTorrent client encapsulated in a Gluetun WireGuard tunnel."
echo "Includes fail-closed killswitch behavior: torrents stop completely if VPN disconnects."
read -p "Enable Torrent downloader with WireGuard VPN? [y/N]: " ENABLE_TORRENT_INPUT
ENABLE_TORRENT_INPUT=${ENABLE_TORRENT_INPUT:-N}
ENABLE_TORRENT=false
if [[ "$ENABLE_TORRENT_INPUT" =~ ^[Yy]$ ]]; then
    ENABLE_TORRENT=true
fi

# ------------------------------------------------------------------------------
# Step 7: Jellyfin Media Server
# ------------------------------------------------------------------------------
echo -e "\n${CYAN}[7/16] Jellyfin Media Server${RESET}"
echo "Enables an integrated Jellyfin instance mounting the shared media library."
read -p "Enable Jellyfin media streaming server? [y/N]: " ENABLE_JELLYFIN_INPUT
ENABLE_JELLYFIN_INPUT=${ENABLE_JELLYFIN_INPUT:-N}
ENABLE_JELLYFIN=false
if [[ "$ENABLE_JELLYFIN_INPUT" =~ ^[Yy]$ ]]; then
    ENABLE_JELLYFIN=true
fi

# ------------------------------------------------------------------------------
# Step 8: AI Features
# ------------------------------------------------------------------------------
echo -e "\n${CYAN}[8/16] AI Media Intelligence${RESET}"
echo "Enables speech-to-text, video Q&A, and auto-summarization using cloud models or local Ollama."
read -p "Enable AI media features? [y/N]: " ENABLE_AI_INPUT
ENABLE_AI_INPUT=${ENABLE_AI_INPUT:-N}
ENABLE_AI=false
if [[ "$ENABLE_AI_INPUT" =~ ^[Yy]$ ]]; then
    ENABLE_AI=true
fi

# ------------------------------------------------------------------------------
# Step 9: Prompt for AI Credentials (Only if AI Enabled)
# ------------------------------------------------------------------------------
GEMINI_KEY_FILE="${SECRETS_DIR}/gemini_api_key.txt"
GROQ_KEY_FILE="${SECRETS_DIR}/groq_api_key.txt"
DEEPSEEK_KEY_FILE="${SECRETS_DIR}/deepseek_api_key.txt"

touch "${GEMINI_KEY_FILE}" "${GROQ_KEY_FILE}" "${DEEPSEEK_KEY_FILE}"
chmod 600 "${GEMINI_KEY_FILE}" "${GROQ_KEY_FILE}" "${DEEPSEEK_KEY_FILE}"

if [ "$ENABLE_AI" = true ]; then
    echo -e "\n${CYAN}[9/16] Optional AI Provider Keys (Press Enter to skip any)${RESET}"
    read -s -p "Enter Google Gemini API Key: " INPUT_GEMINI; echo ""
    if [ -n "$INPUT_GEMINI" ]; then echo -n "$INPUT_GEMINI" > "${GEMINI_KEY_FILE}"; fi

    read -s -p "Enter Groq API Key: " INPUT_GROQ; echo ""
    if [ -n "$INPUT_GROQ" ]; then echo -n "$INPUT_GROQ" > "${GROQ_KEY_FILE}"; fi

    read -s -p "Enter DeepSeek API Key: " INPUT_DEEPSEEK; echo ""
    if [ -n "$INPUT_DEEPSEEK" ]; then echo -n "$INPUT_DEEPSEEK" > "${DEEPSEEK_KEY_FILE}"; fi
fi

# ------------------------------------------------------------------------------
# Step 10: WireGuard VPN Credentials (Only if Torrents Enabled)
# ------------------------------------------------------------------------------
WG_KEY_FILE="${SECRETS_DIR}/wireguard_private_key.txt"
touch "${WG_KEY_FILE}"
chmod 600 "${WG_KEY_FILE}"
VPN_PROVIDER="custom"
VPN_ADDRESSES=""

if [ "$ENABLE_TORRENT" = true ]; then
    echo -e "\n${CYAN}[10/16] WireGuard VPN Configuration${RESET}"
    echo "Requires WireGuard settings from your VPN provider (see docs/vpn-setup.md)."
    echo "Tip: In your provider's .conf file, look for 'Address' and 'PrivateKey' under [Interface]."
    read -p "VPN Provider (mullvad/protonvpn/ivpn/airvpn/custom) [custom]: " VPN_PROVIDER_IN
    VPN_PROVIDER=${VPN_PROVIDER_IN:-custom}

    read -p "WireGuard Assigned Client IP/CIDR (e.g. 10.2.0.2/32): " VPN_ADDRESSES
    read -s -p "WireGuard Private Key: " INPUT_WG_KEY; echo ""
    if [ -n "$INPUT_WG_KEY" ]; then echo -n "$INPUT_WG_KEY" > "${WG_KEY_FILE}"; fi
fi

# ------------------------------------------------------------------------------
# Step 11: Configure Media and Network Paths
# ------------------------------------------------------------------------------
echo -e "\n${CYAN}[11/16] Path and Port Configuration${RESET}"
read -p "Port to expose on host [5003]: " INPUT_PORT
PORT=${INPUT_PORT:-5003}

read -p "Host Media Directory [./media]: " INPUT_MEDIA
MEDIA_DIR=${INPUT_MEDIA:-./media}

read -p "Host Downloads Directory [./media/downloads]: " INPUT_DOWNLOADS
DOWNLOADS_DIR=${INPUT_DOWNLOADS:-./media/downloads}

# ------------------------------------------------------------------------------
# Step 12: Write .env Configuration File
# ------------------------------------------------------------------------------
echo -e "\n${CYAN}[12/16] Writing configuration (.env)...${RESET}"
cat > "${ROOT_DIR}/.env.tmp" <<EOF
PORT=${PORT}
ASPNETCORE_ENVIRONMENT=Production
DATA_DIR=./data
MEDIA_DIR=${MEDIA_DIR}
DOWNLOADS_DIR=${DOWNLOADS_DIR}
CORS_ALLOWED_ORIGINS=http://localhost:${PORT},http://127.0.0.1:${PORT}

ENABLE_TORRENT=${ENABLE_TORRENT}
ENABLE_JELLYFIN=${ENABLE_JELLYFIN}
ENABLE_AI=${ENABLE_AI}

VPN_PROVIDER=${VPN_PROVIDER}
VPN_WIREGUARD_ADDRESSES=${VPN_ADDRESSES}
TRANSMISSION_USER=admin
TRANSMISSION_PASS=admin

Auth__DefaultAdmin__Username=${ADMIN_USER_CONFIG}
Auth__DefaultAdmin__Password=${ADMIN_PASS_CONFIG}
EOF

mv -f "${ROOT_DIR}/.env.tmp" "${ROOT_DIR}/.env"
chmod 600 "${ROOT_DIR}/.env"
echo -e "${GREEN}✓ .env written securely.${RESET}"

# ------------------------------------------------------------------------------
# Step 13: Validate Configuration
# ------------------------------------------------------------------------------
echo -e "\n${CYAN}[13/16] Validating configuration...${RESET}"
if [ ! -s "${JWT_SECRET_FILE}" ]; then
    echo -e "${RED}Validation Failed: JWT secret file is empty.${RESET}"
    exit 1
fi
echo -e "${GREEN}✓ Configuration validated successfully.${RESET}"

# ------------------------------------------------------------------------------
# Step 14: Start Docker Services
# ------------------------------------------------------------------------------
echo -e "\n${CYAN}[14/16] Launching Docker services...${RESET}"
COMPOSE_ARGS=("-f" "${DEPLOY_DIR}/docker-compose.yml")

if [ "$ENABLE_TORRENT" = true ]; then
    COMPOSE_ARGS+=("-f" "${DEPLOY_DIR}/docker-compose.torrent.yml")
fi
if [ "$ENABLE_JELLYFIN" = true ]; then
    COMPOSE_ARGS+=("-f" "${DEPLOY_DIR}/docker-compose.jellyfin.yml")
fi

docker compose "${COMPOSE_ARGS[@]}" up -d --build

# ------------------------------------------------------------------------------
# Step 15: Health Check Verification
# ------------------------------------------------------------------------------
echo -e "\n${CYAN}[15/16] Waiting for service health checks...${RESET}"
MAX_WAIT=30
WAITED=0
HEALTHY=false

while [ $WAITED -lt $MAX_WAIT ]; do
    if curl -s -f "http://localhost:${PORT}/health" > /dev/null 2>&1; then
        HEALTHY=true
        break
    fi
    sleep 2
    WAITED=$((WAITED + 2))
    echo -n "."
done
echo ""

if [ "$HEALTHY" = true ]; then
    echo -e "${GREEN}✓ Backend is healthy and responding.${RESET}"
else
    echo -e "${YELLOW}Notice: Service is starting up in the background. Check logs with 'docker compose logs -f'.${RESET}"
fi

# ------------------------------------------------------------------------------
# Step 16: Summary and Access Info
# ------------------------------------------------------------------------------
echo -e "\n${GREEN}${BOLD}======================================================================${RESET}"
echo -e "${GREEN}${BOLD}  MyTube Public Edition is ready!${RESET}"
echo -e "${GREEN}${BOLD}======================================================================${RESET}"
echo -e "  Web Application:   ${CYAN}http://localhost:${PORT}${RESET}"
if [ "$ENABLE_TORRENT" = true ]; then
    echo -e "  Transmission Web:  ${CYAN}http://localhost:9091${RESET} (Fail-Closed WireGuard)"
fi
if [ "$ENABLE_JELLYFIN" = true ]; then
    echo -e "  Jellyfin Server:   ${CYAN}http://localhost:8096${RESET}"
fi
echo -e "\n  Management Commands:"
echo -e "    Stop Services:   ${BOLD}docker compose down${RESET}"
echo -e "    View Logs:       ${BOLD}docker compose logs -f${RESET}"
echo -e "    Re-run Setup:    ${BOLD}./deploy/scripts/setup.sh${RESET}"
echo -e "${GREEN}${BOLD}======================================================================${RESET}\n"

