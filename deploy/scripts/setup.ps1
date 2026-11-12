# ==============================================================================
# MyTube Public Edition - Interactive Setup Wizard (Windows PowerShell)
# ==============================================================================
# Sets up Docker Compose, persistent volumes, and secure secrets on Windows.
# ==============================================================================

$ErrorActionPreference = "Stop"

Write-Host "  __  __       _______     _          " -ForegroundColor Cyan
Write-Host " |  \/  |     |__   __|   | |         " -ForegroundColor Cyan
Write-Host " | \  / |_   _   | |_   _ | |__   ___ " -ForegroundColor Cyan
Write-Host " | |\/| | | | |  | | | | || '_ \ / _ \" -ForegroundColor Cyan
Write-Host " | |  | | |_| |  | | |_| || |_) |  __/" -ForegroundColor Cyan
Write-Host " |_|  |_|\__, |  |_|\__,_||_.__/ \___|" -ForegroundColor Cyan
Write-Host "          __/ |                       " -ForegroundColor Cyan
Write-Host "         |___/    Public Edition      " -ForegroundColor Cyan
Write-Host ""
Write-Host "Welcome to the MyTube Self-Hosted Media Platform Setup Wizard!" -ForegroundColor Green
Write-Host ""

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = (Resolve-Path "$ScriptDir\..\..").Path
$DeployDir = "$RootDir\deploy"
$SecretsDir = "$DeployDir\secrets"

Set-Location $RootDir

# 1. Check Docker & Compose
Write-Host "[1/16] Checking Docker and Docker Compose..." -ForegroundColor Cyan
if (-not (Get-Command "docker" -ErrorAction SilentlyContinue)) {
    Write-Host "Error: Docker Desktop is not installed or not in PATH." -ForegroundColor Red
    Write-Host "Please install Docker Desktop for Windows and ensure WSL2 backend is active."
    exit 1
}

$composeCheck = docker compose version 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Docker Compose v2 is required but not found." -ForegroundColor Red
    exit 1
}
Write-Host "✓ Docker and Docker Compose are ready." -ForegroundColor Green

# 2. Check Storage Space
Write-Host "`n[2/16] Checking storage capacity..." -ForegroundColor Cyan
$drive = Get-PSDrive -Name ($RootDir.Substring(0,1))
$freeGb = [math]::Round($drive.Free / 1GB, 2)
if ($freeGb -lt 2) {
    Write-Host "Warning: Less than 2 GB of free disk space available ($freeGb GB detected)." -ForegroundColor Yellow
} else {
    Write-Host "✓ Available disk space: $freeGb GB." -ForegroundColor Green
}

# 3. Create Application Directories
Write-Host "`n[3/16] Preparing application directories..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path "$RootDir\data" | Out-Null
New-Item -ItemType Directory -Force -Path "$RootDir\data\keys" | Out-Null
New-Item -ItemType Directory -Force -Path "$RootDir\media" | Out-Null
New-Item -ItemType Directory -Force -Path "$RootDir\media\downloads" | Out-Null
New-Item -ItemType Directory -Force -Path $SecretsDir | Out-Null
Write-Host "✓ Directories created successfully." -ForegroundColor Green

# 4. Generate Cryptographic Keys
Write-Host "`n[4/16] Generating cryptographic security keys..." -ForegroundColor Cyan
function Generate-RandomSecret([int]$bytes = 32) {
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $buff = New-Object byte[] $bytes
    $rng.GetBytes($buff)
    return [System.BitConverter]::ToString($buff).Replace("-","").ToLower()
}

$jwtFile = "$SecretsDir\jwt_secret.txt"
if (-not (Test-Path $jwtFile) -or (Get-Item $jwtFile).Length -eq 0) {
    (Generate-RandomSecret 32) | Out-File -FilePath $jwtFile -Encoding ascii -NoNewline
    Write-Host "✓ Generated secure 64-character JWT signing secret." -ForegroundColor Green
} else {
    Write-Host "✓ Existing JWT secret preserved." -ForegroundColor Green
}

$encFile = "$SecretsDir\encryption_key.txt"
if (-not (Test-Path $encFile) -or (Get-Item $encFile).Length -eq 0) {
    (Generate-RandomSecret 32) | Out-File -FilePath $encFile -Encoding ascii -NoNewline
    Write-Host "✓ Generated AES application data protection key." -ForegroundColor Green
} else {
    Write-Host "✓ Existing encryption key preserved." -ForegroundColor Green
}

# 5. Initial Admin
Write-Host "`n[5/16] Initial Administrator Setup" -ForegroundColor Cyan
$preAdmin = Read-Host "Pre-configure Administrator credentials now? [y/N]"
$adminUser = ""
$adminPass = ""
if ($preAdmin -match "^[Yy]") {
    $adminUser = Read-Host "Administrator Username"
    $secPass = Read-Host -AsSecureString "Administrator Password (min 8 chars)"
    $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secPass)
    $adminPass = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
}

# 6. Torrent Support
Write-Host "`n[6/16] Torrent & WireGuard VPN Subsystem" -ForegroundColor Cyan
$enableTorrentIn = Read-Host "Enable Torrent downloader with WireGuard VPN? [y/N]"
$enableTorrent = if ($enableTorrentIn -match "^[Yy]") { "true" } else { "false" }

# 7. Jellyfin Support
Write-Host "`n[7/16] Jellyfin Media Server" -ForegroundColor Cyan
$enableJellyfinIn = Read-Host "Enable Jellyfin media streaming server? [y/N]"
$enableJellyfin = if ($enableJellyfinIn -match "^[Yy]") { "true" } else { "false" }

# 8. AI Support
Write-Host "`n[8/16] AI Media Intelligence" -ForegroundColor Cyan
$enableAiIn = Read-Host "Enable AI media features? [y/N]"
$enableAi = if ($enableAiIn -match "^[Yy]") { "true" } else { "false" }

# 9. AI Keys
$geminiFile = "$SecretsDir\gemini_api_key.txt"
$groqFile = "$SecretsDir\groq_api_key.txt"
$deepseekFile = "$SecretsDir\deepseek_api_key.txt"
if (-not (Test-Path $geminiFile)) { "" | Out-File -FilePath $geminiFile -Encoding ascii -NoNewline }
if (-not (Test-Path $groqFile)) { "" | Out-File -FilePath $groqFile -Encoding ascii -NoNewline }
if (-not (Test-Path $deepseekFile)) { "" | Out-File -FilePath $deepseekFile -Encoding ascii -NoNewline }

if ($enableAi -eq "true") {
    Write-Host "`n[9/16] Optional AI Provider Keys (Press Enter to skip any)" -ForegroundColor Cyan
    $geminiKey = Read-Host "Enter Google Gemini API Key"
    if ($geminiKey) { $geminiKey | Out-File -FilePath $geminiFile -Encoding ascii -NoNewline }
    
    $groqKey = Read-Host "Enter Groq API Key"
    if ($groqKey) { $groqKey | Out-File -FilePath $groqFile -Encoding ascii -NoNewline }
    
    $deepseekKey = Read-Host "Enter DeepSeek API Key"
    if ($deepseekKey) { $deepseekKey | Out-File -FilePath $deepseekFile -Encoding ascii -NoNewline }
}

# 10. WireGuard Keys
$wgFile = "$SecretsDir\wireguard_private_key.txt"
if (-not (Test-Path $wgFile)) { "" | Out-File -FilePath $wgFile -Encoding ascii -NoNewline }
$vpnProvider = "custom"
$vpnAddresses = ""

if ($enableTorrent -eq "true") {
    Write-Host "`n[10/16] WireGuard VPN Configuration" -ForegroundColor Cyan
    $vpnProviderIn = Read-Host "VPN Provider (mullvad/protonvpn/ivpn/airvpn/custom) [custom]"
    if ($vpnProviderIn) { $vpnProvider = $vpnProviderIn }
    $vpnAddresses = Read-Host "WireGuard Assigned Client IP/CIDR (e.g. 10.2.0.2/32)"
    $wgKey = Read-Host "WireGuard Private Key"
    if ($wgKey) { $wgKey | Out-File -FilePath $wgFile -Encoding ascii -NoNewline }
}

# 11. Paths and Port
Write-Host "`n[11/16] Path and Port Configuration" -ForegroundColor Cyan
$portIn = Read-Host "Port to expose on host [5003]"
$port = if ($portIn) { $portIn } else { "5003" }

$mediaIn = Read-Host "Host Media Directory [./media]"
$mediaDir = if ($mediaIn) { $mediaIn } else { "./media" }

$downIn = Read-Host "Host Downloads Directory [./media/downloads]"
$downDir = if ($downIn) { $downIn } else { "./media/downloads" }

# 12. Write .env
Write-Host "`n[12/16] Writing configuration (.env)..." -ForegroundColor Cyan
$envContent = @"
PORT=$port
ASPNETCORE_ENVIRONMENT=Production
DATA_DIR=./data
MEDIA_DIR=$mediaDir
DOWNLOADS_DIR=$downDir
CORS_ALLOWED_ORIGINS=http://localhost:$port,http://127.0.0.1:$port

ENABLE_TORRENT=$enableTorrent
ENABLE_JELLYFIN=$enableJellyfin
ENABLE_AI=$enableAi

VPN_PROVIDER=$vpnProvider
VPN_WIREGUARD_ADDRESSES=$vpnAddresses
TRANSMISSION_USER=admin
TRANSMISSION_PASS=admin

Auth__DefaultAdmin__Username=$adminUser
Auth__DefaultAdmin__Password=$adminPass
"@

$envContent | Out-File -FilePath "$RootDir\.env" -Encoding ascii
Write-Host "✓ .env written successfully." -ForegroundColor Green

# 13. Validate
Write-Host "`n[13/16] Validating configuration..." -ForegroundColor Cyan
if ((Get-Item $jwtFile).Length -lt 32) {
    Write-Host "Validation Failed: JWT secret file is empty or too short." -ForegroundColor Red
    exit 1
}
Write-Host "✓ Configuration validated successfully." -ForegroundColor Green

# 14. Start Docker
Write-Host "`n[14/16] Launching Docker services..." -ForegroundColor Cyan
$composeArgs = @("-f", "$DeployDir\docker-compose.yml")
if ($enableTorrent -eq "true") {
    $composeArgs += @("-f", "$DeployDir\docker-compose.torrent.yml")
}
if ($enableJellyfin -eq "true") {
    $composeArgs += @("-f", "$DeployDir\docker-compose.jellyfin.yml")
}

& docker compose @composeArgs up -d --build

# 15. Health Check
Write-Host "`n[15/16] Waiting for service health checks..." -ForegroundColor Cyan
$maxWait = 15
$healthy = $false
for ($i = 0; $i -lt $maxWait; $i++) {
    try {
        $res = Invoke-WebRequest -Uri "http://localhost:$port/health" -UseBasicParsing -TimeoutSec 2
        if ($res.StatusCode -eq 200) {
            $healthy = $true
            break
        }
    } catch { }
    Start-Sleep -Seconds 2
    Write-Host "." -NoNewline
}
Write-Host ""

if ($healthy) {
    Write-Host "✓ Backend is healthy and responding." -ForegroundColor Green
} else {
    Write-Host "Notice: Service is starting up in the background. Check logs with 'docker compose logs -f'." -ForegroundColor Yellow
}

# 16. Summary
Write-Host "`n======================================================================" -ForegroundColor Green
Write-Host "  MyTube Public Edition is ready!" -ForegroundColor Green
Write-Host "======================================================================" -ForegroundColor Green
Write-Host "  Web Application:   http://localhost:$port" -ForegroundColor Cyan
if ($enableTorrent -eq "true") {
    Write-Host "  Transmission Web:  http://localhost:9091 (Fail-Closed WireGuard)" -ForegroundColor Cyan
}
if ($enableJellyfin -eq "true") {
    Write-Host "  Jellyfin Server:   http://localhost:8096" -ForegroundColor Cyan
}
Write-Host "`n  Management Commands:"
Write-Host "    Stop Services:   docker compose down"
Write-Host "    View Logs:       docker compose logs -f"
Write-Host "    Re-run Setup:    powershell ./deploy/scripts/setup.ps1"
Write-Host "======================================================================`n" -ForegroundColor Green

