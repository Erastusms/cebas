# CEBAS Backend (.NET 10 Web API)

This is the backend service for **CEBAS (Celoteh Bebas)**, engineered for high-concurrency, real-time social interactions following Clean Architecture principles.

## Architectural Layers

```
Api (Presentation / HTTP Layer)
 └── Application (Use Cases, Validation & Abstractions)
      └── Domain (Entities, Universal UUIDv7 & Exceptions)

Infrastructure (Persistence, EF Core, Redis, Storage Adapters)
 ├── Application
 └── Domain
```

## Features & Baseline (Phase 0)

- **Target Framework**: .NET 10 (`net10.0`)
- **Universal UUIDv7**: Time-ordered monotonic 128-bit identifiers (RFC 9562) with zero index fragmentation.
- **Global Error Handling**: Standardized RFC 7807 Problem Details middleware.
- **Structured Logging**: Serilog configured for console & structured output with correlation tracking.
- **OpenAPI / Swagger**: Swagger UI available at `/swagger`.
- **Database Pooling**: PgBouncer + PostgreSQL 16 connection support.
- **Validation Pipeline**: FluentValidation integration.

## Project Structure

```
backend/
├── CEBAS.sln
├── src/
│   ├── CEBAS.Api/              # Controllers, Middleware, API Startup
│   ├── CEBAS.Application/      # DTOs, Abstractions, Validation Runner
│   ├── CEBAS.Domain/           # Base Entity, UUIDv7 Generator, Exceptions
│   └── CEBAS.Infrastructure/   # EF Core DbContext, Redis, Migrations
├── tests/
│   ├── CEBAS.UnitTests/        # Unit tests for UUIDv7, Serialization, Contracts
│   └── CEBAS.IntegrationTests/ # Integration tests for Health, API & DB
└── migrations/
    ├── sql/
    │   └── 001_extensions.sql  # uuid-ossp, citext, custom ENUMs
    └── README.md
```

## Running the Backend

### Prerequisites

- .NET 10 SDK
- Docker & Docker Compose (or local PostgreSQL 16)

### Build & Test

```bash
dotnet build backend/CEBAS.sln
dotnet test backend/CEBAS.sln
```

### Run Locally

```bash
cd backend/src/CEBAS.Api
dotnet run
```

The API will be available at:
- **Liveness Health**: `http://localhost:5000/health`
- **Ping Endpoint**: `http://localhost:5000/api/v1/ping`
- **Swagger Documentation**: `http://localhost:5000/swagger`
