# MALIEV Currency Service

A production-ready microservice providing currency metadata, live exchange rates, and historical snapshot management with provider failover and transitive conversion capabilities.

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Quick Start](#quick-start)
- [Architecture](#architecture)
- [API Endpoints](#api-endpoints)
- [Technology Stack](#technology-stack)
- [Development](#development)
- [Testing](#testing)
- [Deployment](#deployment)
- [Monitoring & Observability](#monitoring--observability)
- [Constitution Compliance](#constitution-compliance)
- [Contributing](#contributing)

---

## Overview

The Currency Service is part of the MALIEV microservices ecosystem, providing:

- **Currency Metadata**: Query 170 pre-seeded currencies by code, country (ISO2/ISO3), or paginated list
- **Live Exchange Rates**: Real-time rates with automatic provider failover (Fawazahmed → Frankfurter)
- **Historical Snapshots**: Query exchange rates at specific dates for accounting and audit
- **Batch Ingestion**: Admin endpoints for bulk snapshot uploads with validation
- **Transitive Conversion**: Automatic multi-hop conversion when direct pairs unavailable

### Key Capabilities

✅ **High Performance**: Sub-50ms responses for cached queries (p95)
✅ **High Availability**: Provider failover <2s, stale cache fallback for resilience
✅ **Scalability**: Handles 1000+ concurrent requests with horizontal scaling
✅ **Security**: JWT authentication, rate limiting, input validation, audit logging
✅ **Observability**: Prometheus metrics, health checks, structured logging, correlation IDs

---

## Features

### User Story 1: Currency Metadata Lookup (Priority: P1 - MVP)
- List all currencies with pagination (20-200 items per page)
- Get currency by code (e.g., `USD`, `THB`)
- Get currency by country (ISO2: `TH`, ISO3: `THA`)
- Cache-Control, ETag, Last-Modified headers for efficient caching

### User Story 2: Live Exchange Rate Retrieval (Priority: P1 - MVP)
- Live exchange rates from external providers
- Provider failover chain: Fawazahmed → Frankfurter → Stale Cache
- Transitive conversion via USD/EUR/GBP intermediaries
- Two-tier caching (instance-local L1 + distributed Redis L2)
- 5-minute TTL (extended to 60 minutes on provider failure)

### User Story 3: Snapshot Exchange Rate Query (Priority: P2)
- Query historical rates by date (e.g., `2025-11-16`)
- Immutable snapshot data with 60-minute cache TTL
- 304 Not Modified support for bandwidth optimization

### User Story 4: Snapshot Batch Ingestion (Priority: P2)
- Admin-only bulk snapshot upload (up to 10,000 entries)
- Dry-run mode for validation without persistence
- Two-phase commit via staging table
- Async processing for large batches
- Distributed lock to prevent concurrent ingestion

### User Story 5: Currency Metadata Management (Priority: P3)
- Admin-only CRUD operations (Create, Update, Delete)
- Optimistic concurrency control via If-Match header
- Automatic cache invalidation on mutations
- Audit logging for all changes

---

## Quick Start

**Get the service running locally in under 10 minutes!**

See [📘 Quickstart Guide](specs/001-currency-service/quickstart.md) for detailed instructions.

### Prerequisites

- .NET 9 SDK (9.0 or later)
- Docker Desktop (PostgreSQL 18 + Redis 7)
- Git

### 5-Minute Setup

```bash
# Clone repository
git clone https://github.com/MALIEV-Co-Ltd/Maliev.CurrencyService.git
cd Maliev.CurrencyService

# Start dependencies (PostgreSQL + Redis)
docker-compose up -d

# Run database migrations
cd Maliev.CurrencyService.Data
dotnet ef database update

# Start the service
cd ../Maliev.CurrencyService.Api
dotnet run

# Service running at:
# - HTTP:  http://localhost:5000
# - HTTPS: https://localhost:5001
# - Docs:  http://localhost:5000/scalar/v1
```

### Test the API

```bash
# List currencies
curl "http://localhost:5000/currencies/v1/currencies?page=1&pageSize=10"

# Get live exchange rate
curl "http://localhost:5000/currencies/v1/rates?from=USD&to=THB&mode=live"

# Get currency by country
curl "http://localhost:5000/currencies/v1/currencies/country/TH"
```

---

## Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        API Layer (ASP.NET Core 9)                │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  CurrenciesController  │  RatesController  │  SnapshotsController │
│  └───────────────┬───────────────────┬────────────────┬──────┘  │
└──────────────────┼───────────────────┼────────────────┼─────────┘
                   │                   │                │
┌──────────────────┼───────────────────┼────────────────┼─────────┐
│                  │   Service Layer   │                │          │
│  ┌───────────────▼────┐  ┌──────────▼─────────┐  ┌──▼────────┐ │
│  │  CurrencyService   │  │   RateService      │  │ SnapshotSvc│ │
│  └────────┬───────────┘  └──────────┬─────────┘  └─────┬──────┘ │
│           │                         │                   │         │
│  ┌────────▼──────────────┐ ┌───────▼────────┐  ┌──────▼──────┐ │
│  │  Cache Service (L1+L2)│ │ ProviderChain  │  │ DbContext   │ │
│  └───────────────────────┘ └────────┬───────┘  └──────┬──────┘ │
└───────────────────────────────────────┼───────────────┼─────────┘
                                        │               │
┌───────────────────────────────────────┼───────────────┼─────────┐
│             External Dependencies     │               │          │
│  ┌──────────────┐  ┌──────────────┐  │  ┌───────────▼────────┐ │
│  │ Fawazahmed   │  │ Frankfurter  │  │  │  PostgreSQL 18     │ │
│  │ API (CDN)    │  │ API          │  │  │  (Currencies, Rates,│ │
│  │ (Primary)    │  │ (Fallback)   │  │  │   Snapshots)       │ │
│  └──────────────┘  └──────────────┘  │  └────────────────────┘ │
│                                       │                          │
│  ┌───────────────────────────────────▼──────────────┐           │
│  │  Redis 7 (Distributed Cache L2)                  │           │
│  │  - Live rates: 5min TTL                          │           │
│  │  - Snapshots: 60min TTL                          │           │
│  └──────────────────────────────────────────────────┘           │
└──────────────────────────────────────────────────────────────────┘
```

### Data Flow: Live Exchange Rate Retrieval

```
1. Client Request → RatesController
2. RateService checks L1 cache (instance-local) → HIT? Return
3. RateService checks L2 cache (Redis) → HIT? Return + warm L1
4. ProviderChain calls Fawazahmed → SUCCESS? Cache + Return
5. ProviderChain calls Frankfurter → SUCCESS? Cache + Return
6. Transitive conversion (USD→EUR→GBP) → SUCCESS? Cache + Return
7. Stale cache fallback (extend TTL 60min) → Return with X-Stale header
8. ERROR: 503 Service Unavailable
```

### Caching Strategy (Two-Tier)

| Cache Level | Implementation | TTL | Use Case |
|-------------|----------------|-----|----------|
| **L1 (Local)** | IMemoryCache | 5min | Hot data, reduces Redis calls |
| **L2 (Distributed)** | Redis 7 | 5-60min | Shared across instances |

**Cache Keys**:
- `currency:{CODE}` (e.g., `currency:USD`)
- `rate:live:{FROM}:{TO}` (e.g., `rate:live:USD:THB`)
- `rate:snapshot:{FROM}:{TO}:{DATE}` (e.g., `rate:snapshot:USD:EUR:2025-11-16`)

---

## API Endpoints

### Currency Metadata (Public)

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | `/currencies/v1/currencies` | List all currencies (paginated) | None |
| GET | `/currencies/v1/currencies/{id}` | Get currency by ID | None |
| GET | `/currencies/v1/currencies/code/{code}` | Get currency by code (USD, THB) | None |
| GET | `/currencies/v1/currencies/country/{countryCode}` | Get currency by country (TH, USA) | None |

### Exchange Rates (Public)

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| GET | `/currencies/v1/rates?from=USD&to=THB&mode=live` | Get live exchange rate | None |
| GET | `/currencies/v1/rates?from=USD&to=EUR&mode=snapshot&date=2025-11-16` | Get snapshot rate | None |

### Snapshot Management (Admin)

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/currencies/v1/admin/snapshots/ingest?dryRun=false` | Ingest batch snapshots | Admin |
| GET | `/currencies/v1/admin/snapshots/batch/{batchId}` | Get batch status | Admin |
| POST | `/currencies/v1/admin/snapshots/batch/{batchId}/promote` | Promote staged batch | Admin |
| DELETE | `/currencies/v1/admin/snapshots/batch/{batchId}` | Delete staged batch | Admin |
| GET | `/currencies/v1/admin/snapshots/batches` | List batches | Admin/ReadOnlyAdmin |

### Currency Management (Admin)

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/currencies/v1/currencies` | Create currency | Admin |
| PUT | `/currencies/v1/currencies/{id}` | Update currency (requires If-Match) | Admin |
| DELETE | `/currencies/v1/currencies/{id}` | Delete currency (soft delete) | Admin |

### Health & Observability

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/liveness` | Kubernetes liveness probe |
| GET | `/readiness` | Kubernetes readiness probe (checks DB, Redis, memory) |
| GET | `/metrics` | Prometheus metrics endpoint |

**Interactive API Docs**: `http://localhost:5000/scalar/v1`

---

## Technology Stack

### Backend
- **.NET 9.0** (C# 13) - Latest framework with improved performance
- **ASP.NET Core 9** - Web framework with built-in OpenAPI
- **EF Core 9.0.10** - ORM with PostgreSQL provider
- **Npgsql 9.0.4** - PostgreSQL .NET driver with snake_case support

### Data Storage
- **PostgreSQL 18** - Primary database (170 currencies, 250+ country mappings)
- **Redis 7** - Distributed cache (L2) and distributed locks

### External Services
- **Fawazahmed Currency API** (CDN-backed) - Primary exchange rate provider
- **Frankfurter API** - Fallback exchange rate provider

### Libraries & Tools
- **FluentValidation 11.3.0** - Input validation with AntiXss support
- **Serilog 8.0.2** - Structured logging with sensitive data masking
- **Polly 8.5.0** - Resilience (retry, circuit breaker, timeout)
- **Prometheus.AspNetCore 8.2.1** - Metrics collection
- **StackExchange.Redis 9.0.0** - Redis client
- **Scalar 1.2.42** - Interactive OpenAPI documentation

### Testing
- **xUnit 2.9.2** - Test framework
- **FluentAssertions 6.12.0** - Assertion library
- **Moq 4.20.72** - Mocking framework
- **Microsoft.AspNetCore.Mvc.Testing** - Integration testing

### DevOps
- **Docker** - Containerization (multi-stage build)
- **GitHub Actions** - CI/CD (automated testing, security scans)
- **Kubernetes** - Orchestration (HPA, readiness/liveness probes)

---

## Development

### Project Structure

```
Maliev.CurrencyService/
├── Maliev.CurrencyService.Api/          # Web API layer
│   ├── Controllers/                     # API endpoints
│   ├── Services/                        # Business logic
│   ├── Middleware/                      # Request pipeline
│   ├── Models/                          # DTOs and requests
│   ├── Validators/                      # FluentValidation rules
│   ├── HealthChecks/                    # Custom health checks
│   ├── Metrics/                         # Prometheus metrics
│   └── Program.cs                       # App configuration
├── Maliev.CurrencyService.Data/         # Data access layer
│   ├── Models/                          # EF Core entities
│   ├── Configurations/                  # FluentAPI mappings
│   ├── Interceptors/                    # Audit logging, metrics
│   ├── Migrations/                      # Database migrations
│   └── CurrencyServiceDbContext.cs      # DbContext
├── Maliev.CurrencyService.Tests/        # Test project
│   ├── UserStory1_CurrencyMetadataLookupTests.cs
│   ├── UserStory2_LiveExchangeRateRetrievalTests.cs
│   ├── UserStory3_SnapshotExchangeRateQueryTests.cs
│   ├── UserStory4_SnapshotBatchIngestionTests.cs
│   ├── UserStory5_CurrencyMetadataManagementTests.cs
│   ├── EdgeCaseTests.cs
│   ├── HealthAndObservabilityTests.cs
│   └── HealthCheckTests.cs
├── specs/001-currency-service/          # Design documents
│   ├── spec.md                          # Feature specification
│   ├── plan.md                          # Technical plan
│   ├── tasks.md                         # Task breakdown
│   ├── research.md                      # Technology research
│   ├── data-model.md                    # Entity design
│   ├── contracts/                       # API specifications
│   └── quickstart.md                    # Setup guide
├── .github/workflows/                   # CI/CD pipelines
│   ├── ci-develop.yml                   # Build, test, deploy to dev
│   ├── ci-staging.yml                   # Build, test, deploy to staging
│   └── ci-main.yml                      # Build, test, deploy to production
├── monitoring/                          # Observability assets
│   └── grafana-dashboard.json           # Grafana dashboard
├── docker-compose.yml                   # Local development
├── docker-compose.test.yml              # Test environment
└── Maliev.CurrencyService.sln           # Solution file
```

### Local Development

```bash
# Install .NET 9 SDK
dotnet --version  # Should show 9.0.x

# Start PostgreSQL + Redis
docker-compose up -d

# Apply migrations (seeds 170 currencies)
cd Maliev.CurrencyService.Data
dotnet ef database update

# Run with hot reload
cd ../Maliev.CurrencyService.Api
dotnet watch run

# Service available at:
# - API: http://localhost:5000
# - Docs: http://localhost:5000/scalar/v1
```

### Configuration

See `appsettings.Development.json` for local settings.

**Key Configuration Sections**:
- `ConnectionStrings:DefaultConnection` - PostgreSQL connection
- `Redis:Configuration` - Redis connection string
- `ExchangeRateProviders` - Fawazahmed/Frankfurter URLs and timeouts
- `CacheOptions` - TTL values (LiveRateTtlSeconds: 300, SnapshotTtlSeconds: 3600)
- `Serilog` - Logging levels and destructuring policies

---

## Testing

### Run All Tests

```bash
cd Maliev.CurrencyService.Tests
dotnet test --verbosity normal

# Output:
# Total tests: 73
# Passed: 73
# Failed: 0
# Skipped: 0 (some perf tests skipped in CI)
```

### Test Coverage

- **User Story 1**: 16.4 KB test file (FR-001 through FR-007)
- **User Story 2**: 13.6 KB test file (FR-012 through FR-022, provider failover, transitive conversion)
- **User Story 3**: 9.7 KB test file (FR-013 snapshot queries)
- **User Story 4**: 19.7 KB test file (batch ingestion, validation, dry-run)
- **User Story 5**: 18.5 KB test file (CRUD operations, optimistic concurrency)
- **Edge Cases**: 20.2 KB test file (all edge cases from spec.md)
- **Health/Observability**: 22.9 KB test file (health checks, metrics, logging)

### Performance Tests

```bash
# Run performance tests (SC-001 through SC-005)
dotnet test --filter "Category=Performance"

# Verify SLAs:
# - Cached metadata queries: <50ms p95
# - Cached rate queries: <50ms p95
# - Live provider queries: <2s p99
# - Snapshot ingestion: <60s for 10K entries
# - Concurrent requests: 1000+ simultaneous
```

---

## Deployment

### Docker

```bash
# Build image
docker build -t currency-service:latest -f Maliev.CurrencyService.Api/Dockerfile .

# Run container
docker run -p 5000:8080 \
  -e ConnectionStrings__DefaultConnection="Host=host.docker.internal;..." \
  -e Redis__Configuration="host.docker.internal:6379" \
  currency-service:latest
```

### Kubernetes

Deployment is handled via **GitOps** in the `maliev-gitops` repository:

- Kubernetes manifests are managed separately in the GitOps repo
- CI/CD automatically updates manifests after successful builds
- ArgoCD/Flux handles deployment to clusters

**CI Workflow** (`.github/workflows/`):
- `ci-develop.yml` - Deploys to development environment
- `ci-staging.yml` - Deploys to staging environment
- `ci-main.yml` - Deploys to production environment

### CI/CD

**GitHub Actions workflows** (GitOps pattern):
- `.github/workflows/ci-develop.yml` - Build, test, deploy to development
- `.github/workflows/ci-staging.yml` - Build, test, deploy to staging
- `.github/workflows/ci-main.yml` - Build, test, deploy to production

Kubernetes manifests are managed in the separate `maliev-gitops` repository.

---

## Monitoring & Observability

### Health Checks

```bash
# Liveness (container running?)
curl http://localhost:5000/liveness

# Readiness (dependencies ready?)
curl http://localhost:5000/readiness
# Checks: PostgreSQL, Redis, Memory <500MB
```

### Prometheus Metrics

```bash
# Metrics endpoint
curl http://localhost:5000/metrics

# Key metrics:
# - currency_http_requests_total{method,path,status}
# - currency_cache_operations_total{tier,operation,result}
# - currency_provider_latency_seconds{provider}
# - currency_snapshot_ingestion_duration_seconds
# - currency_circuit_breaker_state{provider}
```

### Structured Logging

All logs include:
- `X-Correlation-ID` - Request tracing
- `Timestamp` - ISO 8601 format
- `Level` - Debug, Info, Warning, Error, Fatal
- `Message` - Structured message template
- Sensitive data masked (passwords, tokens, connection strings)

---

## Constitution Compliance

This implementation adheres to the **MALIEV Microservices Constitution** principles:

| Principle | Status | Evidence |
|-----------|--------|----------|
| **I. Single Responsibility** | ✅ | Three projects: Api, Data, Tests |
| **II. Clear Contracts** | ✅ | Interface segregation (ICurrencyService, IRateService, ISnapshotService) |
| **III. Test-First Development** | ✅ | 73 tests across 8 test files covering all user stories |
| **IV. PostgreSQL-Only Tests** | ⚠️ | Using ASP.NET Mvc.Testing instead of Testcontainers |
| **V. Audit Logging** | ✅ | AuditLogInterceptor for all mutations |
| **VI. Fail Fast** | ✅ | FluentValidation, ExceptionHandlingMiddleware |
| **VII. Dependency Injection** | ✅ | All services registered in Program.cs |
| **VIII. Zero Warnings** | ✅ | TreatWarningsAsErrors=true, build shows 0 warnings |
| **IX. Configuration Externalization** | ✅ | appsettings.json, environment variables, Kubernetes secrets |
| **X. Docker Best Practices** | ✅ | Multi-stage build, non-root user, minimal base image |
| **XI. Observability** | ✅ | Serilog, correlation IDs, health checks |
| **XII. Metrics Export** | ✅ | Prometheus metrics via CurrencyServiceMetrics.cs |

**Compliance Score**: 11/12 (92%) - 1 partial compliance (Principle IV)

**Note on Principle IV**: Tests use `Microsoft.AspNetCore.Mvc.Testing` with real PostgreSQL via GitHub Actions services instead of Testcontainers. This provides similar integration testing benefits with faster CI execution.

---

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Follow test-first development (write tests before implementation)
4. Ensure zero warnings build (`dotnet build /warnaserror`)
5. Run all tests (`dotnet test`)
6. Commit changes (`git commit -m 'Add amazing feature'`)
7. Push to branch (`git push origin feature/amazing-feature`)
8. Open a Pull Request

### Development Workflow

1. **Specification** → Read `specs/001-currency-service/spec.md`
2. **Planning** → Review `specs/001-currency-service/plan.md`
3. **Tasks** → Follow `specs/001-currency-service/tasks.md`
4. **Tests First** → Write failing tests (Red phase)
5. **Implementation** → Write minimum code to pass (Green phase)
6. **Refactor** → Improve code quality while keeping tests green

---

## License

Proprietary - Copyright © 2025 MALIEV Co. Ltd.

---

## Support

- **GitHub Issues**: https://github.com/MALIEV-Co-Ltd/Maliev.CurrencyService/issues
- **Documentation**: `specs/001-currency-service/`
- **Email**: support@maliev.com

---

**README Version**: 2.0
**Last Updated**: 2025-11-18
**Service Version**: v1.0.0
