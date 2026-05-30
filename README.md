# Dotnetstore.DocumentViewer

A secure document viewer: an admin uploads PDFs (or DOCX, converted server-side), grants per-user access, and viewers see the documents rendered as watermarked page images via an Avalonia desktop client. The original file bytes never leave the server.

## Stack

- **.NET 10** across the board (`net10.0`).
- **WebApi** — FastEndpoints vertical slices, EF Core + Npgsql, ASP.NET Identity + JWT bearer, PDFtoImage + SkiaSharp for server-side rasterization + watermarking, LibreOffice (`soffice`) for DOCX → PDF conversion.
- **Aspire AppHost** — orchestrates a Postgres container (with pgAdmin), the WebApi, and the Avalonia UI; injects an `api-key` parameter into both.
- **Avalonia 11 + CommunityToolkit.Mvvm** for the desktop viewer.
- **Shared SDK** — wire DTOs + typed `HttpClient` (`IDocumentViewerApiClient`) with `X-Api-Key` and `Bearer` DelegatingHandlers, consumable by any .NET client.
- **Tests** — xUnit v3 + `WebApplicationFactory` + Testcontainers.PostgreSQL on the WebApi side; 30+ integration tests cover auth, users, documents, access, signed-URL/render auth matrix, audit log, password reset, and DOCX upload.

## Security posture (read this before deploying)

This product makes a **practical** "view-only" guarantee, not an unbreakable one. Here's what's enforced where, and what fundamentally can't be:

### What the server enforces
- **X-Api-Key gate** on every endpoint except `/health` and `/alive`. The key identifies the *client build*; without it requests are rejected before authentication runs.
- **JWT bearer** on all protected endpoints. Configured via the options pattern so test overrides flow through; tokens carry `sub`, `email`, and `role` claims. Refresh tokens are rotated on use (the prior one is revoked).
- **Per-IP rate limiting** on `/auth/login` and `/auth/refresh` (default 10 / 1 min, configurable via `RateLimiting:Auth:*`). Honours `X-Forwarded-For` so deployments behind a trusted reverse proxy throttle by the real client IP.
- **Identity lockout** — 5 failed password attempts locks the account for 5 minutes.
- **Per-document ACL.** `Roles(Admin)` plus an explicit `DocumentAccess` row gate every read path. Admins implicitly bypass the ACL.
- **Signed page URLs** for the renderer: `GET /documents/{id}/pages/{n}?exp=&sig=` where `sig = HMAC-SHA256(secret, userId|docId|page|expUnix)`. The userId is in the HMAC input but *not* the URL — the endpoint reads the JWT `sub` and recomputes; a captured URL replayed under a different user fails the signature check without a separate equality check.
- **Server-side rasterization with per-user watermark.** The original PDF bytes never reach the client. Every rendered page bears `{email}  -  {ip}  -  {UTC timestamp}` so leaks are attributable.
- **Audit log** on every render attempt (success and every failure mode: bad signature, expired, forbidden, out-of-range, not-found) plus access grants. Admin can query via `GET /audit-log` with filters.
- **Path-traversal guard** in the file-system storage layer — the absolute resolved path must remain under `DocumentStorage:RootPath`.
- **Constant-time API-key compare** to prevent length-leak side channels.

### What the desktop viewer does (best-effort)
- Renders pages as `Image` controls with `Cache-Control: no-store` from the server.
- Tunneled `KeyDown` handler swallows `Ctrl+C / S / P / X / A` and `PrintScreen` inside the viewer.
- Context menus are nulled on page images; drag-drop is not wired.
- The seeded admin has `MustChangePassword=true`; logging in routes to the change-password screen before the documents list, and Cancel is disabled there.

### What's *not* enforceable (state this honestly)
- **OS-level screen capture, screenshots, and photographing the screen** can't be prevented from inside the app. The watermark is the real mitigation — it makes leaks attributable, not impossible.
- **DLL injection, debugging the client process, memory dumps.** The Avalonia app holds rendered bitmaps in memory; any user with the right tooling can extract them.
- **The signed URL is captured *during* its 60-second window by an attacker with the same JWT** (i.e. the same user) still works. The mitigation is the short expiry + per-page binding, not single-use; if you need single-use, add a server-side `(userId, docId, page, exp)` nonce table.

### Operational notes
- **Set strong secrets via user-secrets, not committed config.**
  - AppHost parameter: `dotnet user-secrets set Parameters:api-key <random> --project src/Dotnetstore.DocumentViewer.Shared.AppHost`
  - WebApi (if running standalone): `Jwt:SigningKey`, `ApiKey:Value`, `SignedUrl:SigningKey`, `Seed:Admin:Password` — `dotnet user-secrets set <key> <value> --project src/Dotnetstore.DocumentViewer.WebApi`
- **LibreOffice required for DOCX conversion.** Without it, DOCX uploads stay in `Status=Converting` until the worker tries them and flips them to `Failed`. Install `libreoffice-core libreoffice-writer` on the WebApi host (or override `DocumentConversion:SofficePath`).
- **HTTPS termination matters for rate limiting.** Behind a reverse proxy, configure forwarded-headers so `X-Forwarded-For` reflects the real client IP and rate-limit partitions don't all collapse to the proxy address.

## Running locally

```powershell
# One-time: store the AppHost api-key
dotnet user-secrets set Parameters:api-key <random> `
  --project src/Dotnetstore.DocumentViewer.Shared.AppHost

# Run the whole stack
dotnet run --project src/Dotnetstore.DocumentViewer.Shared.AppHost
```

The terminal prints a login URL for the Aspire dashboard. Resources come up in this order: `postgres` → `postgres-pgadmin` → `webApi` (migrations + admin seed run on startup) → `ui` (Avalonia window pops automatically).

Default dev seed admin: `admin@dotnetstore.local` / `ChangeMe123!` — with `MustChangePassword=true`, so the first login forces a change.

## Tests

```powershell
dotnet test
```

The WebApi integration tests need Docker (Testcontainers spins up Postgres per fixture). The AppHost smoke test needs Docker too (boots the distributed app and pings `/alive`).
