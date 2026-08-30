# CEBAS (Celoteh Bebas)

> **A high-concurrency, real-time web social platform for unhindered public conversation.**

---

## 1. Project Overview

CEBAS is architected from the ground up for high-performance, real-time social interactions with strong modularity, observability, security, and database integrity. The platform eliminates arbitrary daily limits on core engagements while enforcing robust safety controls, multi-device stateful sessions, and transaction-safe event streaming.

This repository is organized as a clean, decoupled monorepo containing two independently deployable applications:

```text
CEBAS/
├── .env.example            # Master environment variable template
├── .gitignore              # Monorepo-wide ignore rules
├── docker-compose.yml      # Local infrastructure (PostgreSQL 16, PgBouncer, Redis 7, MinIO)
├── README.md               # Master engineering documentation
│
├── backend/                # .NET 10 Clean Architecture Web API
│   ├── CEBAS.sln
│   ├── src/
│   │   ├── CEBAS.Api/            # HTTP Controllers, RFC 7807 Middleware, Swagger
│   │   ├── CEBAS.Application/    # Contracts, DTOs, Behaviors, FluentValidation
│   │   ├── CEBAS.Domain/         # Universal UUIDv7 (RFC 9562), Entity, Exceptions
│   │   └── CEBAS.Infrastructure/ # EF Core, Npgsql, PgBouncer, Redis, S3/MinIO
│   ├── tests/
│   │   ├── CEBAS.UnitTests/      # UUIDv7, Serialization & Contract Tests
│   │   └── CEBAS.IntegrationTests/# Health, API Routing & Migration Tests
│   ├── migrations/
│   │   └── sql/001_extensions.sql # Foundational extensions & ENUM types
│   └── README.md
│
└── frontend/               # Next.js 15 App Router Web Client
    ├── app/                # Layout, Pages, Semantic CSS
    ├── components/ui/      # WCAG 2.2 AA UI Primitives (Button, Input, Modal, etc.)
    ├── hooks/              # Custom React Hooks
    ├── lib/api/            # Fetch API Client & RFC 7807 Problem Details Error Normalizer
    ├── providers/          # TanStack Query Provider
    ├── stores/             # Zustand State Stores
    ├── types/              # Shared TypeScript API & Cursor Pagination Contracts
    ├── scripts/            # OpenAPI to TypeScript typegen scripts
    ├── package.json
    └── README.md
```

---

## 2. Technology Baseline

| Layer / Service | Technology | Version | Purpose |
|---|---|---|---|
| **Backend Framework** | .NET / ASP.NET Core | 10.0 | High-performance Web API engine |
| **Persistence ORM** | EF Core / Npgsql | 10.0 | Relational database mapping & migrations |
| **Primary Database** | PostgreSQL | 16 (Alpine) | ACID-compliant transactional persistence |
| **Connection Pooler** | PgBouncer | 1.22 | Transaction-level connection pooling |
| **Cache & Pub/Sub** | Redis | 7 (Alpine) | In-memory cache & real-time message backplane |
| **Object Storage** | MinIO (S3 API) | Latest | Decoupled binary asset & media storage |
| **Primary Keys** | Universal UUIDv7 | RFC 9562 | Monotonic time-ordered 128-bit identifiers |
| **API Error Standard** | Problem Details | RFC 7807 | Normalized, secure API error envelopes |
| **Frontend Framework** | Next.js (App Router) | 15.1 | Server and client rendered UI |
| **Language & Styling**| TypeScript / Tailwind | 5.7 / 3.4 | Type safety & semantic design token system |
| **State & Data Fetch** | Zustand / TanStack Query | 5.0 / 5.66 | UI state management & server state caching |

---

## 3. Quick Start Guide

### 3.1 Prerequisites

Ensure the following tools are installed on your machine:
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js](https://nodejs.org/) (v20+ or v25+) and `npm`
- [Docker](https://www.docker.com/) and `docker compose` (or local PostgreSQL 16)

### 3.2 Environment Setup

Copy `.env.example` to create your local environment file:

```bash
# Windows PowerShell
Copy-Item .env.example .env

# Linux / macOS
cp .env.example .env
```

### 3.3 Start Local Infrastructure (Docker)

Launch PostgreSQL 16, PgBouncer, Redis 7, and MinIO:

```bash
docker compose up -d
```

Verify that all containers are healthy:

```bash
docker compose ps
```

| Service | Container Name | Host Port | Purpose |
|---|---|---|---|
| PostgreSQL | `cebas-postgres` | 5432 | Primary relational store |
| PgBouncer | `cebas-pgbouncer` | 6432 | Connection pooler (app default) |
| Redis | `cebas-redis` | 6379 | In-memory cache & Pub/Sub |
| MinIO API | `cebas-minio` | 9000 | S3-compatible media storage |
| MinIO Console | `cebas-minio` | 9001 | MinIO Web Management Console |

---

## 4. Database Migrations Baseline

CEBAS uses deterministic, sequential SQL scripts and EF Core migrations.

The initial baseline migration script is located at:
`backend/migrations/sql/001_extensions.sql`

It establishes:
1. `uuid-ossp` and `citext` PostgreSQL extensions.
2. Custom domain ENUM types:
   - `user_role_enum` (`USER`, `MODERATOR`, `ADMIN`)
   - `media_type_enum` (`IMAGE`, `VIDEO`, `AUDIO`)
   - `media_status_enum` (`UPLOADING`, `READY`, `FAILED`, `DELETED`)
   - `notification_type_enum` (`POST_LIKED`, `POST_REPLIED`, `REPLY_LIKED`, `USER_FOLLOWED`, `USER_MENTIONED`)

### Running Migrations

```bash
# Direct via psql
psql -h localhost -p 5432 -U cebas_admin -d cebas_db -f backend/migrations/sql/001_extensions.sql

# Via Docker Compose
docker compose exec -T postgres psql -U cebas_admin -d cebas_db < backend/migrations/sql/001_extensions.sql
```

*(Note: In Development mode, the backend's `DatabaseMigrator` service also verifies and applies `001_extensions.sql` automatically upon startup).*

---

## 5. Backend Development

### Build & Test Solution

```bash
# Build all backend projects
dotnet build backend/CEBAS.sln

# Execute Unit and Integration test suites
dotnet test backend/CEBAS.sln
```

### Run Backend API

```bash
cd backend/src/CEBAS.Api
dotnet run
```

### Endpoints:
- **Liveness Health**: `http://localhost:5000/health`
- **API Ping**: `http://localhost:5000/api/v1/ping`
- **Swagger / OpenAPI Documentation**: `http://localhost:5000/swagger`
- **RFC 7807 Error Verification**: `http://localhost:5000/api/v1/error-test?type=validation`

---

## 6. Frontend Development

### Installation & Run

```bash
cd frontend
npm install
npm run dev
```

Open [http://localhost:3000](http://localhost:3000) to view the CEBAS Phase 0 verification portal and interactive UI component showcase.

### Linting & Production Build

```bash
npm run lint
npm run build
```

---

## 7. Key Architectural Invariants

1. **Universal UUIDv7 (ADR-01 & RFC 9562)**:
   - All primary keys across all domain entities use UUIDv7.
   - Preserves B-Tree index page locality, prevents insert fragmentation, and natively supports keyset cursor pagination without sequential ID leaking.
2. **PgBouncer Pooling (ADR-02 / ADR-09)**:
   - Applications connect via PgBouncer (port 6432) in transaction pool mode to scale client concurrency without exhausting database connection limits.
3. **Decoupled Media Storage (ADR-06)**:
   - Binary blobs are never stored inside PostgreSQL. MinIO / S3 stores binary media while PostgreSQL stores only metadata and references.
4. **RFC 7807 Problem Details**:
   - All API errors conform strictly to RFC 7807 schema (`type`, `title`, `status`, `detail`, `instance`, `traceId`, `errors`), preventing internal stack traces or connection strings from leaking to clients.

---

## 8. Phase 0 Scope Boundary & Roadmap

Phase 0 establishes the technical foundation, database baseline, and scaffolding.

### Intentionally Deferred to Phase 1+:
- User Registration, Login, Sessions, JWT, Password Hashing (Phase 1)
- User Profiles, Avatars, Social Graph (Phase 1)
- Posts, Replies, Threads, Media attachments (Phase 2)
- Likes, Reactions, Bookmarks, Timelines (Phase 2)
- Notifications, WebSockets, Moderation, Reporting, Blocking (Phase 3)
