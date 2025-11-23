# Build Angular frontend
FROM --platform=$BUILDPLATFORM node:24-alpine AS frontend-build
WORKDIR /app

# Copy package files first for better layer caching
COPY frontend/package*.json ./
# Use cache mount for npm to speed up builds
RUN --mount=type=cache,target=/root/.npm \
    npm ci && npm install -g @angular/cli

# Copy source code
COPY frontend/ .

# Build with appropriate base-href and deploy-url
RUN npm run build

# Build .NET backend
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS build
ARG TARGETARCH
ARG VERSION=0.0.1
ARG PACKAGES_USERNAME
ARG PACKAGES_PAT
WORKDIR /app
EXPOSE 11011

# Copy solution and project files first for better layer caching
# COPY backend/*.sln ./backend/
# COPY backend/*/*.csproj ./backend/*/

# Copy source code
COPY backend/ ./backend/

# Add NuGet source
RUN dotnet nuget add source --username ${PACKAGES_USERNAME} --password ${PACKAGES_PAT} --store-password-in-clear-text --name Cleanuparr https://nuget.pkg.github.com/Cleanuparr/index.json

# Restore and publish with cache mount
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet restore ./backend/Cleanuparr.Api/Cleanuparr.Api.csproj -a $TARGETARCH && \
    dotnet publish ./backend/Cleanuparr.Api/Cleanuparr.Api.csproj \
    -a $TARGETARCH \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:Version=${VERSION} \
    /p:PublishSingleFile=true \
    /p:DebugSymbols=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim

# Install required packages for user management and timezone support
RUN apt-get update && apt-get install -y \
    curl \
    tzdata \
    gosu \
    && rm -rf /var/lib/apt/lists/*

ENV PUID=1000 \
    PGID=1000 \
    UMASK=022 \
    TZ=Etc/UTC \
    HTTP_PORTS=11011

# Fix FileSystemWatcher in Docker: https://github.com/dotnet/dotnet-docker/issues/3546
ENV DOTNET_USE_POLLING_FILE_WATCHER=true

WORKDIR /app

# Copy backend
COPY --from=build /app/publish .
# Copy frontend to wwwroot
COPY --from=frontend-build /app/dist/ui/browser ./wwwroot
# Copy entrypoint script
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

ENTRYPOINT ["/entrypoint.sh"]
CMD ["./Cleanuparr"]