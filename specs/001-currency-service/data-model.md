# Phase 1: Data Model Design

**Feature**: Currency WebAPI Service
**Branch**: `001-currency-service`
**Date**: 2025-11-17
**Purpose**: Document entity relationships, database schema, and data constraints

## Overview

The Currency WebAPI Service uses a relational data model implemented with Entity Framework Core 9.0.10 and PostgreSQL 18. The schema supports currency metadata management, live rate caching, historical snapshots, and batch ingestion workflows.

**Design Principles**:
- **Immutability**: Snapshots are write-once, read-many (no updates)
- **Optimistic Concurrency**: Currency entity uses `RowVersion` for conflict detection
- **Temporal Data**: Snapshots use `DateOnly` for precise date queries
- **Performance**: Strategic indexes on foreign keys, composite keys, and date ranges
- **Staging Pattern**: StagedSnapshot enables dry-run validation before commit

---

## Entity Relationship Diagram

```text
┌─────────────────┐          ┌──────────────────────┐
│   Currency      │          │  CountryCurrency     │
├─────────────────┤          ├──────────────────────┤
│ Id (PK)         │◄────────○│ CurrencyCode (FK)    │
│ Code (UQ)       │          │ CountryIso2 (UQ)     │
│ Symbol          │          │ CountryIso3 (UQ)     │
│ Name            │          │ IsPrimary            │
│ DecimalPlaces   │          └──────────────────────┘
│ IsActive        │
│ IsPrimary       │
│ Version         │          ┌──────────────────────┐
│ CreatedAt       │          │  ExchangeRate        │
│ UpdatedAt       │          ├──────────────────────┤
└─────────────────┘          │ Id (PK)              │
                             │ FromCurrency         │
                             │ ToCurrency           │
                             │ Rate                 │
                             │ Provider             │
                             │ IsTransitive         │
                             │ IntermediateCurrency │
                             │ FetchedAt            │
                             │ ExpiresAt (IX)       │
                             │ CreatedAt            │
                             │ UpdatedAt            │
                             └──────────────────────┘
                             (UQ: FromCurrency + ToCurrency + FetchedAt)

┌──────────────────────┐     ┌──────────────────────┐
│  RateSnapshot        │     │  StagedSnapshot      │
├──────────────────────┤     ├──────────────────────┤
│ Id (PK)              │     │ Id (PK)              │
│ BatchId (IX)         │     │ BatchId (IX)         │
│ FromCurrency         │     │ FromCurrency         │
│ ToCurrency           │     │ ToCurrency           │
│ Rate                 │     │ Rate                 │
│ SnapshotDate (IX)    │     │ SnapshotDate         │
│ Source               │     │ Status (IX)          │
│ CreatedAt            │     │ ValidationError      │
└──────────────────────┘     │ CreatedAt            │
(UQ: FromCurrency +          └──────────────────────┘
     ToCurrency +
     SnapshotDate)
```

**Legend**:
- `(PK)` = Primary Key
- `(FK)` = Foreign Key
- `(UQ)` = Unique Constraint
- `(IX)` = Non-unique Index
- `◄────○` = One-to-Many Relationship

---

## Entity Definitions

### 1. Currency

**Purpose**: Stores global currency metadata per ISO 4217 standard

**Table**: `currencies`

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | UUID | PRIMARY KEY | Unique identifier |
| `code` | VARCHAR(3) | NOT NULL, UNIQUE | ISO 4217 code (e.g., USD, EUR) |
| `symbol` | VARCHAR(10) | NOT NULL | Display symbol (e.g., $, €, ¥) |
| `name` | VARCHAR(100) | NOT NULL | Full name (e.g., "US Dollar") |
| `decimal_places` | INT | NOT NULL, DEFAULT 2 | Precision (0 for JPY, 2 for USD) |
| `is_active` | BOOLEAN | NOT NULL, DEFAULT TRUE | Tradeable status |
| `is_primary` | BOOLEAN | NOT NULL, DEFAULT FALSE | Major currency flag (USD, EUR, GBP, JPY) |
| `created_at` | TIMESTAMP | NOT NULL | Creation timestamp |
| `updated_at` | TIMESTAMP | NOT NULL | Last modification timestamp |
| `version` | BYTEA | TIMESTAMP | Optimistic concurrency token (xmin) |

**Indexes**:
- `pk_currencies` (PRIMARY KEY on `id`)
- `ix_currencies_code` (UNIQUE on `code`)

**Business Rules**:
- `code` must match regex `^[A-Z]{3}$`
- `is_primary` = TRUE for top 20 traded currencies (pre-seeded)
- `version` enables optimistic locking via `[Timestamp]` attribute

**Example Row**:
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "code": "USD",
  "symbol": "$",
  "name": "US Dollar",
  "decimal_places": 2,
  "is_active": true,
  "is_primary": true,
  "created_at": "2025-01-01T00:00:00Z",
  "updated_at": "2025-01-01T00:00:00Z",
  "version": [0x00, 0x00, 0x00, 0x01]
}
```

---

### 2. CountryCurrency

**Purpose**: Maps countries to their primary currencies (supports geo-based lookups)

**Table**: `country_currencies`

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | UUID | PRIMARY KEY | Unique identifier |
| `country_iso2` | VARCHAR(2) | NOT NULL | ISO 3166-1 alpha-2 (e.g., TH, US) |
| `country_iso3` | VARCHAR(3) | NOT NULL | ISO 3166-1 alpha-3 (e.g., THA, USA) |
| `currency_code` | VARCHAR(3) | NOT NULL, FOREIGN KEY → `currencies.code` | Associated currency |
| `is_primary` | BOOLEAN | NOT NULL, DEFAULT TRUE | Primary currency flag |
| `created_at` | TIMESTAMP | NOT NULL | Creation timestamp |

**Indexes**:
- `pk_country_currencies` (PRIMARY KEY on `id`)
- `ix_country_iso2_currency` (UNIQUE on `country_iso2` + `currency_code`)
- `ix_country_iso3_currency` (UNIQUE on `country_iso3` + `currency_code`)

**Relationships**:
- Many-to-One with `Currency` via `currency_code`

**Business Rules**:
- Eurozone countries (DE, FR, IT, etc.) all map to EUR
- Some countries have multiple currencies (e.g., Switzerland: CHF, EUR)
- `is_primary` = TRUE for the official currency, FALSE for secondary

**Example Row**:
```json
{
  "id": "223e4567-e89b-12d3-a456-426614174001",
  "country_iso2": "TH",
  "country_iso3": "THA",
  "currency_code": "THB",
  "is_primary": true,
  "created_at": "2025-01-01T00:00:00Z"
}
```

---

### 3. ExchangeRate

**Purpose**: Caches live exchange rates from external providers (Fawazahmed, Frankfurter)

**Table**: `exchange_rates`

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | UUID | PRIMARY KEY | Unique identifier |
| `from_currency` | VARCHAR(3) | NOT NULL | Source currency code |
| `to_currency` | VARCHAR(3) | NOT NULL | Target currency code |
| `rate` | NUMERIC(18,6) | NOT NULL | Exchange rate (6 decimal precision) |
| `provider` | VARCHAR(50) | NOT NULL | Provider name (Fawazahmed, Frankfurter, Cached) |
| `is_transitive` | BOOLEAN | NOT NULL, DEFAULT FALSE | Calculated via intermediary |
| `intermediate_currency` | VARCHAR(3) | NULL | Intermediary currency (e.g., USD for THB→EUR) |
| `fetched_at` | TIMESTAMP | NOT NULL | Provider fetch timestamp |
| `expires_at` | TIMESTAMP | NOT NULL | Cache expiration (5 minutes nominal) |
| `created_at` | TIMESTAMP | NOT NULL | Creation timestamp |
| `updated_at` | TIMESTAMP | NOT NULL | Last update timestamp |

**Indexes**:
- `pk_exchange_rates` (PRIMARY KEY on `id`)
- `ix_exchange_rates_from_to_fetched` (UNIQUE on `from_currency` + `to_currency` + `fetched_at`)
- `ix_exchange_rates_expires_at` (on `expires_at` for cleanup queries)

**Business Rules**:
- Cache TTL: 5 minutes (normal), 60 minutes (degraded mode when providers fail)
- Transitive rates: If USD→THB and USD→EUR exist, calculate THB→EUR via USD
- `intermediate_currency` populated only when `is_transitive = TRUE`
- Cleanup: Delete rows where `expires_at < NOW()` (background service runs daily)

**Example Row (Direct Rate)**:
```json
{
  "id": "323e4567-e89b-12d3-a456-426614174002",
  "from_currency": "USD",
  "to_currency": "THB",
  "rate": 33.567890,
  "provider": "Fawazahmed",
  "is_transitive": false,
  "intermediate_currency": null,
  "fetched_at": "2025-11-17T10:00:00Z",
  "expires_at": "2025-11-17T10:05:00Z",
  "created_at": "2025-11-17T10:00:00Z",
  "updated_at": "2025-11-17T10:00:00Z"
}
```

**Example Row (Transitive Rate)**:
```json
{
  "id": "423e4567-e89b-12d3-a456-426614174003",
  "from_currency": "THB",
  "to_currency": "EUR",
  "rate": 0.026123,
  "provider": "Cached",
  "is_transitive": true,
  "intermediate_currency": "USD",
  "fetched_at": "2025-11-17T10:00:00Z",
  "expires_at": "2025-11-17T10:05:00Z",
  "created_at": "2025-11-17T10:00:00Z",
  "updated_at": "2025-11-17T10:00:00Z"
}
```

**Transitive Rate Calculation**:
```
Rate(THB→EUR) = Rate(THB→USD) × Rate(USD→EUR)
              = (1 / Rate(USD→THB)) × Rate(USD→EUR)
              = (1 / 33.567890) × 0.876543
              = 0.026123
```

---

### 4. RateSnapshot

**Purpose**: Stores historical point-in-time exchange rates for compliance and reporting

**Table**: `rate_snapshots`

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | UUID | PRIMARY KEY | Unique identifier |
| `batch_id` | UUID | NOT NULL | Batch ingestion group identifier |
| `from_currency` | VARCHAR(3) | NOT NULL | Source currency code |
| `to_currency` | VARCHAR(3) | NOT NULL | Target currency code |
| `rate` | NUMERIC(18,6) | NOT NULL | Exchange rate |
| `snapshot_date` | DATE | NOT NULL | Business date of snapshot |
| `source` | VARCHAR(100) | NULL | Data source (e.g., "ECB Daily", "Manual Upload") |
| `created_at` | TIMESTAMP | NOT NULL | Ingestion timestamp |

**Indexes**:
- `pk_rate_snapshots` (PRIMARY KEY on `id`)
- `ix_rate_snapshots_from_to_date` (UNIQUE on `from_currency` + `to_currency` + `snapshot_date`)
- `ix_rate_snapshots_batch_id` (on `batch_id` for batch queries)
- `ix_rate_snapshots_snapshot_date` (on `snapshot_date` for time-range queries)

**Business Rules**:
- **Immutability**: No UPDATE or DELETE operations (insert-only)
- **Uniqueness**: One rate per currency pair per date (prevents duplicates)
- **Retention**: 12-month rolling window (background cleanup service)
- **Partitioning** (future): Monthly partitions on `snapshot_date` for >1M rows

**Example Row**:
```json
{
  "id": "523e4567-e89b-12d3-a456-426614174004",
  "batch_id": "623e4567-e89b-12d3-a456-426614174005",
  "from_currency": "USD",
  "to_currency": "EUR",
  "rate": 0.876543,
  "snapshot_date": "2025-11-16",
  "source": "ECB Daily Rates",
  "created_at": "2025-11-17T00:05:00Z"
}
```

**Query Example (Snapshot Lookup)**:
```sql
SELECT rate
FROM rate_snapshots
WHERE from_currency = 'USD'
  AND to_currency = 'EUR'
  AND snapshot_date = '2025-11-16';
```

---

### 5. StagedSnapshot

**Purpose**: Temporary staging table for batch validation and dry-run processing

**Table**: `staged_snapshots`

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | UUID | PRIMARY KEY | Unique identifier |
| `batch_id` | UUID | NOT NULL | Batch processing group |
| `from_currency` | VARCHAR(3) | NOT NULL | Source currency code |
| `to_currency` | VARCHAR(3) | NOT NULL | Target currency code |
| `rate` | NUMERIC(18,6) | NOT NULL | Exchange rate |
| `snapshot_date` | DATE | NOT NULL | Business date |
| `status` | VARCHAR(20) | NOT NULL, DEFAULT 'Pending' | Validation status |
| `validation_error` | TEXT | NULL | Error message if status = Rejected |
| `created_at` | TIMESTAMP | NOT NULL | Staging timestamp |

**Indexes**:
- `pk_staged_snapshots` (PRIMARY KEY on `id`)
- `ix_staged_snapshots_batch_id` (on `batch_id` for batch operations)
- `ix_staged_snapshots_status` (on `status` for filtered queries)

**Status Values**:
- `Pending`: Initial state, awaiting validation
- `Validated`: Passed all checks, ready for commit
- `Rejected`: Failed validation (duplicate, invalid currency, etc.)
- `Committed`: Successfully moved to `rate_snapshots` table

**Business Rules**:
- **Lifecycle**: Pending → Validated/Rejected → Committed/Deleted
- **Dry-Run Mode**: Validation runs without committing to `rate_snapshots`
- **Cleanup**: Delete all rows with `status = Committed` after successful batch
- **Retention**: Delete `Rejected` rows after 30 days (audit trail)

**Validation Checks**:
1. Currency codes exist in `currencies` table (`is_active = TRUE`)
2. No duplicate (from_currency, to_currency, snapshot_date) within batch
3. Rate > 0 and <= 1,000,000 (sanity check for data entry errors)
4. Snapshot date not in future
5. No conflict with existing `rate_snapshots` (unless overwrite flag set)

**Example Row (Validated)**:
```json
{
  "id": "723e4567-e89b-12d3-a456-426614174006",
  "batch_id": "823e4567-e89b-12d3-a456-426614174007",
  "from_currency": "GBP",
  "to_currency": "USD",
  "rate": 1.234567,
  "snapshot_date": "2025-11-16",
  "status": "Validated",
  "validation_error": null,
  "created_at": "2025-11-17T09:00:00Z"
}
```

**Example Row (Rejected)**:
```json
{
  "id": "923e4567-e89b-12d3-a456-426614174008",
  "batch_id": "823e4567-e89b-12d3-a456-426614174007",
  "from_currency": "USD",
  "to_currency": "XXX",
  "rate": 0.999999,
  "snapshot_date": "2025-11-16",
  "status": "Rejected",
  "validation_error": "Invalid currency code: XXX does not exist in currencies table",
  "created_at": "2025-11-17T09:00:01Z"
}
```

---

## Database Constraints

### Foreign Keys

```sql
-- CountryCurrency → Currency
ALTER TABLE country_currencies
ADD CONSTRAINT fk_country_currencies_currency
FOREIGN KEY (currency_code) REFERENCES currencies(code)
ON DELETE RESTRICT;  -- Prevent deletion of currencies with country mappings
```

**Note**: ExchangeRate and RateSnapshot do NOT have foreign key constraints to `currencies` table. This design choice prevents blocking external provider data due to missing currency records. Validation is handled at application layer instead.

---

### Unique Constraints

```sql
-- Currency: Unique code
CREATE UNIQUE INDEX ix_currencies_code ON currencies(code);

-- CountryCurrency: Unique country-currency mappings
CREATE UNIQUE INDEX ix_country_iso2_currency ON country_currencies(country_iso2, currency_code);
CREATE UNIQUE INDEX ix_country_iso3_currency ON country_currencies(country_iso3, currency_code);

-- ExchangeRate: Unique rate per currency pair per fetch time
CREATE UNIQUE INDEX ix_exchange_rates_from_to_fetched
ON exchange_rates(from_currency, to_currency, fetched_at);

-- RateSnapshot: Unique rate per currency pair per date
CREATE UNIQUE INDEX ix_rate_snapshots_from_to_date
ON rate_snapshots(from_currency, to_currency, snapshot_date);
```

---

### Check Constraints

```sql
-- Currency: Code must be uppercase 3-letter
ALTER TABLE currencies
ADD CONSTRAINT ck_currencies_code_format
CHECK (code ~ '^[A-Z]{3}$');

-- ExchangeRate: Rate must be positive
ALTER TABLE exchange_rates
ADD CONSTRAINT ck_exchange_rates_rate_positive
CHECK (rate > 0);

-- RateSnapshot: Rate must be positive
ALTER TABLE rate_snapshots
ADD CONSTRAINT ck_rate_snapshots_rate_positive
CHECK (rate > 0);

-- StagedSnapshot: Status must be valid enum value
ALTER TABLE staged_snapshots
ADD CONSTRAINT ck_staged_snapshots_status_valid
CHECK (status IN ('Pending', 'Validated', 'Rejected', 'Committed'));
```

---

## EF Core Configuration

### Fluent API Configurations

**CurrencyConfiguration.cs**:
```csharp
builder.Property(c => c.Code)
    .IsRequired()
    .HasMaxLength(3)
    .HasColumnType("varchar(3)");

builder.HasIndex(c => c.Code).IsUnique();

builder.Property(c => c.Version)
    .IsRowVersion();  // Maps to xmin in PostgreSQL
```

**ExchangeRateConfiguration.cs**:
```csharp
builder.HasIndex(e => new { e.FromCurrency, e.ToCurrency, e.FetchedAt })
    .IsUnique();

builder.HasIndex(e => e.ExpiresAt);  // For cleanup queries

builder.Property(e => e.Rate)
    .HasPrecision(18, 6);  // 6 decimal places for accuracy
```

**RateSnapshotConfiguration.cs**:
```csharp
builder.HasIndex(s => new { s.FromCurrency, s.ToCurrency, s.SnapshotDate })
    .IsUnique();

builder.HasIndex(s => s.BatchId);
builder.HasIndex(s => s.SnapshotDate);
```

---

## Migration Strategy

**Initial Migration**: `20251102072848_InitialCreate.cs`

**Commands**:
```bash
# Create migration
dotnet ef migrations add InitialCreate --project Maliev.CurrencyService.Data

# Apply migration
dotnet ef database update --project Maliev.CurrencyService.Data

# Generate SQL script (for production)
dotnet ef migrations script --project Maliev.CurrencyService.Data --output migration.sql
```

**Production Deployment**:
1. Generate SQL script from migration
2. Review script for breaking changes
3. Apply via PostgreSQL client with transaction:
   ```sql
   BEGIN;
   -- Migration SQL here
   COMMIT;
   ```
4. Verify with integration tests against updated schema

---

## Data Seeding

**Seed Data** (via EF Core `OnModelCreating`):

```csharp
modelBuilder.Entity<Currency>().HasData(
    new Currency {
        Id = Guid.Parse("...-USD"),
        Code = "USD",
        Symbol = "$",
        Name = "US Dollar",
        DecimalPlaces = 2,
        IsActive = true,
        IsPrimary = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    },
    new Currency {
        Id = Guid.Parse("...-EUR"),
        Code = "EUR",
        Symbol = "€",
        Name = "Euro",
        DecimalPlaces = 2,
        IsActive = true,
        IsPrimary = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    }
    // ... 168 more currencies
);
```

**Initial Dataset**:
- 170 currencies from ISO 4217 standard
- 250+ country-currency mappings (including Eurozone)
- No pre-seeded exchange rates or snapshots

---

## Performance Considerations

### Index Strategy

| Index | Cardinality | Read Pattern | Write Overhead |
|-------|-------------|--------------|----------------|
| `ix_currencies_code` | High (170 rows) | Every rate query | Low (rare inserts) |
| `ix_exchange_rates_from_to_fetched` | High (20k+ rows) | Cache lookups | Medium (5-min TTL) |
| `ix_exchange_rates_expires_at` | Low (clustered by TTL) | Cleanup job | Medium |
| `ix_rate_snapshots_from_to_date` | Very High (1M+ rows) | Historical queries | Low (insert-only) |
| `ix_rate_snapshots_snapshot_date` | Medium (365 dates/year) | Date range queries | Low |

**Query Optimization**:
- Use `AsNoTracking()` for read-only endpoints (CurrenciesController.Get)
- Enable query splitting to avoid cartesian explosion on joins
- Use `DateOnly` for snapshot queries (avoids timestamp conversions)

### Storage Estimates

| Entity | Row Size | Rows (1 year) | Total Size |
|--------|----------|---------------|------------|
| Currency | ~200 bytes | 170 | 34 KB |
| CountryCurrency | ~100 bytes | 250 | 25 KB |
| ExchangeRate | ~250 bytes | 20,000 | 5 MB |
| RateSnapshot | ~150 bytes | 2,000,000 | 300 MB |
| StagedSnapshot | ~200 bytes | 10,000 (transient) | 2 MB |

**Total Database Size** (1 year): ~310 MB (excluding indexes)

---

## Audit & Compliance

**Audit Interceptor** (`AuditLogInterceptor.cs`):
- Automatically sets `created_at` and `updated_at` timestamps
- Logs all INSERT/UPDATE/DELETE operations to structured logs
- Captures user ID and correlation ID from request context

**Example Audit Log**:
```json
{
  "timestamp": "2025-11-17T10:15:32Z",
  "correlationId": "a1b2c3d4-...",
  "userId": "admin@maliev.com",
  "operation": "INSERT",
  "table": "currencies",
  "primaryKey": "123e4567-...",
  "changes": { "code": "THB", "name": "Thai Baht" }
}
```

**Retention Policies**:
- Audit logs: 90 days (compliance requirement)
- RateSnapshots: 12 months (automatic cleanup)
- StagedSnapshots: 30 days for rejected, immediate for committed

---

**Data Model Completed**: 2025-11-17
**Next Phase**: API Contracts & OpenAPI Specification
