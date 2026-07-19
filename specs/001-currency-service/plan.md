# Implementation Plan: Currency WebAPI Service

**Branch**: `001-currency-service` | **Date**: 2025-11-17 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-currency-service/spec.md`

**Note**: This plan documents the implementation of the Currency WebAPI Service with comprehensive technical specifications.

## Summary

The Currency WebAPI Service is a .NET 10 microservice that provides global currency metadata management and real-time/historical exchange rate queries. The service integrates with free external providers (Fawazahmed, Frankfurter) with automatic failover, implements transitive currency conversion via intermediary currencies, and delivers sub-50ms p95 read response times through aggressive two-tier caching (in-memory + Redis). Administrative capabilities include bulk snapshot ingestion with dry-run validation, currency CRUD operations with optimistic concurrency control, and RBAC-protected endpoints. The implementation follows Clean Architecture with PostgreSQL for persistence, comprehensive observability via Prometheus metrics, and production-grade resilience patterns.

## Technical Context

**Language/Version**: .NET 10 (C# 13) with ASP.NET Core 10.0
**Primary Dependencies**:
- Entity Framework Core 9.0.10 (data access)
- Npgsql 9.0.4 (PostgreSQL provider)
- Polly 8.5.0 (HTTP resilience)
- FluentValidation 11.3.0 (request validation)
- Serilog 8.0.2 (structured logging)
- StackExchange.Redis 9.0.0 (distributed caching)
- Prometheus.AspNetCore 8.2.1 (metrics)
- Scalar 1.2.42 (API documentation)

**Storage**: PostgreSQL 18 (currency metadata, snapshots, staged data) + Redis (distributed cache)
**Testing**: xUnit with real PostgreSQL containers (Testcontainers), FluentAssertions, Moq
**Target Platform**: Linux containers (Docker) on Kubernetes, small instances (2 vCPU, 4GB RAM)
**Project Type**: Microservice WebAPI with Clean Architecture (Api, Data, Tests layers)
**Performance Goals**:
- Sub-50ms p95 latency for cached queries
- 1000 concurrent requests without degradation
- 80%+ cache hit ratio
- Provider failover <2 seconds
- Memory <500MB per instance

**Constraints**:
- External provider calls: 500-1000ms max
- Snapshot ingestion: 10,000 entries in <60 seconds
- Cache invalidation: atomic within 5 seconds
- Rate limiting: 100 req/min per API key
- No authentication for external providers (free APIs)

**Scale/Scope**:
- ~170 global currencies with country associations
- 20-50 hot currency pairs pre-cached
- 12-month snapshot retention window
- Two-replica deployment for HA

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence/Notes |
|-----------|--------|----------------|
| **I. Service Autonomy** | ✅ PASS | Own database (currency_app_db), own domain logic, no cross-service DB access |
| **II. Explicit Contracts** | ✅ PASS | OpenAPI/Scalar documentation, versioned APIs (v1.0) |
| **III. Test-First Development** | ✅ PASS | Comprehensive test suite with >280 tests, contract/integration/unit coverage |
| **IV. PostgreSQL-Only Testing** | ✅ PASS | All tests use real PostgreSQL (Testcontainers), no in-memory DB |
| **V. Auditability & Observability** | ✅ PASS | Serilog structured logging, health checks (/liveness, /readiness), Prometheus metrics |
| **VI. Security & Compliance** | ✅ PASS | JWT authentication, RBAC (Admin/ReadOnlyAdmin), rate limiting, input validation |
| **VII. Secrets Management** | ✅ PASS | Google Secret Manager integration, no secrets in source code |
| **VIII. Zero Warnings Policy** | ✅ PASS | TreatWarningsAsErrors enabled, build produces zero warnings |
| **IX. Clean Project Artifacts** | ✅ PASS | .gitignore and .dockerignore configured, no unused boilerplate |
| **X. Docker Best Practices** | ✅ PASS | Multi-stage build, built-in app user, .NET 10 base images, health checks |
| **XI. Simplicity & Maintainability** | ✅ PASS | Clean Architecture, YAGNI applied, stateless design |
| **XII. Business Metrics & Analytics** | ✅ PASS | Prometheus metrics for request rates, provider latency, cache hit/miss, background jobs |

**Overall Gate Status**: ✅ **PASSED** - All constitutional requirements satisfied

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
Maliev.CurrencyService/
├── Maliev.CurrencyService.Api/              # WebAPI Layer (Controllers, DTOs, Services)
│   ├── Controllers/
│   │   ├── CurrenciesController.cs          # Currency metadata CRUD
│   │   ├── RatesController.cs               # Live/snapshot rate queries
│   │   └── SnapshotsController.cs           # Bulk snapshot ingestion
│   ├── Services/
│   │   ├── ICurrencyService.cs              # Currency business logic interface
│   │   ├── CurrencyService.cs               # Currency implementation
│   │   ├── IRateService.cs                  # Rate lookup interface
│   │   ├── RateService.cs                   # Rate implementation
│   │   ├── ISnapshotService.cs              # Snapshot management interface
│   │   ├── SnapshotService.cs               # Snapshot implementation
│   │   ├── ICacheService.cs                 # Cache abstraction
│   │   ├── InMemoryCacheService.cs          # Local cache
│   │   ├── RedisCacheService.cs             # Redis distributed cache
│   │   ├── CacheTagService.cs               # Cache key management
│   │   └── External/
│   │       ├── IExchangeRateProvider.cs     # Provider interface
│   │       ├── FawazahmedProvider.cs        # Fawazahmed API client
│   │       ├── FrankfurterProvider.cs       # Frankfurter API client
│   │       └── ProviderChain.cs             # Failover orchestration
│   ├── Models/
│   │   ├── Currencies/                      # Currency DTOs
│   │   ├── Rates/                           # Rate query/response DTOs
│   │   ├── Snapshots/                       # Snapshot ingestion DTOs
│   │   ├── Common/                          # Shared DTOs (ErrorResponse, etc.)
│   │   └── ApiResponses/                    # External provider models
│   ├── Validators/                          # FluentValidation validators
│   ├── Middleware/                          # Exception handling, correlation ID
│   ├── BackgroundServices/                  # Cache warming, cleanup
│   ├── Metrics/                             # Prometheus metrics
│   └── HealthChecks/                        # Custom health checks
│
├── Maliev.CurrencyService.Data/             # Data Access Layer
│   ├── Models/
│   │   ├── Currency.cs                      # Currency entity
│   │   ├── CountryCurrency.cs               # Country-currency association
│   │   ├── ExchangeRate.cs                  # Live rate entity
│   │   ├── RateSnapshot.cs                  # Historical snapshot entity
│   │   └── StagedSnapshot.cs                # Temporary staging entity
│   ├── Configurations/                      # EF Core FluentAPI configurations
│   ├── Migrations/                          # EF Core migrations
│   ├── Interceptors/                        # Audit log, metrics interceptors
│   ├── CurrencyServiceDbContext.cs          # Main DbContext
│   └── CurrencyServiceDbContextFactory.cs   # Design-time factory
│
├── Maliev.CurrencyService.Tests/            # Test Suite
│   ├── UserStory1_CurrencyMetadataLookupTests.cs
│   ├── UserStory2_LiveExchangeRateRetrievalTests.cs
│   ├── UserStory3_SnapshotExchangeRateQueryTests.cs
│   ├── UserStory4_SnapshotBatchIngestionTests.cs
│   ├── UserStory5_CurrencyMetadataManagementTests.cs
│   ├── EdgeCaseTests.cs                     # Edge case validation
│   ├── HealthAndObservabilityTests.cs       # Health check tests
│   └── TestAuthenticationHandler.cs         # Test JWT mock
│
├── docker-compose.yml                       # Local dev environment
├── docker-compose.test.yml                  # Test PostgreSQL setup
├── Dockerfile                               # Production container
└── .dockerignore                            # Docker build exclusions
```

**Structure Decision**: Clean Architecture with three-layer separation:
- **Api Layer**: REST endpoints, business services, external integrations
- **Data Layer**: EF Core entities, DbContext, migrations
- **Tests Layer**: User story-based test organization with real PostgreSQL

This structure enables independent testing of each layer, clear separation of concerns, and easy navigation by feature (user story).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

**Status**: ✅ No violations - All constitutional principles satisfied

This implementation adheres to all MALIEV Microservices Constitution principles without requiring any exceptions or justifications. The three-project structure (Api, Data, Tests) is minimal and follows Clean Architecture best practices without introducing unnecessary complexity.
