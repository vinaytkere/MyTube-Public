# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |
| < 1.0   | :x:                |

---

## Reporting a Vulnerability

We take the security of MyTube seriously. If you believe you have discovered a security vulnerability in MyTube Public Edition, please follow responsible disclosure guidelines.

- **Do NOT** open a public GitHub issue for security vulnerabilities.
- Please email the maintainers with a detailed description of the vulnerability, reproduction steps, and potential impact.
- You will receive an acknowledgment within 48 hours, followed by regular updates until a fix is published.

---

## Security Model & Hardening

MyTube Public Edition is designed to be secure by default for self-hosted environments:

### 1. Secret Protection & Hybrid Configuration
- Sensitive secrets (such as JWT signing keys, database encryption keys, and third-party API tokens) are mounted as Docker secrets under `/run/secrets/`.
- Host-side secret files (`secrets/*.secret`) must be protected with strict OS filesystem permissions (`chmod 600` on Linux/macOS, ACL restriction on Windows).
- Secrets are never committed to version control, embedded in Docker image layers, or logged to application outputs.

### 2. Fail-Closed VPN Killswitch
- When torrent integration is activated, Transmission network traffic is routed entirely through a WireGuard container (Gluetun).
- Gluetun enforces a strict iptables firewall killswitch: if the VPN tunnel drops, all outbound traffic is dropped instantly to prevent IP leakage.

### 3. Server-Side Request Forgery (SSRF) Protection
- All user-supplied URLs for IPTV playlists, video streams, or media downloads are validated before fetching.
- Private, loopback, link-local, and reserved IP ranges (RFC 1918, RFC 3927, RFC 4193, 127.0.0.0/8, 169.254.0.0/16, ::1, fc00::/7) are strictly blocked.
- HTTP redirects are manually validated up to 3 hops; redirecting to an internal IP address will abort the request immediately.
- Response payloads from unverified endpoints have strict size limits (e.g., 5 MB maximum for IPTV manifests) to mitigate decompression bombs and memory exhaustion.

### 4. Non-Root Container Execution
- The backend application runs under an unprivileged user (`mytube`, UID `1000`, GID `1000`).
- Container capabilities are dropped by default where possible.

### 5. Authentication & Token Security
- Password hashing uses ASP.NET Core PasswordHasher (PBKDF2 with HMAC-SHA256, 100,000+ iterations).
- JWT tokens require signing keys with a minimum length of 256 bits (32 bytes). Weak or default tokens are rejected at application startup.
- Role-based authorization is enforced on administrative endpoints.

