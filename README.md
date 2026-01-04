# Maliev Currency Service

[![Build Status](https://img.shields.io/badge/Build-Passing-success)](https://github.com/ORGANIZATION/Maliev.CurrencyService)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Database](https://img.shields.io/badge/Database-PostgreSQL%2018-blue)](https://www.postgresql.org/)

Production-ready microservice providing currency metadata, live exchange rates, and historical snapshots.

**Role in MALIEV Architecture**: The centralized provider for all financial calculations. It delivers real-time exchange rates with automatic provider failover, maintains historical rate snapshots for auditing, and ensures consistent currency metadata across the platform.

---

## 🏗️ Architecture & Tech Stack

- **Framework**: ASP.NET Core 10.0 (C# 13)
- **Database**: PostgreSQL 18 with Entity Framework Core 10.x
- **Distributed Cache**: Redis 7.x (Two-tier L1/L2 caching for high performance)
- **Messaging**: RabbitMQ via MassTransit
- **Resilience**: Polly Chain (Fawazahmed → Frankfurter → Stale Fallback)
- **API Documentation**: OpenAPI 3.1 + Scalar UI
- **Observability**: OpenTelemetry (Metrics, Traces, Logging)

---

## ⚖️ Constitution Rules

This service strictly adheres to the platform development mandates:

### Banned Libraries
To maintain high performance and low complexity, the following are **NOT** used:
- ❌ **AutoMapper**: Explicit manual mapping only.
- ❌ **FluentValidation**: Standard Data Annotations (`[Required]`, `[EmailAddress]`) only.
- ❌ **FluentAssertions**: Standard xUnit `Assert` methods only.
- ❌ **In-memory Test DB**: All integration tests use **Testcontainers** with real PostgreSQL 18.

### Mandatory Practices
- ✅ **TreatWarningsAsErrors**: Enabled in all `.csproj` files.
- ✅ **XML Documentation**: Required on all public methods and properties.
- ✅ **No Secrets in Code**: All sensitive configuration injected via environment variables.
- ✅ **No Test Config in Program.cs**: Test configuration in test fixtures only.
- ✅ **IAM Integration**: Self-registers permissions with the IAM Service using GCP-style naming: `{service}.{resource}.{action}`.

---

## ✨ Key Features

- **Transitive Conversion Engine**: Automatic discovery of conversion paths when direct pairs are unavailable.
- **Resilient Provider Chain**: Industrial-grade failover between multiple exchange rate providers with <2s switching.
- **Audit-Ready Snapshots**: High-precision historical rate tracking for accounting and financial reporting.
- **Multi-Tier Caching**: Sub-50ms p95 latency via instance-local memory and distributed Redis caching.
- **Batch Metadata Processing**: Global currency metadata covering 170+ legal tenders with country mappings.

---

## 🚀 Quick Start

### Prerequisites
- .NET 10.0 SDK
- Docker Desktop (for infrastructure)
- PostgreSQL 18 (Alpine)

### Local Development Setup

1. **Clone the repository**
```bash
git clone https://github.com/ORGANIZATION/Maliev.CurrencyService.git
cd Maliev.CurrencyService
```

2. **Spin up Infrastructure**
```bash
docker run --name currency-db -e POSTGRES_PASSWORD=YOUR_PASSWORD -p 5432:5432 -d postgres:18-alpine
docker run --name currency-redis -p 6379:6379 -d redis:7-alpine
```

3. **Configure Environment**
```powershell
# Windows PowerShell
$env:ConnectionStrings__CurrencyDbContext="YOUR_POSTGRES_CONNECTION_STRING"
$env:ConnectionStrings__Cache="YOUR_REDIS_CONNECTION_STRING"
```

4. **Apply Migrations & Run**
```bash
dotnet ef database update --project Maliev.CurrencyService.Api
dotnet run --project Maliev.CurrencyService.Api
```

The service will be available at `http://localhost:5000/currencies`. Access the interactive documentation at `http://localhost:5000/currencies/scalar`.

---

## 📡 API Endpoints

All endpoints are prefixed with `/currencies/v1/`.

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/currencies` | List all supported currencies |
| GET | `/rates?from=USD&to=THB` | Get live or snapshot exchange rates |
| POST | `/admin/snapshots/ingest` | Bulk ingestion of historical rates |
| GET | `/currencies/country/{code}` | Get currency by ISO country code |

---

## 🏥 Health & Monitoring

Standardized health probes for Kubernetes orchestration:
- **Liveness**: `GET /currencies/liveness`
- **Readiness**: `GET /currencies/readiness` (Checks DB and Redis connectivity)
- **Metrics**: `GET /currencies/metrics` (Prometheus format)

---

## 🧪 Testing

We prioritize reliable tests over mock-heavy unit tests.

```bash
# Run all tests using Testcontainers
dotnet test --verbosity normal
```

- **Integration Tests**: Use real PostgreSQL 18 containers.
- **Contract Tests**: Ensure API stability for consumers.

---

## 📦 Deployment

Infrastructure management is handled via GitOps patterns.

- **Docker Image**: `REGION-docker.pkg.dev/PROJECT_ID/REPOSITORY/maliev-currency-service:{sha}`
- **Environments**: Development, Staging, Production

---

## 📄 License

Proprietary - © 2025 MALIEV Co., Ltd. All rights reserved.
