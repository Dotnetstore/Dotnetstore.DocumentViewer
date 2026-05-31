# Deployment

Two container-based paths ship in this repo:

| Path | Use it for | Entry point |
|---|---|---|
| `docker compose` | local prod-like smoke tests, single-host deployments | [/docker-compose.yml](../docker-compose.yml) |
| Kubernetes manifests | clusters | [`k8s/`](k8s/) |

The Aspire AppHost ([src/Dotnetstore.DocumentViewer.Shared.AppHost](../src/Dotnetstore.DocumentViewer.Shared.AppHost/)) stays the dev path — both container paths are deliberately separate so day-to-day F5 still benefits from the Aspire dashboard.

---

## docker compose

1. **Set the secrets.** Copy `.env.example` to `.env` and replace every value:

   ```bash
   cp .env.example .env
   # then edit .env — JWT_SIGNING_KEY / SIGNED_URL_SIGNING_KEY want ≥ 32 chars,
   # e.g. `openssl rand -base64 48`
   ```

2. **Build and run.**

   ```bash
   docker compose up -d --build
   ```

   First boot:
   - `postgres` comes up, healthcheck passes
   - `gotenberg` comes up
   - `webapi` builds from the local Dockerfile, applies migrations, seeds the admin user, listens on `http://localhost:8080`

3. **Smoke test.**

   ```bash
   curl -i http://localhost:8080/alive       # 200 OK
   curl -i http://localhost:8080/auth/login \
        -H "X-Api-Key: $API_KEY" \
        -H "Content-Type: application/json" \
        -d '{"email":"admin@dotnetstore.local","password":"ChangeMeOnFirstLogin123!"}'
   ```

4. **Stop / wipe.**

   ```bash
   docker compose down              # stop, keep volumes
   docker compose down -v           # stop + drop postgres + storage volumes
   ```

The WebApi image is built locally as `dotnetstore/documentviewer-webapi:local`. To push to a registry instead, replace the `build:` block with `image: <your-registry>/dotnetstore/documentviewer-webapi:<tag>`.

---

## Kubernetes

Manifests under [`k8s/`](k8s/) are numbered so a plain `kubectl apply -f k8s/` works (alphabetical order = dependency order: namespace → postgres → gotenberg → secret → configmap → pvc → deployment → service).

### One-time setup

1. **Build and push a multi-arch image.**

   ```bash
   docker buildx build \
     --platform linux/amd64,linux/arm64 \
     -t ghcr.io/<your-org>/documentviewer-webapi:$(git rev-parse --short HEAD) \
     -t ghcr.io/<your-org>/documentviewer-webapi:latest \
     --push .
   ```

   Then update `image:` in [`k8s/33-webapi-deployment.yaml`](k8s/33-webapi-deployment.yaml) to that tag.

2. **Materialise the Secret.** Copy the example and replace every value:

   ```bash
   cp deploy/k8s/30-webapi-secret.example.yaml deploy/k8s/webapi-secret.yaml
   # edit deploy/k8s/webapi-secret.yaml — webapi-secret.yaml is gitignored
   ```

   For real clusters, prefer sealing the Secret through Sealed Secrets, External Secrets Operator, or your cloud KMS — never commit plaintext.

3. **Apply.**

   ```bash
   kubectl apply -f deploy/k8s/00-namespace.yaml
   kubectl apply -f deploy/k8s/webapi-secret.yaml    # your unsealed copy
   kubectl apply -f deploy/k8s/
   ```

### What's in each file

| File | Resource |
|---|---|
| [`00-namespace.yaml`](k8s/00-namespace.yaml) | `Namespace documentviewer` |
| [`10-postgres.yaml`](k8s/10-postgres.yaml) | Postgres PVC + Service + Deployment (evaluation-grade; swap for managed Postgres in prod) |
| [`20-gotenberg.yaml`](k8s/20-gotenberg.yaml) | Gotenberg Service + Deployment |
| [`30-webapi-secret.example.yaml`](k8s/30-webapi-secret.example.yaml) | Template for the `webapi-secrets` Secret |
| [`31-webapi-configmap.yaml`](k8s/31-webapi-configmap.yaml) | `webapi-config` ConfigMap (issuer / audience / forwarded-headers / gotenberg URL) |
| [`32-webapi-pvc.yaml`](k8s/32-webapi-pvc.yaml) | `webapi-data` PVC for `/data/{storage,cache}` |
| [`33-webapi-deployment.yaml`](k8s/33-webapi-deployment.yaml) | WebApi Deployment with probes, non-root securityContext, env from ConfigMap + Secret |
| [`34-webapi-service.yaml`](k8s/34-webapi-service.yaml) | ClusterIP Service on port 80 → container port 8080 |

### Exposing the API

The Service is `ClusterIP` only — front it with an Ingress (nginx-ingress, traefik, …) that terminates TLS and sets `X-Forwarded-For` / `-Proto` / `-Host`. The WebApi's ForwardedHeaders middleware honours those headers from the cluster pod CIDR by default (see `ForwardedHeaders__KnownNetworks__*` in the ConfigMap).

### Verifying

```bash
kubectl -n documentviewer get pods
kubectl -n documentviewer logs deploy/webapi
kubectl -n documentviewer port-forward svc/webapi 8080:80
curl -i http://localhost:8080/alive
```

---

## Configuration reference

Every option below maps the appsettings.json key onto the env var (`__` = key nesting). The Secret holds the signing keys + admin seed password; everything else is fine to publish via ConfigMap.

| Env var | appsettings key | Source | Notes |
|---|---|---|---|
| `ConnectionStrings__documentviewer` | `ConnectionStrings:documentviewer` | composed in Deployment from Secret | Standard Npgsql connection string |
| `Jwt__Issuer` | `Jwt:Issuer` | ConfigMap | |
| `Jwt__Audience` | `Jwt:Audience` | ConfigMap | |
| `Jwt__SigningKey` | `Jwt:SigningKey` | **Secret** | ≥ 32 chars |
| `ApiKey__Value` | `ApiKey:Value` | **Secret** | Shared with the Avalonia client build |
| `SignedUrl__SigningKey` | `SignedUrl:SigningKey` | **Secret** | ≥ 32 chars |
| `Seed__Admin__Email` | `Seed:Admin:Email` | ConfigMap | |
| `Seed__Admin__Password` | `Seed:Admin:Password` | **Secret** | First login forces a change |
| `DocumentConversion__Mode` | `DocumentConversion:Mode` | ConfigMap | `Gotenberg` or `Soffice` |
| `DocumentConversion__Gotenberg__BaseAddress` | `DocumentConversion:Gotenberg:BaseAddress` | ConfigMap | `http://gotenberg:3000` in-cluster |
| `DocumentStorage__RootPath` | `DocumentStorage:RootPath` | image default | `/data/storage` — overridable |
| `Cache__RootPath` | `Cache:RootPath` | image default | `/data/cache` — overridable |
| `ForwardedHeaders__Enabled` | `ForwardedHeaders:Enabled` | ConfigMap | |
| `ForwardedHeaders__KnownNetworks__N` | `ForwardedHeaders:KnownNetworks[N]` | ConfigMap | Trust your pod CIDR / proxy network |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | n/a (consumed by ServiceDefaults) | optional ConfigMap | OTLP collector endpoint |
