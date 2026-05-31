# Dotnetstore.DocumentViewer

[![CI](https://github.com/Dotnetstore/Dotnetstore.DocumentViewer/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Dotnetstore/Dotnetstore.DocumentViewer/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/github/license/Dotnetstore/Dotnetstore.DocumentViewer)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Avalonia 11](https://img.shields.io/badge/Avalonia-11.3-3FB1FF?logo=avalonia)](https://avaloniaui.net/)
[![FastEndpoints](https://img.shields.io/badge/FastEndpoints-8.1-7B68EE)](https://fast-endpoints.com/)
[![Aspire](https://img.shields.io/badge/Aspire-13.3-512BD4?logo=dotnet)](https://learn.microsoft.com/dotnet/aspire/)
[![Docker](https://img.shields.io/badge/docker-multi--arch-2496ED?logo=docker&logoColor=white)](Dockerfile)
[![Kubernetes](https://img.shields.io/badge/Kubernetes-ready-326CE5?logo=kubernetes&logoColor=white)](deploy/k8s/)

> **A secure, view-only document distribution platform.** Admin uploads PDFs (or DOCX, converted server-side); viewers see the documents as watermarked, page-by-page images in an Avalonia desktop client. The original file bytes never leave the server.

If you've ever needed to share a document with someone who must be able to *read* it but should not be able to easily *save, print, or forward* it — board packs, M&A diligence, exam papers, regulatory submissions, signed contracts — this is that tool.

---

## Highlights

- **Bytes never leave the server.** Every page is rasterised server-side into a PNG and stamped with a per-request watermark (user email, IP, UTC timestamp, copyright). The desktop client never sees the source PDF.
- **Defence-in-depth auth.** ApiKey + JWT bearer + per-jti revocation + refresh-token rotation **with stolen-token detection** + Identity lockout + per-IP rate limiting + per-document ACLs + per-document IP allow-lists.
- **Tamper-evident audit trail.** Every render attempt and every admin state change writes a row to the audit log: success and *every* denial path (bad signature, ACL, IP-blocked, page out of range, document not found, viewer-session denied). Admins drill in per-document from the UI.
- **Three deployment paths, one image.**
  - .NET Aspire AppHost for daily dev (Postgres + pgAdmin + Gotenberg + WebApi + Avalonia, one F5).
  - `docker compose` for prod-like single-host.
  - Kubernetes manifests for clusters; the same image runs in all three.
- **Multi-arch container.** `linux/amd64` + `linux/arm64` from a single `docker buildx build`.
- **Real test coverage.** 78 WebApi integration tests against a real Postgres via Testcontainers + 39 ViewModel tests — every security path has tests.

---

## Quick start

Pick the flow that matches what you're doing right now.

### A. Local dev — .NET Aspire (F5 experience)

```pwsh
# One-time: store an API key for the AppHost to inject into the WebApi + UI
dotnet user-secrets set Parameters:api-key (openssl rand -base64 32) `
  --project src/Dotnetstore.DocumentViewer.Shared.AppHost

dotnet run --project src/Dotnetstore.DocumentViewer.Shared.AppHost
```

The Aspire dashboard URL prints in the terminal. Resources come up in order: `postgres` → `postgres-pgadmin` → `gotenberg` → `webApi` (migrations + admin seed) → `ui` (Avalonia window opens). First boot pulls images (~600 MB Gotenberg).

**Default dev admin:** `admin@dotnetstore.local` / `ChangeMe123!` — `MustChangePassword=true`, so first login forces a change.

### B. Container — `docker compose`

```sh
cp .env.example .env             # then edit secrets in .env
docker compose up -d --build
curl -i http://localhost:8080/alive
```

Brings up `postgres` + `gotenberg` + `webapi`. WebApi waits on Postgres health before booting. Volumes persist between `docker compose down` runs (use `down -v` to wipe).

### C. Kubernetes

```sh
# 1. Push the image
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t ghcr.io/<your-org>/documentviewer-webapi:$(git rev-parse --short HEAD) \
  --push .

# 2. Materialise the Secret from the template and apply
cp deploy/k8s/30-webapi-secret.example.yaml deploy/k8s/webapi-secret.yaml
# edit deploy/k8s/webapi-secret.yaml — gitignored

kubectl apply -f deploy/k8s/00-namespace.yaml
kubectl apply -f deploy/k8s/webapi-secret.yaml
kubectl apply -f deploy/k8s/
```

Full walkthrough + every env-var → appsettings key mapping is in [deploy/README.md](deploy/README.md).

---

## Security features

| Layer | What's enforced | Where |
|---|---|---|
| **Network ingress** | `X-Api-Key` middleware gates every request except `/health` and `/alive`. Constant-time compare against the configured key. | [ApiKeyMiddleware.cs](src/Dotnetstore.DocumentViewer.WebApi/Infrastructure/Security/ApiKeyMiddleware.cs) |
| **Authentication** | JWT bearer with `sub` / `email` / `role` / `mcp` claims. Options-pattern bound so test fixtures and prod config both flow through. | [JwtTokenService.cs](src/Dotnetstore.DocumentViewer.WebApi/Infrastructure/Identity/JwtTokenService.cs), [Program.cs](src/Dotnetstore.DocumentViewer.WebApi/Program.cs) |
| **Refresh-token theft detection** | Reuse of an already-rotated refresh token revokes the *entire* live token family for that user and writes `RefreshToken.Reuse` audit row (RFC 6749 §10.4). | [RefreshTokenEndpoint.cs](src/Dotnetstore.DocumentViewer.WebApi/Features/Auth/RefreshToken/RefreshTokenEndpoint.cs) |
| **Access-token revocation** | `IAccessTokenRevocationStore` keeps a jti blacklist with an `IMemoryCache` front; revocations expire alongside the token. Logout pushes the current jti onto the blacklist. | [AccessTokenRevocationStore.cs](src/Dotnetstore.DocumentViewer.WebApi/Infrastructure/Security/AccessTokenRevocationStore.cs), [LogoutEndpoint.cs](src/Dotnetstore.DocumentViewer.WebApi/Features/Auth/Logout/LogoutEndpoint.cs) |
| **Login timing-attack mitigation** | Missing-user branch still runs `IPasswordHasher.VerifyHashedPassword` against a pre-computed dummy hash so wall-clock cost matches a real check. Stops account enumeration. | [LoginEndpoint.cs](src/Dotnetstore.DocumentViewer.WebApi/Features/Auth/Login/LoginEndpoint.cs) |
| **MustChangePassword enforcement** | `mcp` claim on the JWT; a middleware 403s any request to a non-allow-listed path while the flag is set, so non-Avalonia clients can't ignore the prompt. | [MustChangePasswordGuardMiddleware.cs](src/Dotnetstore.DocumentViewer.WebApi/Infrastructure/Security/MustChangePasswordGuardMiddleware.cs) |
| **Brute-force lockout** | ASP.NET Identity: 5 failed password attempts → 5-min lockout. | [Program.cs](src/Dotnetstore.DocumentViewer.WebApi/Program.cs) |
| **Per-IP rate limiting** | Fixed-window per-IP on `/auth/login` / `/refresh` / `/logout`. Honours `X-Forwarded-For` from trusted networks. | [RateLimitingOptions.cs](src/Dotnetstore.DocumentViewer.WebApi/Infrastructure/Security/RateLimitingOptions.cs) |
| **Per-document ACL** | `IDocumentAccessPolicy.CanViewAsync(userId, isAdmin, documentId)` gates every read path. Admins bypass. | [IDocumentAccessPolicy.cs](src/Dotnetstore.DocumentViewer.WebApi/Infrastructure/Persistence/IDocumentAccessPolicy.cs) |
| **Per-document IP allow-list** | `IDocumentIpPolicy.IsAllowedAsync(documentId, isAdmin, clientIp)`: closed by default for viewers. Render and viewer-session both gate on it. CIDR + single IPs (auto-normalised). | [IDocumentIpPolicy.cs](src/Dotnetstore.DocumentViewer.WebApi/Infrastructure/Security/IDocumentIpPolicy.cs) |
| **Signed page URLs** | `?exp=&sig=` with `HMAC-SHA256(key, userId\|docId\|page\|expUnix)`. `userId` is in the HMAC input but **not** the URL, so a captured URL replayed under a different user fails the signature recomputation. Constant-time byte compare. | [SignedUrlService.cs](src/Dotnetstore.DocumentViewer.WebApi/Infrastructure/Security/SignedUrlService.cs) |
| **Per-page watermark** | `{email} - {ip} - {UTC timestamp} ©  Dotnetstore` overlaid on every served PNG. Survives screenshots. | [RenderPageEndpoint.cs](src/Dotnetstore.DocumentViewer.WebApi/Features/Documents/RenderPage/RenderPageEndpoint.cs) |
| **Upload validation** | Magic-byte sniff (`%PDF-` / `PK\x03\x04`). Renamed payloads are rejected — the content-type header / filename extension are advisory only. | [UploadDocumentEndpoint.cs](src/Dotnetstore.DocumentViewer.WebApi/Features/Documents/Upload/UploadDocumentEndpoint.cs) |
| **Storage path traversal** | `Path.GetRelativePath`-based guard rejects `..` segments and rooted absolute paths. | [FileSystemDocumentStorage.cs](src/Dotnetstore.DocumentViewer.WebApi/Infrastructure/Storage/FileSystemDocumentStorage.cs) |
| **ForwardedHeaders** | Trusts `X-Forwarded-For` / `-Proto` / `-Host` only from configured `KnownNetworks` / `KnownProxies`. Loopback trusted by default (Aspire dev). | [Program.cs](src/Dotnetstore.DocumentViewer.WebApi/Program.cs) |
| **Audit log** | Every state-changing admin action + every render attempt (success and every failure mode) writes an `AccessAuditLog` row with user, IP, action, status, document, page, timestamp. LEFT-joined with users on read so the UI shows emails, not Guids. | [IAuditLogger.cs](src/Dotnetstore.DocumentViewer.WebApi/Infrastructure/Auditing/IAuditLogger.cs), [AuditActions.cs](src/Dotnetstore.DocumentViewer.WebApi/Infrastructure/Identity/AuditActions.cs) |

### Desktop-client hardening (best-effort)

The Avalonia client makes opportunistic attempts to discourage casual copying:

- `KeyDown` tunnel handler swallows `Ctrl+C / S / P / X / A` and `PrintScreen` inside the viewer.
- Pages render via `<Image>` controls with `Cache-Control: no-store` on the response.
- Top bar always shows the logged-in user's email and the server-known IP, so the viewer can see exactly which identity is in scope.

### What's *not* enforceable

State this honestly:

- **OS screen capture, phone cameras, screencasting.** Can't be prevented from inside a userland app. The watermark is the real mitigation — it makes leaks attributable, not impossible.
- **Debugger / memory dumps of the client.** The Avalonia process holds rendered bitmaps in RAM; a determined user with the right tooling can extract them.
- **Replay of a still-valid signed URL by the same user.** The URL is bound to `(userId, docId, page, exp)` with a ~60-second window; replay by *another* user fails the HMAC, but replay by the same user within the window does succeed. Add a server-side nonce table if you need strict single-use.

---

## Feature tour

### For administrators

- **User management** — create / update / delete viewers and other admins, with per-user role assignment and one-click password reset (target user is force-prompted to change on next login).
- **Document upload** — drag-and-drop PDF or DOCX; DOCX is queued for server-side conversion via Gotenberg, with status surfaced as `Converting → Ready` or `Failed`.
- **Per-document access grants** — explicit ACL: admin picks which viewers can see which documents. Grants are idempotent and revoked one-click.
- **Per-document IP allow-list** — CIDR or single IP, with optional description. Closed by default: viewers without a matching entry get a clean 403 + audit row. Admins always bypass.
- **Per-document audit log** — drill into any document and see every access attempt: who tried, from what IP, when, and which gate (if any) blocked them. Failures highlighted in red.

### For viewers

- Log in, see the documents you've been granted access to.
- Open a document → pages render one by one with the watermark.
- That's it. There's deliberately no save, copy, print, or download in the UI.

### For developers integrating with the API

- A typed SDK ([Dotnetstore.DocumentViewer.Shared.SDK](src/Dotnetstore.DocumentViewer.Shared.SDK/)) ships the DTOs + `IDocumentViewerApiClient` with `X-Api-Key` and `Bearer` `DelegatingHandler`s pre-wired. Register with `services.AddDocumentViewerSdk(configuration)`.
- Swagger UI at `/swagger` in Development.

---

## Architecture

```
            ┌──────────────────────────┐
            │  Avalonia desktop client │
            │  (CommunityToolkit.Mvvm) │
            └────────────┬─────────────┘
                         │  HTTPS + X-Api-Key + Bearer JWT
                         │  (via IDocumentViewerApiClient)
                         ▼
┌──────────────────────────────────────────────────┐
│  ASP.NET Core WebApi (FastEndpoints)             │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐        │
│  │ Auth     │  │ Docs     │  │ Audit    │  ...   │
│  └─────┬────┘  └─────┬────┘  └────┬─────┘        │
│        │ ┌─ ApiKey   │ ForwardedH │                │
│        │ ├─ RateLimit│ AuthN/AuthZ│                │
│        │ ├─ MCP guard│ Audit      │                │
└────────┼─┼───────────┼────────────┼────────────────┘
         │ │           │            │
         │ │           │            │
   ┌─────▼─▼┐   ┌──────▼───┐   ┌────▼────┐
   │Postgres│   │ FS / PVC │   │Gotenberg│
   │ (EF +  │   │  /data   │   │  DOCX→  │
   │ Identity│  │ storage  │   │  PDF    │
   │  + audit│  │  + cache │   │         │
   └────────┘   └──────────┘   └─────────┘
```

### Project layout

```
src/
  Dotnetstore.DocumentViewer.WebApi                 -- FastEndpoints + EF Core + Identity + JWT + rendering
  Dotnetstore.DocumentViewer.Shared.AppHost         -- Aspire orchestrator (dev only)
  Dotnetstore.DocumentViewer.Shared.ServiceDefaults -- OTel / health / resilience (Aspire defaults)
  Dotnetstore.DocumentViewer.Shared.SDK             -- Wire DTOs + typed HttpClient
  Dotnetstore.DocumentViewer.UI.AvalonUi            -- Desktop client (Avalonia + CommunityToolkit.Mvvm + Semi.Avalonia)
tests/
  Dotnetstore.DocumentViewer.WebApi.Tests           -- xUnit v3 + WebApplicationFactory + Testcontainers.PostgreSQL
  Dotnetstore.DocumentViewer.Shared.AppHost.Tests   -- DistributedApplicationTestingBuilder smoke
  Dotnetstore.DocumentViewer.Shared.SDK.Tests       -- DTO + DelegatingHandler tests
  Dotnetstore.DocumentViewer.UI.AvalonUi.Tests      -- ViewModel tests
deploy/
  k8s/                                              -- Kubernetes manifests (numbered apply order)
  README.md                                         -- Per-flow walkthrough + config reference
.github/workflows/ci.yml                            -- CI: build, test, image smoke
Dockerfile                                          -- Multi-stage, multi-arch (amd64 + arm64)
docker-compose.yml                                  -- Local prod-like stack
.env.example                                        -- Compose secrets template
```

---

## Stack at a glance

| Layer | Choice |
|---|---|
| Language / runtime | C# / .NET 10 |
| Web framework | FastEndpoints 8.1 (vertical slices) |
| Persistence | EF Core 10 + Npgsql + Aspire enrichment |
| Identity | ASP.NET Core Identity + JWT bearer |
| Rasterization | PDFtoImage (PDFium) + SkiaSharp watermarking |
| DOCX → PDF | Gotenberg (default, sidecar container) or local `soffice` |
| Desktop UI | Avalonia 11 + CommunityToolkit.Mvvm + Semi.Avalonia |
| Dev orchestration | .NET Aspire AppHost |
| Container | Multi-stage Dockerfile on `mcr.microsoft.com/dotnet/aspnet:10.0-noble`, multi-arch |
| Tests | xUnit v3 + Testcontainers.PostgreSql + NSubstitute + Shouldly |
| CI | GitHub Actions ([.github/workflows/ci.yml](.github/workflows/ci.yml)) |

---

## Configuration reference

Every option below maps the `appsettings.json` key to its env-var form (`__` = key nesting). Secrets — JWT signing key, signed-URL key, API key, admin seed password — should come from user-secrets (dev), `.env` (compose), or a Secret manifest (K8s). Never commit them.

| appsettings key | Env var | Notes |
|---|---|---|
| `ConnectionStrings:documentviewer` | `ConnectionStrings__documentviewer` | Npgsql connection string |
| `Jwt:Issuer` / `Jwt:Audience` | `Jwt__Issuer` / `Jwt__Audience` | Identity claims |
| `Jwt:SigningKey` | `Jwt__SigningKey` | **Secret**, ≥ 32 chars |
| `Jwt:AccessTokenLifetime` | `Jwt__AccessTokenLifetime` | Default `00:15:00` |
| `Jwt:RefreshTokenLifetime` | `Jwt__RefreshTokenLifetime` | Default `14.00:00:00` |
| `ApiKey:Value` | `ApiKey__Value` | **Secret**; shared with the client build |
| `SignedUrl:SigningKey` | `SignedUrl__SigningKey` | **Secret**, ≥ 32 chars |
| `SignedUrl:Lifetime` | `SignedUrl__Lifetime` | Default `00:01:00` |
| `Seed:Admin:Email` / `DisplayName` | `Seed__Admin__Email` / `DisplayName` | Seeded on first boot if absent |
| `Seed:Admin:Password` | `Seed__Admin__Password` | **Secret**; MustChangePassword=true |
| `DocumentStorage:RootPath` | `DocumentStorage__RootPath` | `/data/storage` in containers |
| `DocumentStorage:MaxBytes` | `DocumentStorage__MaxBytes` | Per-upload size cap |
| `Cache:RootPath` / `MaxBytes` / `MaxAge` / `EvictionInterval` | `Cache__*` | Page-image disk cache |
| `DocumentConversion:Mode` | `DocumentConversion__Mode` | `Gotenberg` or `Soffice` |
| `DocumentConversion:Gotenberg:BaseAddress` | `DocumentConversion__Gotenberg__BaseAddress` | `http://gotenberg:3000` in-cluster |
| `RateLimiting:Auth:PermitLimit` / `Window` | `RateLimiting__Auth__*` | Defaults: 10 / 1 min |
| `ForwardedHeaders:Enabled` | `ForwardedHeaders__Enabled` | |
| `ForwardedHeaders:KnownNetworks[N]` | `ForwardedHeaders__KnownNetworks__N` | Trust these CIDRs for `X-Forwarded-For` |
| `ForwardedHeaders:KnownProxies[N]` | `ForwardedHeaders__KnownProxies__N` | Specific proxy IPs |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | (env var) | Optional OTLP exporter target |

---

## Running the tests

```sh
dotnet test
```

The WebApi integration tests use **Testcontainers.PostgreSql**, which needs Docker available on the host. The UI ViewModel tests have no infra dependencies and run in milliseconds.

CI runs both on every PR to `main` ([.github/workflows/ci.yml](.github/workflows/ci.yml)), plus a Docker image smoke build.

---

## Contributing

Bug reports and PRs welcome. Before opening a PR:

1. `dotnet build` is clean (Debug treats every warning as error per [Directory.Build.props](Directory.Build.props)).
2. `dotnet test` is green (Docker required for the WebApi integration tests).
3. New endpoints have at least one integration test under [tests/Dotnetstore.DocumentViewer.WebApi.Tests/](tests/Dotnetstore.DocumentViewer.WebApi.Tests/).

---

## License

[MIT](LICENSE) © 2026 Hans Sjödin / Dotnetstore.
