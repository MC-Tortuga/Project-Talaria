# Project-Talaria

Secure one-time bank statement delivery service with decoupled architecture, focusing on Security-by-Design for sensitive data exfiltration prevention and verified delivery.

## Architecture Overview

Two-API design with Clean Architecture vertical slices:

```
┌──────────┐   JWT Auth    ┌─────────────┐   One-Time   ┌──────────┐
│  Client   │ ──────────▶  │ ControlPlane │ ──────────▶  │ DataPlane│
│ (Browser) │ ◀──────────  │   (:5000)    │ ◀──────────  │ (:5001)  │
└──────────┘              └──────┬───────┘              └─────┬────┘
                                 │                            │
                          ┌──────▼───────┐             ┌──────▼──────┐
                          │  SQLite/SQL   │             │  S3 / Local  │
                          │   Server      │             │  File System │
                          └──────────────┘             └─────────────┘
```

- **Control Plane (Port 5000)**: Manages identity, authorization, metadata retrieval, and generation of secure one-time access tokens.
- **Data Plane / Streamer (Port 5001)**: Handles high-performance streaming of encrypted documents directly from storage (S3) after verifying one-time tokens.

## Tech Stack

- **Framework**: .NET 10.0 with Clean Architecture
- **Database**: SQL Server (Production) / SQLite (Local Development)
- **Storage**: AWS S3 with `IStorageProvider` abstraction (local filesystem fallback)
- **Security & Encryption**:
  - AWS KMS: Envelope encryption for document protection
  - AWS Secrets Manager: Secure storage for CDN signing keys
  - SHA-256 Hashing: Secure API key persistence
- **Infrastructure**: Docker Compose (local) / Kubernetes manifests (production)
- **Observability**: Structured JSON logging via Serilog

## Key Security Features

- **One-Time "Burn" Tokens**: Access tokens for document streaming are single-use. The system invalidates the token immediately upon successful download to prevent link sharing or replay attacks.
- **Zero-Knowledge Key Storage**: Plaintext API keys are never stored — only SHA-256 hashes are persisted in the database.
- **Envelope Encryption**: Documents stored in S3 use unique data keys. These keys are encrypted via AWS KMS and must be decrypted at runtime to stream the file.
- **Granular RBAC**: Permission system controlling access at the resource and action level (e.g., `statements:read`, `apikeys:write`). Roles: Admin, User, Auditor.
- **Audit Logging**: Every critical action (token generation, usage, security denials) is logged with user ID, IP address, and timestamp.
- **Rate Limiting**: Configurable fixed-window limiter (default 100 req/min per IP).

## Quick Start

### Prerequisites

- Docker and Docker Compose
- .NET 10 SDK (for local development without Docker)

### Setup

```bash
# Start services
docker compose up -d

# Initialize database and seed data
./scripts/init.sh
```

### Run the Demos

```bash
# Full feature demo
./demo.sh

# Security-focused demo (burn-on-use, API key hashing)
./demo_security.sh
```

### Manual API Walkthrough

```bash
# 1. Get a JWT (dev endpoint)
curl "http://localhost:5000/dev/token?account_id=ACC001&roles=Admin"

# 2. List statements
curl -H "Authorization: Bearer <token>" http://localhost:5000/api/statements

# 3. Get one-time download token
curl -H "Authorization: Bearer <token>" \
  "http://localhost:5000/api/statements/<guid>/download"

# 4. Stream the file (token burned after first use)
curl -H "Authorization: Bearer <one-time-token>" \
  "http://localhost:5001/stream/<guid>"
```

## API Endpoints

### ControlPlane (`:5000`)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/health` | No | Health check |
| GET | `/health/live` | No | Liveness probe |
| GET | `/health/ready` | No | Readiness probe |
| GET | `/dev/token` | No* | Dev JWT generator |
| GET | `/api/statements` | JWT | List statements for account |
| GET | `/api/statements/{id}/download` | JWT | Get one-time download token |
| GET | `/api/apikeys` | JWT | List API keys |
| POST | `/api/apikeys` | JWT | Create API key (plain key returned once) |
| DELETE | `/api/apikeys/{id}` | JWT | Revoke API key |

*\* Gated by `Dev__EnableTestTokenEndpoint=true` AND `Development` environment*

### DataPlane (`:5001`)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/health/live` | No | Liveness probe |
| GET | `/health/ready` | No | Readiness probe |
| GET | `/stream/{id}` | One-time token | Download statement (burn-on-use) |

## Configuration Reference

| Variable | Default | Description |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` | Runtime environment |
| `ConnectionStrings__TalariaDb` | — | SQLite (`*.db`) or SQL Server connection string |
| `Storage__UseLocal` | `true` | `true`=local files, `false`=AWS S3 |
| `Storage__LocalPath` | `statements/` | Local storage directory |
| `Jwt__DevSecret` | *(required)* | HMAC-SHA256 JWT signing key |
| `Dev__EnableTestTokenEndpoint` | `true` | Enable `/dev/token` (development only) |
| `Streamer__BaseUrl` | `http://localhost:5001` | DataPlane URL for token generation |
| `RateLimiting:Fixed:PermitLimit` | `100` | Max requests per rate limit window |
| `RateLimiting:Fixed:WindowMinutes` | `1` | Rate limit window duration in minutes |
| `RateLimiting:Fixed:QueueLimit` | `2` | Max queued requests when limit exceeded |
| `AWS__Region` | `us-east-1` | AWS region |
| `AWS__BucketName` | `talaria-statements` | S3 bucket |

## Production Deployment (Kubernetes)

```
┌──────────────────────────────────────────────┐
│              Ingress (TLS termination)         │
│      ┌──────────────┐   ┌──────────────┐      │
│      │ ControlPlane  │   │  DataPlane   │      │
│      │  (2 replicas) │   │ (2 replicas) │      │
│      └──────┬───────┘   └──────┬───────┘      │
│             │                   │              │
│      ┌──────▼───────┐   ┌──────▼───────┐      │
│      │  SQL Server   │   │   S3 Bucket  │      │
│      │  (External)   │   │  (AWS KMS)   │      │
│      └──────────────┘   └──────────────┘      │
└──────────────────────────────────────────────┘
```

```bash
# 1. Create secrets (edit secret-template.yml first)
kubectl apply -f k8s/secret-template.yml

# 2. Run database migration
kubectl apply -f k8s/job-migrate.yml

# 3. Deploy services
kubectl apply -f k8s/deployment.yml
```

### Production Checklist

- **TLS**: Terminate at ingress/load balancer — APIs use HTTP internally
- **Secrets**: Use Kubernetes Secrets, HashiCorp Vault, or AWS Secrets Manager — never ConfigMaps
- **Storage**: Set `Storage__UseLocal=false` with S3 bucket + KMS key
- **Migrations**: InitContainer or pre-deployment Job for EF Core migrations
- **Token Endpoint**: `Dev__EnableTestTokenEndpoint` must be `false` (default in `appsettings.Production.json`)
- **Rate Limits**: Tune `RateLimiting:Fixed` values based on load testing

## Project Structure

```
src/
├── ProjectTalaria.Domain/              # Entities, interfaces (IStorageProvider, etc.)
├── ProjectTalaria.Application/         # DTOs, service contracts
├── ProjectTalaria.Infrastructure/      # Data (EF Core), Storage (S3/Local), Security (KMS), CDN
├── ProjectTalaria.ControlPlane.Api/    # Auth, metadata, token generation, API keys
└── ProjectTalaria.DataPlane.Streamer/  # File streaming, token verification, burn-on-use
k8s/
├── deployment.yml                      # Deployments + Services + Secret
├── secret-template.yml                 # Secret template for production
└── job-migrate.yml                     # EF Core migration Job
scripts/
├── init.sh                             # Service orchestration + seed
├── init-localstack.sh                  # S3 + KMS + Secrets setup
└── seed-db.sh                          # SQL Server schema + seed data
```

## Testing

```bash
dotnet test tests/ProjectTalaria.Tests.Unit
```

Unit tests cover token generation logic, API key hashing, and repository interactions.

## Recent Security Improvements

- **Logging**: Route patterns logged instead of raw paths to prevent sensitive data leakage
- **Exception Handling**: Structured `application/problem+json` responses; stack traces suppressed in production
- **Token Endpoint**: Runtime `LogCritical` warning if `/dev/token` enabled outside Development
- **Secrets**: DB connection string moved from ConfigMap to Kubernetes Secret
- **Migrations**: Replaced `EnsureCreated()` with EF Core `Database.Migrate()` + migration Job
- **Rate Limiting**: Values externalized to `RateLimiting:Fixed` configuration section

## License

MIT
