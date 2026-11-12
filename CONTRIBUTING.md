# Contributing to MyTube Public Edition

Thank you for your interest in contributing to MyTube! This guide outlines our architecture principles, development environment setup, and contribution standards.

---

## Architecture & Design Principles

MyTube follows Clean Architecture and CQRS patterns with strict security boundaries:

1. **Simplicity First:** Avoid premature abstractions, speculative patterns, and unnecessary dependencies.
2. **Zero Hardcoded Secrets:** Never hardcode credentials, IP addresses, private domains, or cryptographic keys. All secrets must resolve via `/run/secrets/` or environment variables.
3. **Graceful Degradation:** All external integrations (AI providers, WireGuard VPN, Jellyfin, Prowlarr) are completely optional. The platform must start and remain fully operational if any or all external services are absent.
4. **Surgical Changes:** Make targeted changes that solve the issue at hand. Do not reformat unrelated code.
5. **Goal-Driven Execution:** Verify every change with automated tests before opening a pull request.

---

## Development Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js](https://nodejs.org/) (v20 or later) & npm
- [Docker & Docker Compose](https://www.docker.com/) (optional, for containerized testing)
- [FFmpeg](https://ffmpeg.org/) (for local media processing)

---

## Local Setup

### 1. Backend (`src/MyTube.Backend`)

```bash
cd src/MyTube.Backend

# Restore dependencies
dotnet restore

# Run tests
dotnet test

# Start API server
dotnet run --project MyTube.Backend.Api
```

By default, the backend runs on `http://localhost:5003` with an embedded SQLite database (`mytube.db`).

### 2. Frontend (`src/MyTube.Frontend`)

```bash
cd src/MyTube.Frontend

# Install dependencies
npm install

# Start Vite development server
npm run dev
```

The frontend connects to the backend API via the proxy configured in `vite.config.ts`.

---

## Coding Standards

### Backend (C# / .NET)
- Use standard C# coding conventions and nullable reference types.
- Follow CQRS using MediatR: Commands mutate state; Queries return projections.
- Always propagate `CancellationToken` in asynchronous methods.
- Do not make blocking calls (`.Result`, `.Wait()`) on tasks.
- For SQLite data access, use `Microsoft.EntityFrameworkCore.Sqlite` and keep database transactions short.

### Frontend (React / TypeScript)
- Use TypeScript with strict type checking; avoid `any`.
- Prefer derived state and custom hooks over cascaded `useEffect`.
- Ensure accessible UI elements: icon-only buttons must have `aria-label` attributes.
- Match existing Tailwind CSS utility patterns.

---

## Submitting Pull Requests

1. Create a feature branch: `git checkout -b feature/your-feature-name`.
2. Ensure all backend tests pass: `dotnet test`.
3. Ensure frontend builds cleanly: `npm run build`.
4. Verify no sensitive credentials or personal configurations are committed.
5. Open a Pull Request with a clear title and description explaining the changes and how they were verified.

