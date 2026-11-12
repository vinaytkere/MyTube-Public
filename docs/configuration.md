# MyTube Configuration Reference

MyTube uses a hybrid configuration model:
1. **Non-sensitive settings** are placed in the `.env` file or passed as environment variables.
2. **Sensitive credentials and API keys** are read from Docker secrets mounted at `/run/secrets/` (or fallback host files under `secrets/*.secret`).

---

## 1. Environment Variables (`.env`)

| Variable | Default Value | Description |
| -------- | ------------- | ----------- |
| `APP_PORT` | `5003` | Host port on which MyTube web dashboard and API listen. |
| `ASPNETCORE_ENVIRONMENT` | `Production` | ASP.NET Core environment mode (`Production` or `Development`). |
| `DATA_PATH` | `./data` | Host path for persistent application data, SQLite database, and keys. |
| `MEDIA_PATH` | `./media` | Host path where your organized video/media library resides. |
| `DOWNLOADS_PATH` | `./downloads` | Host path where in-progress and completed downloads are saved. |
| `ALLOWED_CORS_ORIGINS` | `http://localhost:5003` | Comma-separated list of allowed CORS origins for API requests. |
| `AI_PROVIDER` | `none` | Default AI provider: `none`, `gemini`, `groq`, `deepseek`, or `ollama`. |
| `OLLAMA_ENDPOINT` | `http://host.docker.internal:11434` | Endpoint URL when using local Ollama for AI metadata. |
| `VPN_SERVICE_PROVIDER` | `custom` | Gluetun VPN provider (e.g., `mullvad`, `protonvpn`, `nordvpn`, `custom`). |
| `SERVER_COUNTRIES` | `Netherlands` | Country selection for commercial VPN providers. |
| `JELLYFIN_PORT` | `8096` | Host port for optional Jellyfin media server. |

---

## 2. Docker Compose Secrets (`/run/secrets/`)

All sensitive values should be placed inside individual files in the `secrets/` directory on the host machine. The Docker Compose configuration mounts these files into the container at `/run/secrets/<secret_name>`.

| Secret File | Container Secret Mount | Description |
| ----------- | ---------------------- | ----------- |
| `secrets/jwt_secret.secret` | `/run/secrets/jwt_secret` | **Required.** Cryptographic signing secret for user authentication tokens. Must be at least 32 characters (256 bits). |
| `secrets/app_encryption_key.secret` | `/run/secrets/app_encryption_key` | Optional. Key used for symmetric data encryption. |
| `secrets/gemini_api_key.secret` | `/run/secrets/gemini_api_key` | Optional. Google Gemini API key for AI summaries and video categorization. |
| `secrets/groq_api_key.secret` | `/run/secrets/groq_api_key` | Optional. Groq Cloud API key for ultra-fast Llama-based video summarization. |
| `secrets/deepseek_api_key.secret` | `/run/secrets/deepseek_api_key` | Optional. DeepSeek API key for intelligent tagging and media extraction. |
| `secrets/wireguard_private_key.secret` | `/run/secrets/wireguard_private_key` | Required when torrent VPN is enabled. WireGuard client private key. |
| `secrets/wireguard_addresses.secret` | `/run/secrets/wireguard_addresses` | Required when torrent VPN is enabled. WireGuard client assigned IP (e.g. `10.2.0.2/32`). |
| `secrets/transmission_user.secret` | `/run/secrets/transmission_user` | Optional. Custom username for Transmission RPC interface. |
| `secrets/transmission_pass.secret` | `/run/secrets/transmission_pass` | Optional. Custom password for Transmission RPC interface. |

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

