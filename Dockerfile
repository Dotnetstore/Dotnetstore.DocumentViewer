# syntax=docker/dockerfile:1.7
# Multi-stage, multi-arch build for the Dotnetstore.DocumentViewer.WebApi image.
# Build:
#   docker buildx build --platform linux/amd64,linux/arm64 -t <tag> --push .
# Local single-arch build:
#   docker build -t dotnetstore/documentviewer-webapi:dev .

# Tags pinned to match global.json so local + CI + container builds all run on the
# same SDK + runtime. The floating "10.0-noble" tag currently jumps to the SDK 10.0.300
# feature band, whose paired ASP.NET Core runtime triggers a FastEndpoints /
# JsonTypeInfoResolverChain NRE on first request. Bump these tags in lockstep with
# global.json when intentionally rolling the SDK forward.
ARG SDK_TAG=10.0.203-noble
ARG RUNTIME_TAG=10.0.8-noble

# ---- build ---------------------------------------------------------------
# Pin the BUILDPLATFORM so the SDK runs natively (avoids QEMU on cross-builds);
# `-a $TARGETARCH` makes the restore + publish produce the target's RID bits.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:${SDK_TAG} AS build
ARG TARGETARCH
ARG BUILD_CONFIG=Release
WORKDIR /src

# Copy shared props + only the project files first so dotnet restore can cache.
COPY Directory.Build.props ./
COPY src/Dotnetstore.DocumentViewer.WebApi/Dotnetstore.DocumentViewer.WebApi.csproj src/Dotnetstore.DocumentViewer.WebApi/
COPY src/Dotnetstore.DocumentViewer.Shared.SDK/Dotnetstore.DocumentViewer.Shared.SDK.csproj src/Dotnetstore.DocumentViewer.Shared.SDK/
COPY src/Dotnetstore.DocumentViewer.Shared.ServiceDefaults/Dotnetstore.DocumentViewer.Shared.ServiceDefaults.csproj src/Dotnetstore.DocumentViewer.Shared.ServiceDefaults/

RUN dotnet restore src/Dotnetstore.DocumentViewer.WebApi/Dotnetstore.DocumentViewer.WebApi.csproj \
    -a "$TARGETARCH"

# Now bring in the rest of the source and publish.
COPY src/ src/

RUN dotnet publish src/Dotnetstore.DocumentViewer.WebApi/Dotnetstore.DocumentViewer.WebApi.csproj \
    -c "$BUILD_CONFIG" \
    -a "$TARGETARCH" \
    --no-restore \
    -o /publish

# ---- runtime --------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${RUNTIME_TAG} AS runtime

# PDFium (via PDFtoImage) needs fontconfig at runtime for any page that touches text.
# curl is used by the HEALTHCHECK directive below.
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
        libfontconfig1 \
        curl \
        ca-certificates \
 && rm -rf /var/lib/apt/lists/*

# The MS ASP.NET image pre-creates a non-root 'app' user (UID 1654). Reuse it
# instead of fabricating our own so the K8s securityContext just has to pin the
# same UID. /data and /app must be writable by that user.
WORKDIR /app
COPY --from=build --chown=app:app /publish ./

# Document + cache storage. Mount a PersistentVolume / named volume here in deployment.
RUN mkdir -p /data/storage /data/cache && chown -R app:app /data
VOLUME ["/data"]

USER app

EXPOSE 8080

# Container-friendly defaults. ALL of these are overridable via env / ConfigMap.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DocumentStorage__RootPath=/data/storage \
    Cache__RootPath=/data/cache

# Liveness probe — /alive is API-key allow-listed by ApiKeyMiddleware so this works
# without credentials. K8s should use its own probes; this is for `docker compose` /
# `docker run` flows that rely on the image-declared healthcheck.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent --show-error http://localhost:8080/alive || exit 1

ENTRYPOINT ["dotnet", "Dotnetstore.DocumentViewer.WebApi.dll"]
