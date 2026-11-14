# MyTube Configuration Reference

MyTube uses a hybrid configuration model:
1. **Non-sensitive settings** are placed in the `.env` file or passed as environment variables.
2. **Sensitive credentials and API keys** are read from Docker secrets mounted at `/run/secrets/` (or fallback host files under `secrets/*.secret`).

---

## 1. Environment Variables (`.env`)

| Variable | Default Value | Description |
| -------- | ------------- | ----------- |
| `PORT` | `5003` | Host port on which MyTube web dashboard and API listen. |
| `ASPNETCORE_ENVIRONMENT` | `Production` | ASP.NET Core environment mode (`Production` or `Development`). |
| `DATA_DIR` | `../data` | Path to persistent application data, SQLite database, and keys (relative to deploy/ or absolute). |
| `MEDIA_DIR` | `../media` | Path to host media library (relative to deploy/ or absolute). |
| `DOWNLOADS_DIR` | `../media/downloads` | Path to torrent and direct download staging directory. |
| `CORS_ALLOWED_ORIGINS` | `http://localhost:5003,http://127.0.0.1:5003` | Comma-separated list of allowed CORS origins for API requests. |
| `ENABLE_TORRENT` | `false` | Set to `true` to launch Gluetun WireGuard VPN + Transmission torrent client. |
| `ENABLE_JELLYFIN` | `false` | Set to `true` to launch integrated Jellyfin streaming server. |
| `ENABLE_AI` | `false` | Set to `true` to enable AI media features. |
| `VPN_PROVIDER` | `custom` | Gluetun VPN provider (e.g. `mullvad`, `protonvpn`, `ivpn`, `airvpn`, `custom`). |
| `VPN_WIREGUARD_ADDRESSES` | `10.2.0.2/32` | WireGuard client assigned IP/CIDR. |
| `Auth__DefaultAdmin__Username` | | Pre-configured username for the initial Administrator account. |
| `Auth__DefaultAdmin__Password` | | Pre-configured password for the initial Administrator account. |

---

## 2. Docker Compose Secrets (`/run/secrets/`)

All sensitive values are placed inside individual files in the `deploy/secrets/` directory on the host machine. The Docker Compose configuration mounts these files into the container at `/run/secrets/<secret_name>`.

| Secret File | Container Secret Mount | Description |
| ----------- | ---------------------- | ----------- |
| `deploy/secrets/jwt_secret.txt` | `/run/secrets/jwt_secret` | **Required.** Cryptographic signing secret for user authentication tokens. Minimum 32 characters. |
| `deploy/secrets/encryption_key.txt` | `/run/secrets/optional_encryption_key` | Optional. Key used for symmetric data encryption. |
| `deploy/secrets/gemini_api_key.txt` | `/run/secrets/optional_gemini_api_key` | Optional. Google Gemini API key for AI summaries and video categorization. |
| `deploy/secrets/groq_api_key.txt` | `/run/secrets/optional_groq_api_key` | Optional. Groq Cloud API key for Llama and Whisper STT. |
| `deploy/secrets/deepseek_api_key.txt` | `/run/secrets/optional_deepseek_api_key` | Optional. DeepSeek API key for intelligent tagging. |
| `deploy/secrets/wireguard_private_key.txt` | `/run/secrets/wireguard_private_key` | Required when torrent VPN is enabled. WireGuard client private key. |

---

## 3. Secret Resolution Precedence

When MyTube starts up, `SecretManagementService` resolves credential values in the following order:

1. **Docker Secret File (`/run/secrets/<NAME>`):** Highest priority. If the file exists and is not empty, its contents are used.
2. **Host Secret File (`secrets/<NAME>.secret`):** Used during local development if running outside Docker.
3. **Environment Variable (`<NAME>`):** Fallback mechanism if no secret file is present.
4. **Configuration Setting (`appsettings.json`):** Default fallback.

---

## 4. Secret Rotation Procedures

To rotate credentials (for instance, updating your JWT secret or an AI API key):

1. Update or replace the target secret file in `secrets/`:
   ```bash
   # Update Gemini API Key
   echo "NEW_GEMINI_KEY_HERE" > secrets/gemini_api_key.secret
   chmod 600 secrets/gemini_api_key.secret
   ```
2. Restart the MyTube backend container to pick up the updated file mount:
   ```bash
   docker compose -f deploy/docker-compose.yml restart mytube
   ```
3. Verify the application logs:
   ```bash
   docker compose -f deploy/docker-compose.yml logs -f mytube
   ```
*(Note: Rotating the `jwt_secret` will invalidate existing user sessions, requiring users to log in again).*

