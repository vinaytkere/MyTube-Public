# syntax=docker/dockerfile:1

# ==========================================
# Stage 1: Static FFmpeg & FFprobe
# ==========================================
FROM mwader/static-ffmpeg:7.1 AS ffmpeg-source

# ==========================================
# Stage 2: Frontend Build (Node.js)
# ==========================================
FROM node:22-alpine AS frontend-build
WORKDIR /frontend

COPY src/MyTube.Frontend/package*.json ./
RUN npm ci --prefer-offline

COPY src/MyTube.Frontend/ ./
RUN npm run build

# ==========================================
# Stage 3: Runtime Base Dependencies
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime-base
ARG TARGETARCH
WORKDIR /app

# Copy static ffmpeg and ffprobe binaries
COPY --from=ffmpeg-source /ffmpeg /usr/local/bin/
COPY --from=ffmpeg-source /ffprobe /usr/local/bin/

# Install runtime dependencies (python3 for yt-dlp, nodejs, ca-certificates, curl, unzip, aria2)
RUN apt-get update && apt-get install -y --no-install-recommends \
    python3 \
    nodejs \
    ca-certificates \
    curl \
    unzip \
    aria2 \
    sqlite3 \
    && rm -rf /var/lib/apt/lists/*

# Install architecture-specific Deno binary (amd64 / arm64) for yt-dlp modern EJS challenges
RUN if [ "$TARGETARCH" = "arm64" ]; then \
        DENO_ARCH="aarch64"; \
    else \
        DENO_ARCH="x86_64"; \
    fi && \
    curl -fsSL "https://github.com/denoland/deno/releases/latest/download/deno-${DENO_ARCH}-unknown-linux-gnu.zip" -o /tmp/deno.zip && \
    unzip -q /tmp/deno.zip -d /usr/local/bin && \
    rm /tmp/deno.zip && \
    chmod a+rx /usr/local/bin/deno

# Download latest yt-dlp binary and verify executability
RUN curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -o /usr/local/bin/yt-dlp \
    && chmod a+rx /usr/local/bin/yt-dlp /usr/local/bin/ffmpeg /usr/local/bin/ffprobe

# Create non-root application user and persistent data directories
RUN groupadd -g 1000 mytube && \
    useradd -u 1000 -g mytube -m -s /bin/bash mytube && \
    mkdir -p /app/data /app/data/keys /app/media /app/cache /run/secrets && \
    chown -R mytube:mytube /app

# ==========================================
# Stage 4: Backend Build (.NET 10 SDK)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project definition and props first for caching
COPY ["MyTube.slnx", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["src/MyTube.Backend/MyTube.Backend.Domain/MyTube.Backend.Domain.csproj", "src/MyTube.Backend/MyTube.Backend.Domain/"]
COPY ["src/MyTube.Backend/MyTube.Backend.Application/MyTube.Backend.Application.csproj", "src/MyTube.Backend/MyTube.Backend.Application/"]
COPY ["src/MyTube.Backend/MyTube.Backend.Infrastructure/MyTube.Backend.Infrastructure.csproj", "src/MyTube.Backend/MyTube.Backend.Infrastructure/"]
COPY ["src/MyTube.Backend/MyTube.Backend.Api/MyTube.Backend.Api.csproj", "src/MyTube.Backend/MyTube.Backend.Api/"]

RUN dotnet restore "src/MyTube.Backend/MyTube.Backend.Api/MyTube.Backend.Api.csproj"

# Copy source and publish
COPY src/MyTube.Backend/ src/MyTube.Backend/
RUN dotnet publish "src/MyTube.Backend/MyTube.Backend.Api/MyTube.Backend.Api.csproj" \
    -c Release -o /app/publish --no-restore -p:UseSharedCompilation=true -p:BuildInParallel=true

# Copy compiled frontend assets into wwwroot of backend
COPY --from=frontend-build /frontend/dist /app/publish/wwwroot

# ==========================================
# Stage 5: Final Production Runtime Image
# ==========================================
FROM runtime-base AS final
WORKDIR /app

COPY --from=build /app/publish .

# Run as non-root user
USER mytube

ENV PORT=5003
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/mytube.db;Cache=Shared"
ENV DATA_DIR="/app/data"
ENV Media__RootPath="/app/media"
ENV Media__CachePath="/app/cache"
ENV SECRETS_PATH="/run/secrets"

EXPOSE 5003

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:${PORT}/health || exit 1

ENTRYPOINT ["sh", "-c", "dotnet MyTube.Backend.Api.dll --urls http://0.0.0.0:${PORT}"]
