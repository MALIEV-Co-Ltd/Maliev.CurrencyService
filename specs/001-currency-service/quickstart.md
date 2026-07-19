# Quickstart Guide: Currency WebAPI Service

**Feature**: Currency WebAPI Service
**Branch**: `001-currency-service`
**Date**: 2025-11-17
**Purpose**: Get the Currency WebAPI Service running locally in under 10 minutes

---

## Prerequisites

Before you begin, ensure you have:

- **.NET 10 SDK** (version 10.0 or later)
  ```bash
  dotnet --version  # Should show 10.0.x
  ```

- **Docker Desktop** (for PostgreSQL and Redis containers)
  ```bash
  docker --version  # Should show 20.10 or later
  ```

- **Git** (to clone the repository)

- **IDE** (Visual Studio 2022, VS Code, or Rider)

- **Postman or cURL** (for API testing)

---

## Quick Start (5 Minutes)

### Step 1: Clone the Repository

```bash
git clone https://github.com/MALIEV-Co-Ltd/Maliev.CurrencyService.git
cd Maliev.CurrencyService
```

### Step 2: Start Dependencies (PostgreSQL + Redis)

```bash
docker-compose up -d
```

This starts:
- **PostgreSQL 18** on port `5432`
- **Redis 7** on port `6379`

**Verify containers are running**:
```bash
docker ps
```

Expected output:
```
CONTAINER ID   IMAGE           PORTS                    NAMES
abc123...      postgres:18     0.0.0.0:5432->5432/tcp  currency-db
def456...      redis:7-alpine  0.0.0.0:6379->6379/tcp  currency-cache
```

### Step 3: Run Database Migrations

```bash
cd Maliev.CurrencyService.Data
dotnet ef database update
```

Expected output:
```
Build started...
Build succeeded.
Applying migration '20251102072848_InitialCreate'.
Done.
```

This creates:
- `currencies` table with 170 pre-seeded currencies
- `country_currencies` table with 250+ country mappings
- `exchange_rates`, `rate_snapshots`, `staged_snapshots` tables

### Step 4: Run the Service

```bash
cd ../Maliev.CurrencyService.Api
dotnet run
```

Expected output:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

**The service is now running at**:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`

### Step 5: Test the API

Open your browser or Postman and try:

**1. List all currencies**:
```
GET http://localhost:5000/currencies/v1/currencies?page=1&pageSize=10
```

**2. Get live exchange rate**:
```
GET http://localhost:5000/currencies/v1/rates?from=USD&to=THB&mode=live
```

**3. Get currency by country**:
```
GET http://localhost:5000/currencies/v1/currencies/country/TH
```

**Expected Response**:
```json
{
  "id": "...",
  "code": "THB",
  "symbol": "฿",
  "name": "Thai Baht",
  "decimalPlaces": 2,
  "isActive": true,
  "isPrimary": false,
  "createdAt": "2025-01-01T00:00:00Z",
  "updatedAt": "2025-01-01T00:00:00Z"
}
```

---

## API Documentation (Scalar)

**Interactive API documentation** is available at:

```
http://localhost:5000/scalar/v1
```

This provides:
- **Interactive API explorer** (try endpoints directly in browser)
- **Request/response examples**
- **Schema documentation**
- **Code generation** (TypeScript, Python, C# clients)

![Scalar Documentation](https://scalar.com/images/scalar-api-reference.png)

---

## Local Development Setup

### Option 1: Visual Studio 2022

1. Open `Maliev.CurrencyService.sln`
2. Set `Maliev.CurrencyService.Api` as startup project
3. Press **F5** to run with debugger

### Option 2: VS Code

1. Open folder in VS Code
2. Install **C# Dev Kit** extension
3. Press **F5** or run:
   ```bash
   dotnet run --project Maliev.CurrencyService.Api
   ```

### Option 3: JetBrains Rider

1. Open `Maliev.CurrencyService.sln`
2. Right-click `Maliev.CurrencyService.Api` → **Run**

---

## Configuration

### Environment Variables

The service uses `appsettings.json` and `appsettings.Development.json` for configuration.

**Override via environment variables**:

```bash
# Database connection
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=currency_app_db;Username=currency_app_user;Password=dev_password"

# Redis cache
export Redis__Configuration="localhost:6379"

# External providers
export ExchangeRateProviders__Fawazahmed__BaseUrl="https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1"
export ExchangeRateProviders__Frankfurter__BaseUrl="https://api.frankfurter.app"

# Logging
export Serilog__MinimumLevel__Default="Information"

# Run the service
dotnet run --project Maliev.CurrencyService.Api
```

### appsettings.Development.json

**Default development settings**:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=currency_app_db;Username=currency_app_user;Password=dev_password"
  },
  "Redis": {
    "Configuration": "localhost:6379",
    "InstanceName": "CurrencyService:"
  },
  "ExchangeRateProviders": {
    "Fawazahmed": {
      "BaseUrl": "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1",
      "TimeoutSeconds": 5
    },
    "Frankfurter": {
      "BaseUrl": "https://api.frankfurter.app",
      "TimeoutSeconds": 5
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Information",
        "System": "Warning"
      }
    }
  }
}
```

---

## Common Tasks

### 1. Seed Test Data

**Seed currencies** (already done via migration):
```bash
cd Maliev.CurrencyService.Data
dotnet ef database update
```

**Verify seed data**:
```sql
SELECT COUNT(*) FROM currencies;
-- Should return 170
```

### 2. Reset Database

**Drop and recreate**:
```bash
cd Maliev.CurrencyService.Data
dotnet ef database drop --force
dotnet ef database update
```

### 3. Run Tests

**Run all tests**:
```bash
cd Maliev.CurrencyService.Tests
dotnet test
```

**Run specific test class**:
```bash
dotnet test --filter "FullyQualifiedName~UserStory1_CurrencyMetadataLookupTests"
```

**Run tests with coverage**:
```bash
dotnet test --collect:"XPlat Code Coverage"
```

### 4. Watch Mode (Auto-restart on code changes)

```bash
cd Maliev.CurrencyService.Api
dotnet watch run
```

### 5. Build Docker Image

```bash
docker build -t currency-service:dev .
docker run -p 5000:8080 currency-service:dev
```

---

## Sample API Requests

### User Story 1: Currency Metadata Lookup

**List all currencies (paginated)**:
```bash
curl -X GET "http://localhost:5000/currencies/v1/currencies?page=1&pageSize=20&isActive=true"
```

**Get currency by code**:
```bash
curl -X GET "http://localhost:5000/currencies/v1/currencies/code/USD"
```

**Get currency by country**:
```bash
curl -X GET "http://localhost:5000/currencies/v1/currencies/country/TH"
```

**Response**:
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "code": "THB",
  "symbol": "฿",
  "name": "Thai Baht",
  "decimalPlaces": 2,
  "isActive": true,
  "isPrimary": false,
  "createdAt": "2025-01-01T00:00:00Z",
  "updatedAt": "2025-01-01T00:00:00Z"
}
```

---

### User Story 2: Live Exchange Rate Retrieval

**Get live rate**:
```bash
curl -X GET "http://localhost:5000/currencies/v1/rates?from=USD&to=THB&mode=live"
```

**Response**:
```json
{
  "fromCurrency": "USD",
  "toCurrency": "THB",
  "rate": 33.567890,
  "mode": "live",
  "provider": "Fawazahmed",
  "isTransitive": false,
  "intermediateCurrency": null,
  "fetchedAt": "2025-11-17T10:00:00Z",
  "expiresAt": "2025-11-17T10:05:00Z",
  "cacheStatus": "miss"
}
```

**Get transitive rate** (via intermediary currency):
```bash
curl -X GET "http://localhost:5000/currencies/v1/rates?from=THB&to=EUR&mode=live"
```

**Response**:
```json
{
  "fromCurrency": "THB",
  "toCurrency": "EUR",
  "rate": 0.026123,
  "mode": "live",
  "provider": "Cached",
  "isTransitive": true,
  "intermediateCurrency": "USD",
  "fetchedAt": "2025-11-17T10:00:00Z",
  "expiresAt": "2025-11-17T10:05:00Z",
  "cacheStatus": "hit"
}
```

---

### User Story 3: Snapshot Exchange Rate Query

**First, ingest test snapshots** (requires Admin JWT token):
```bash
curl -X POST "http://localhost:5000/currencies/v1/admin/snapshots/ingest?dryRun=false" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "snapshots": [
      {
        "from": "USD",
        "to": "EUR",
        "rate": 0.876543,
        "timestamp": "2025-11-16T00:00:00Z"
      },
      {
        "from": "USD",
        "to": "GBP",
        "rate": 0.765432,
        "timestamp": "2025-11-16T00:00:00Z"
      }
    ],
    "source": "Test Data"
  }'
```

**Then query snapshot**:
```bash
curl -X GET "http://localhost:5000/currencies/v1/rates?from=USD&to=EUR&mode=snapshot&date=2025-11-16"
```

**Response**:
```json
{
  "fromCurrency": "USD",
  "toCurrency": "EUR",
  "rate": 0.876543,
  "mode": "snapshot",
  "snapshotDate": "2025-11-16",
  "source": "Test Data",
  "isTransitive": false,
  "intermediateCurrency": null,
  "fetchedAt": "2025-11-17T00:05:00Z",
  "cacheStatus": "hit"
}
```

---

### User Story 5: Currency Metadata Management (Admin)

**Create currency** (requires Admin JWT):
```bash
curl -X POST "http://localhost:5000/currencies/v1/currencies" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "code": "BTC",
    "symbol": "₿",
    "name": "Bitcoin",
    "decimalPlaces": 8,
    "isActive": true,
    "isPrimary": false
  }'
```

**Update currency** (requires Admin JWT + If-Match header):
```bash
curl -X PUT "http://localhost:5000/currencies/v1/currencies/{id}" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -H "If-Match: \"d8f3a7b2c1e4f5a6\"" \
  -d '{
    "name": "Bitcoin (Updated)",
    "isActive": false
  }'
```

---

## Authentication (For Admin Endpoints)

### Generate Test JWT Token

For local development, you can generate a test JWT token:

**Using online tool**:
1. Go to https://jwt.io
2. Payload:
   ```json
   {
     "sub": "test-admin@maliev.com",
     "email": "test-admin@maliev.com",
     "role": "Admin",
     "iat": 1700222400,
     "exp": 1732758400
   }
   ```
3. Secret: `dev-secret-key-for-testing-only`
4. Copy the generated token

**Use in requests**:
```bash
export JWT_TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

curl -X POST "http://localhost:5000/currencies/v1/admin/snapshots/ingest" \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ ... }'
```

**Note**: For production, use a proper identity provider (e.g., Auth0, Azure AD, Google Identity).

---

## Health Checks

**Liveness check** (is the container running?):
```bash
curl http://localhost:5000/liveness
```

**Response (200 OK)**:
```json
{
  "status": "Healthy",
  "timestamp": "2025-11-17T10:00:00Z"
}
```

**Readiness check** (are dependencies ready?):
```bash
curl http://localhost:5000/readiness
```

**Response (200 OK)**:
```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "redis": "Healthy",
    "memory": "Healthy"
  },
  "timestamp": "2025-11-17T10:00:00Z"
}
```

---

## Metrics (Prometheus)

**Prometheus metrics endpoint**:
```bash
curl http://localhost:5000/metrics
```

**Sample metrics**:
```
# HELP currency_http_requests_total Total HTTP requests
# TYPE currency_http_requests_total counter
currency_http_requests_total{method="GET",path="/currencies",status="200"} 1234

# HELP currency_cache_operations_total Total cache operations
# TYPE currency_cache_operations_total counter
currency_cache_operations_total{tier="L1",operation="get",result="hit"} 5678

# HELP currency_provider_latency_seconds Provider latency
# TYPE currency_provider_latency_seconds histogram
currency_provider_latency_seconds_bucket{provider="Fawazahmed",le="0.5"} 120
currency_provider_latency_seconds_bucket{provider="Fawazahmed",le="1.0"} 150
```

---

## Troubleshooting

### Issue: Port 5432 already in use

**Solution**: Stop existing PostgreSQL instance or change port in `docker-compose.yml`:
```yaml
services:
  postgres:
    ports:
      - "5433:5432"  # Use port 5433 instead
```

Then update `appsettings.Development.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5433;..."
}
```

---

### Issue: Cannot connect to database

**Check PostgreSQL is running**:
```bash
docker ps | grep postgres
```

**Check connection string**:
```bash
dotnet ef database update --project Maliev.CurrencyService.Data --verbose
```

**Manual connection test**:
```bash
docker exec -it currency-db psql -U currency_app_user -d currency_app_db
```

---

### Issue: Redis connection failed

**Check Redis is running**:
```bash
docker ps | grep redis
```

**Test Redis connection**:
```bash
docker exec -it currency-cache redis-cli
> PING
PONG
```

---

### Issue: External provider timeout

**Check internet connection**:
```bash
curl https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/usd.json
```

**Increase timeout** in `appsettings.Development.json`:
```json
"ExchangeRateProviders": {
  "Fawazahmed": {
    "TimeoutSeconds": 10  // Increase from 5 to 10
  }
}
```

---

## Next Steps

1. **Explore API Documentation**: http://localhost:5000/scalar/v1
2. **Review User Stories**: See `specs/001-currency-service/spec.md`
3. **Run Full Test Suite**: `dotnet test`
4. **Read Technical Plan**: See `specs/001-currency-service/plan.md`
5. **Check API Contracts**: See `specs/001-currency-service/contracts/`

---

## Production Deployment

For production deployment to Kubernetes:

1. **Build Docker image**:
   ```bash
   docker build -t gcr.io/maliev-prod/currency-service:v1.0 .
   docker push gcr.io/maliev-prod/currency-service:v1.0
   ```

2. **Apply Kubernetes manifests**:
   ```bash
   kubectl apply -f k8s/deployment.yaml
   kubectl apply -f k8s/service.yaml
   ```

3. **Configure secrets**:
   ```bash
   kubectl create secret generic currency-db-secret \
     --from-literal=connection-string='Host=...'
   ```

4. **Monitor deployment**:
   ```bash
   kubectl get pods -w
   kubectl logs -f deployment/currency-service
   ```

---

## Support

For questions or issues:
- **GitHub Issues**: https://github.com/MALIEV-Co-Ltd/Maliev.CurrencyService/issues
- **Documentation**: `specs/001-currency-service/`
- **Email**: support@maliev.com

---

**Quickstart Guide Version**: 1.0
**Last Updated**: 2025-11-17
