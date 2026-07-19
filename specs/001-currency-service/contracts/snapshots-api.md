# API Contract: Snapshots Endpoint

**Base Path**: `/currencies/v1/admin/snapshots`
**Controller**: `SnapshotsController`
**User Story**: User Story 4 (Snapshot Batch Ingestion)
**Authentication**: Required (Admin role via JWT)

---

## Security Requirements

**All endpoints in this contract require**:
- Valid JWT token in `Authorization` header
- User must have `Admin` role claim
- Rate limiting: 100 requests per minute per user

**Authorization Header**:
```http
Authorization: Bearer <jwt-token>
```

---

## Endpoints

### 1. Import Batch Snapshots

**User Story**: US4 - FR-026, FR-027, FR-028

```http
POST /currencies/v1/admin/snapshots/ingest
```

**Authentication**: Required (Admin role)

**Query Parameters**:

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `dryRun` | boolean | No | false | If true, validates without committing to database |

**Request Headers**:
```http
Authorization: Bearer <jwt-token>
Content-Type: application/json
```

**Request Body**:
```json
{
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
    },
    {
      "from": "EUR",
      "to": "GBP",
      "rate": 0.873456,
      "timestamp": "2025-11-16T00:00:00Z"
    }
  ],
  "source": "ECB Daily Rates",
  "overwriteExisting": false
}
```

**Request Body Schema**:
```typescript
{
  snapshots: Array<{
    from: string;           // Required, 3 uppercase letters
    to: string;             // Required, 3 uppercase letters
    rate: number;           // Required, positive number
    timestamp: string;      // Required, ISO 8601 format
  }>;
  source?: string;          // Optional, max 100 characters
  overwriteExisting?: boolean; // Optional, default false
}
```

**Validation Rules**:
- `snapshots` array: Required, 1-10,000 entries
- `from` / `to`: Required, 3 uppercase letters, must exist in currencies table
- `rate`: Required, positive number, max 1,000,000
- `timestamp`: Required, valid ISO 8601, not in future
- No duplicate (from, to, date) combinations within batch
- If `overwriteExisting=false`, no conflicts with existing snapshots

---

### Response (202 Accepted) - Dry Run Mode

**User Story**: US4 - FR-028

```json
{
  "batchId": "823e4567-e89b-12d3-a456-426614174007",
  "dryRun": true,
  "validationReport": {
    "totalEntries": 3,
    "validEntries": 2,
    "invalidEntries": 1,
    "errors": [
      {
        "index": 2,
        "from": "EUR",
        "to": "GBP",
        "timestamp": "2025-11-16T00:00:00Z",
        "error": "Duplicate entry: EUR→GBP on 2025-11-16 already exists in database",
        "severity": "Error"
      }
    ],
    "warnings": [
      {
        "index": 0,
        "from": "USD",
        "to": "EUR",
        "timestamp": "2025-11-16T00:00:00Z",
        "warning": "Rate differs from previous snapshot by >10% (0.876543 vs 0.968421)",
        "severity": "Warning"
      }
    ]
  },
  "recommendation": "Fix 1 error(s) before committing. Set overwriteExisting=true to replace existing snapshots.",
  "timestamp": "2025-11-17T10:00:00Z"
}
```

**Response Headers**:
```http
X-Correlation-ID: a1b2c3d4-...
```

---

### Response (202 Accepted) - Async Processing

**User Story**: US4 - FR-027

```json
{
  "batchId": "923e4567-e89b-12d3-a456-426614174008",
  "dryRun": false,
  "status": "Processing",
  "totalEntries": 10000,
  "message": "Batch ingestion started. Check status at /admin/snapshots/batch/923e4567-e89b-12d3-a456-426614174008",
  "estimatedCompletionTime": "2025-11-17T10:01:30Z",
  "timestamp": "2025-11-17T10:00:00Z"
}
```

**Response Headers**:
```http
Location: /currencies/v1/admin/snapshots/batch/923e4567-e89b-12d3-a456-426614174008
X-Correlation-ID: a1b2c3d4-...
```

**Processing Flow**:
1. Validate all entries → Stage in `staged_snapshots` table
2. If validation passes → Copy to `rate_snapshots` table (bulk insert)
3. Delete staged entries → Mark batch as completed
4. Total time: ~48 seconds for 10,000 entries

---

### Response (200 OK) - Synchronous Small Batch

**User Story**: US4 - FR-026 (small batches <100 entries)

```json
{
  "batchId": "a23e4567-e89b-12d3-a456-426614174009",
  "dryRun": false,
  "status": "Completed",
  "totalEntries": 3,
  "validEntries": 3,
  "invalidEntries": 0,
  "insertedCount": 3,
  "updatedCount": 0,
  "processingTimeMs": 85,
  "timestamp": "2025-11-17T10:00:00Z"
}
```

---

### Error Responses

#### 400 Bad Request - Empty Array

```json
{
  "error": "BadRequest",
  "message": "Snapshot array cannot be empty",
  "timestamp": "2025-11-17T10:00:00Z",
  "correlationId": "a1b2c3d4-..."
}
```

#### 400 Bad Request - Validation Errors

```json
{
  "error": "BadRequest",
  "message": "Validation failed for snapshot entries",
  "timestamp": "2025-11-17T10:00:00Z",
  "correlationId": "a1b2c3d4-...",
  "details": {
    "entry_0": ["Invalid 'from' currency code: must be 3 uppercase letters"],
    "entry_2": ["Rate must be positive"],
    "entry_5": ["Timestamp format invalid: expected ISO 8601"]
  }
}
```

#### 400 Bad Request - Too Many Entries

```json
{
  "error": "BadRequest",
  "message": "Batch size exceeds maximum limit",
  "timestamp": "2025-11-17T10:00:00Z",
  "correlationId": "a1b2c3d4-...",
  "details": {
    "received": 15000,
    "maximum": 10000,
    "suggestion": "Split into multiple batches of 10,000 entries each"
  }
}
```

#### 401 Unauthorized - Missing Token

```json
{
  "error": "Unauthorized",
  "message": "Authentication required",
  "timestamp": "2025-11-17T10:00:00Z",
  "correlationId": "a1b2c3d4-..."
}
```

#### 403 Forbidden - Insufficient Permissions

```json
{
  "error": "Forbidden",
  "message": "Admin role required for snapshot ingestion",
  "timestamp": "2025-11-17T10:00:00Z",
  "correlationId": "a1b2c3d4-..."
}
```

#### 409 Conflict - Duplicate Snapshots

```json
{
  "error": "Conflict",
  "message": "Duplicate snapshots detected in batch",
  "timestamp": "2025-11-17T10:00:00Z",
  "correlationId": "a1b2c3d4-...",
  "details": {
    "duplicates": [
      "USD→EUR on 2025-11-16 (entries 0 and 5)",
      "GBP→JPY on 2025-11-16 (entries 12 and 18)"
    ],
    "suggestion": "Remove duplicates or set overwriteExisting=true"
  }
}
```

#### 500 Internal Server Error - Processing Failure

```json
{
  "error": "InternalServerError",
  "message": "Batch processing failed",
  "timestamp": "2025-11-17T10:00:00Z",
  "correlationId": "a1b2c3d4-...",
  "details": {
    "batchId": "b23e4567-e89b-12d3-a456-426614174010",
    "failedAt": "Database insertion",
    "suggestion": "Check batch status for partial completion"
  }
}
```

---

## 2. Get Batch Status

**User Story**: US4 - FR-027

```http
GET /currencies/v1/admin/snapshots/batch/{batchId}
```

**Authentication**: Required (Admin role)

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `batchId` | UUID | Yes | Batch unique identifier |

**Request Example**:
```http
GET /currencies/v1/admin/snapshots/batch/923e4567-e89b-12d3-a456-426614174008
Authorization: Bearer <jwt-token>
Accept: application/json
```

**Response (200 OK) - Processing**:
```json
{
  "batchId": "923e4567-e89b-12d3-a456-426614174008",
  "status": "Processing",
  "totalEntries": 10000,
  "processedEntries": 6543,
  "validEntries": 6540,
  "invalidEntries": 3,
  "progress": 65.43,
  "startedAt": "2025-11-17T10:00:00Z",
  "estimatedCompletionTime": "2025-11-17T10:01:30Z"
}
```

**Response (200 OK) - Completed**:
```json
{
  "batchId": "923e4567-e89b-12d3-a456-426614174008",
  "status": "Completed",
  "totalEntries": 10000,
  "processedEntries": 10000,
  "validEntries": 9997,
  "invalidEntries": 3,
  "insertedCount": 9850,
  "updatedCount": 147,
  "progress": 100,
  "startedAt": "2025-11-17T10:00:00Z",
  "completedAt": "2025-11-17T10:00:48Z",
  "processingTimeMs": 48000,
  "errors": [
    {
      "index": 1234,
      "from": "USD",
      "to": "XXX",
      "error": "Invalid currency code: XXX does not exist"
    },
    {
      "index": 5678,
      "from": "EUR",
      "to": "GBP",
      "error": "Rate out of range: 0.0 not allowed"
    }
  ]
}
```

**Response (200 OK) - Failed**:
```json
{
  "batchId": "923e4567-e89b-12d3-a456-426614174008",
  "status": "Failed",
  "totalEntries": 10000,
  "processedEntries": 4567,
  "failureReason": "Database connection lost during bulk insert",
  "startedAt": "2025-11-17T10:00:00Z",
  "failedAt": "2025-11-17T10:00:30Z",
  "suggestion": "Check database logs and retry with a new batch"
}
```

**Error Responses**:

- **404 Not Found**: Batch ID does not exist
  ```json
  {
    "error": "NotFound",
    "message": "Batch 923e4567-e89b-12d3-a456-426614174008 not found",
    "timestamp": "2025-11-17T10:00:00Z",
    "correlationId": "a1b2c3d4-..."
  }
  ```

---

## 3. List Recent Batches

**User Story**: US4 - Monitoring

```http
GET /currencies/v1/admin/snapshots/batches
```

**Authentication**: Required (Admin role)

**Query Parameters**:

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `status` | string | No | null | Filter by status: Processing, Completed, Failed |
| `limit` | integer | No | 20 | Number of results (1-100) |

**Request Example**:
```http
GET /currencies/v1/admin/snapshots/batches?status=Completed&limit=10
Authorization: Bearer <jwt-token>
```

**Response (200 OK)**:
```json
{
  "batches": [
    {
      "batchId": "923e4567-e89b-12d3-a456-426614174008",
      "status": "Completed",
      "totalEntries": 10000,
      "validEntries": 9997,
      "invalidEntries": 3,
      "source": "ECB Daily Rates",
      "startedAt": "2025-11-17T10:00:00Z",
      "completedAt": "2025-11-17T10:00:48Z",
      "processingTimeMs": 48000
    },
    {
      "batchId": "a23e4567-e89b-12d3-a456-426614174009",
      "status": "Completed",
      "totalEntries": 150,
      "validEntries": 150,
      "invalidEntries": 0,
      "source": "Manual Upload",
      "startedAt": "2025-11-16T14:30:00Z",
      "completedAt": "2025-11-16T14:30:02Z",
      "processingTimeMs": 2100
    }
  ],
  "totalCount": 2
}
```

---

## 4. Delete Batch (Cleanup)

**User Story**: US4 - FR-029

```http
DELETE /currencies/v1/admin/snapshots/batch/{batchId}
```

**Authentication**: Required (Admin role)

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `batchId` | UUID | Yes | Batch unique identifier |

**Request Example**:
```http
DELETE /currencies/v1/admin/snapshots/batch/923e4567-e89b-12d3-a456-426614174008
Authorization: Bearer <jwt-token>
```

**Response (204 No Content)**:
```http
HTTP/1.1 204 No Content
```

**Notes**:
- Deletes staged snapshots (from `staged_snapshots` table)
- Does NOT delete committed snapshots (from `rate_snapshots` table)
- Only deletes batches with status: Completed, Failed, or Rejected
- Cannot delete batches with status: Processing

**Error Responses**:

- **404 Not Found**: Batch does not exist
- **409 Conflict**: Batch is still processing
  ```json
  {
    "error": "Conflict",
    "message": "Cannot delete batch while processing",
    "timestamp": "2025-11-17T10:00:00Z",
    "correlationId": "a1b2c3d4-...",
    "details": {
      "batchId": "923e4567-e89b-12d3-a456-426614174008",
      "status": "Processing",
      "suggestion": "Wait for batch to complete or fail"
    }
  }
  ```

---

## 5. Promote Staged Snapshots

**User Story**: US4 - FR-029

```http
POST /currencies/v1/admin/snapshots/batch/{batchId}/promote
```

**Authentication**: Required (Admin role)

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `batchId` | UUID | Yes | Batch unique identifier |

**Request Example**:
```http
POST /currencies/v1/admin/snapshots/batch/923e4567-e89b-12d3-a456-426614174008/promote
Authorization: Bearer <jwt-token>
```

**Response (200 OK)**:
```json
{
  "batchId": "923e4567-e89b-12d3-a456-426614174008",
  "promotedCount": 9997,
  "skippedCount": 3,
  "message": "Batch promoted successfully from staging to snapshots table",
  "timestamp": "2025-11-17T10:00:00Z"
}
```

**Notes**:
- Copies validated entries from `staged_snapshots` to `rate_snapshots`
- Only promotes entries with `status = Validated`
- Skips entries with `status = Rejected`
- Atomic operation (all or nothing)

**Error Responses**:

- **400 Bad Request**: Batch not validated
  ```json
  {
    "error": "BadRequest",
    "message": "Batch must be validated before promotion",
    "timestamp": "2025-11-17T10:00:00Z",
    "correlationId": "a1b2c3d4-...",
    "details": {
      "batchId": "923e4567-e89b-12d3-a456-426614174008",
      "currentStatus": "Pending",
      "suggestion": "Run dry-run validation first"
    }
  }
  ```

- **409 Conflict**: Batch already promoted
  ```json
  {
    "error": "Conflict",
    "message": "Batch already promoted",
    "timestamp": "2025-11-17T10:00:00Z",
    "correlationId": "a1b2c3d4-..."
  }
  ```

---

## Data Models

### SnapshotBatchRequest

```typescript
{
  snapshots: Array<SnapshotEntryDto>;
  source?: string;              // Optional, max 100 characters
  overwriteExisting?: boolean;  // Optional, default false
}
```

### SnapshotEntryDto

```typescript
{
  from: string;       // Required, 3 uppercase letters
  to: string;         // Required, 3 uppercase letters
  rate: number;       // Required, positive number
  timestamp: string;  // Required, ISO 8601 format
}
```

### SnapshotBatchResponse

```typescript
{
  batchId: string;                    // UUID
  dryRun: boolean;
  status: "Pending" | "Processing" | "Completed" | "Failed";
  totalEntries: number;
  validEntries?: number;
  invalidEntries?: number;
  insertedCount?: number;             // Only for completed batches
  updatedCount?: number;              // Only for completed batches
  processingTimeMs?: number;
  validationReport?: ValidationReport;
  timestamp: string;
}
```

### ValidationReport

```typescript
{
  totalEntries: number;
  validEntries: number;
  invalidEntries: number;
  errors: Array<{
    index: number;
    from: string;
    to: string;
    timestamp: string;
    error: string;
    severity: "Error" | "Warning";
  }>;
  warnings: Array<{
    index: number;
    from: string;
    to: string;
    timestamp: string;
    warning: string;
    severity: "Warning";
  }>;
}
```

---

## Validation Rules

### Batch Validation

**SnapshotBatchRequestValidator** (FluentValidation):

```csharp
public class SnapshotBatchRequestValidator : AbstractValidator<SnapshotBatchRequest>
{
    public RuleFor(x => x.Snapshots)
        .NotNull().WithMessage("Snapshots array is required")
        .NotEmpty().WithMessage("Snapshots array cannot be empty")
        .Must(s => s.Count <= 10000)
        .WithMessage("Batch size cannot exceed 10,000 entries");

    public RuleForEach(x => x.Snapshots).SetValidator(new SnapshotEntryValidator());

    public RuleFor(x => x.Source)
        .MaximumLength(100)
        .WithMessage("Source description cannot exceed 100 characters");
}
```

### Entry Validation

**SnapshotEntryValidator**:

```csharp
public class SnapshotEntryValidator : AbstractValidator<SnapshotEntryDto>
{
    public RuleFor(x => x.From)
        .NotEmpty().WithMessage("Source currency is required")
        .Length(3).WithMessage("Currency code must be 3 characters")
        .Matches("^[A-Z]{3}$").WithMessage("Currency code must be uppercase");

    public RuleFor(x => x.To)
        .NotEmpty().WithMessage("Target currency is required")
        .Length(3).WithMessage("Currency code must be 3 characters")
        .Matches("^[A-Z]{3}$").WithMessage("Currency code must be uppercase");

    public RuleFor(x => x.Rate)
        .GreaterThan(0).WithMessage("Rate must be positive")
        .LessThanOrEqualTo(1000000).WithMessage("Rate exceeds maximum (1,000,000)");

    public RuleFor(x => x.Timestamp)
        .NotEmpty().WithMessage("Timestamp is required")
        .Must(BeValidDate).WithMessage("Invalid timestamp format")
        .Must(NotBeFutureDate).WithMessage("Timestamp cannot be in the future");
}
```

### Database Validation

**During staging**:
1. Currency codes exist in `currencies` table with `is_active = true`
2. No duplicate (from_currency, to_currency, snapshot_date) within batch
3. No conflict with existing `rate_snapshots` (unless `overwriteExisting = true`)
4. Rate deviation check: Warn if >10% difference from previous snapshot

---

## Performance Characteristics

**User Story**: US4 - FR-030

| Batch Size | Processing Time | Throughput | Method |
|------------|----------------|------------|--------|
| 100 entries | 85ms | 1,176 entries/sec | Synchronous |
| 1,000 entries | 2.1s | 476 entries/sec | Async (background) |
| 10,000 entries | 48s | 208 entries/sec | Async (bulk insert) |

**Optimization Techniques**:
- Bulk INSERT via `AddRange()` and `SaveChangesAsync()`
- Batch validation in parallel (10 threads)
- PostgreSQL `COPY` protocol for >5,000 entries
- Transaction batching (500 entries per transaction)

**Concurrency**:
- Max 5 concurrent batch operations per instance
- Distributed lock (Redis) ensures one instance processes each batch
- Queue-based processing for fairness

---

## Rate Limiting

**Admin Endpoints**:
- 100 requests per minute per user token
- Batch ingestion counts as 1 request regardless of size
- Status checks not counted against rate limit

**Batch Size Limits**:
- Minimum: 1 entry
- Maximum: 10,000 entries per batch
- Recommendation: Split large datasets into 5,000-entry batches

---

## Audit Logging

**All snapshot operations logged**:

```json
{
  "timestamp": "2025-11-17T10:00:00Z",
  "level": "Information",
  "message": "Snapshot batch ingestion started",
  "correlationId": "a1b2c3d4-...",
  "userId": "admin@maliev.com",
  "batchId": "923e4567-e89b-12d3-a456-426614174008",
  "totalEntries": 10000,
  "dryRun": false,
  "source": "ECB Daily Rates"
}
```

**Retention**:
- Audit logs: 90 days
- Staged snapshots (Rejected): 30 days
- Staged snapshots (Committed): Deleted immediately after promotion

---

## Monitoring & Observability

**Prometheus Metrics**:
```
# Batch metrics
currency_snapshot_batches_total{status}
currency_snapshot_batch_duration_seconds{status}
currency_snapshot_batch_size_entries{percentile}

# Validation metrics
currency_snapshot_validation_errors_total{error_type}
currency_snapshot_validation_warnings_total{warning_type}

# Processing metrics
currency_snapshot_processing_throughput_entries_per_second
currency_snapshot_database_operations_total{operation, result}
```

---

**Contract Version**: 1.0
**Last Updated**: 2025-11-17
