# API Contract: Currencies Endpoint

**Base Path**: `/currencies/v1/currencies`
**Controller**: `CurrenciesController`
**User Stories**: User Story 1 (Currency Metadata Lookup), User Story 5 (Currency Metadata Management)
**Authentication**: Public (read), Admin role (write)

---

## Endpoints

### 1. List All Currencies (Paginated)

**User Story**: US1 - FR-001

```http
GET /currencies/v1/currencies
```

**Query Parameters**:

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `page` | integer | No | 1 | Page number (1-indexed) |
| `pageSize` | integer | No | 50 | Items per page (1-200) |
| `isActive` | boolean | No | null | Filter by active status |

**Request Example**:
```http
GET /currencies/v1/currencies?page=1&pageSize=20&isActive=true
Accept: application/json
```

**Response (200 OK)**:
```json
{
  "data": [
    {
      "id": "123e4567-e89b-12d3-a456-426614174000",
      "code": "USD",
      "symbol": "$",
      "name": "US Dollar",
      "decimalPlaces": 2,
      "isActive": true,
      "isPrimary": true,
      "createdAt": "2025-01-01T00:00:00Z",
      "updatedAt": "2025-01-01T00:00:00Z"
    },
    {
      "id": "223e4567-e89b-12d3-a456-426614174001",
      "code": "EUR",
      "symbol": "€",
      "name": "Euro",
      "decimalPlaces": 2,
      "isActive": true,
      "isPrimary": true,
      "createdAt": "2025-01-01T00:00:00Z",
      "updatedAt": "2025-01-01T00:00:00Z"
    }
  ],
  "pagination": {
    "currentPage": 1,
    "pageSize": 20,
    "totalPages": 9,
    "totalCount": 170
  }
}
```

**Response Headers**:
```http
X-Correlation-ID: a1b2c3d4-e5f6-7890-abcd-ef1234567890
Cache-Control: public, max-age=300
ETag: "d8f3a7b2c1e4f5a6"
```

**Error Responses**:

- **400 Bad Request**: Invalid pagination parameters
  ```json
  {
    "error": "BadRequest",
    "message": "Page size must be between 1 and 200",
    "timestamp": "2025-11-17T10:00:00Z",
    "correlationId": "a1b2c3d4-..."
  }
  ```

- **429 Too Many Requests**: Rate limit exceeded
  ```json
  {
    "error": "TooManyRequests",
    "message": "Rate limit exceeded. Try again in 60 seconds.",
    "timestamp": "2025-11-17T10:00:00Z",
    "correlationId": "a1b2c3d4-..."
  }
  ```

---

### 2. Get Currency by ID

**User Story**: US1 - FR-002

```http
GET /currencies/v1/currencies/{id}
```

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | UUID | Yes | Currency unique identifier |

**Request Example**:
```http
GET /currencies/v1/currencies/123e4567-e89b-12d3-a456-426614174000
Accept: application/json
```

**Response (200 OK)**:
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "code": "USD",
  "symbol": "$",
  "name": "US Dollar",
  "decimalPlaces": 2,
  "isActive": true,
  "isPrimary": true,
  "createdAt": "2025-01-01T00:00:00Z",
  "updatedAt": "2025-01-01T00:00:00Z"
}
```

**Error Responses**:

- **404 Not Found**: Currency does not exist
  ```json
  {
    "error": "NotFound",
    "message": "Currency with ID 123e4567-e89b-12d3-a456-426614174000 not found",
    "timestamp": "2025-11-17T10:00:00Z",
    "correlationId": "a1b2c3d4-..."
  }
  ```

---

### 3. Get Currency by Code

**User Story**: US1 - FR-003

```http
GET /currencies/v1/currencies/code/{code}
```

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `code` | string | Yes | ISO 4217 currency code (e.g., USD, EUR) |

**Request Example**:
```http
GET /currencies/v1/currencies/code/THB
Accept: application/json
```

**Response (200 OK)**:
```json
{
  "id": "323e4567-e89b-12d3-a456-426614174002",
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

**Error Responses**:

- **404 Not Found**: Currency code does not exist
  ```json
  {
    "error": "NotFound",
    "message": "Currency with code 'XYZ' not found",
    "timestamp": "2025-11-17T10:00:00Z",
    "correlationId": "a1b2c3d4-..."
  }
  ```

---

### 4. Get Currency by Country

**User Story**: US1 - FR-004

```http
GET /currencies/v1/currencies/country/{countryCode}
```

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `countryCode` | string | Yes | ISO 3166-1 alpha-2 (TH, US) or alpha-3 (THA, USA) |

**Request Example**:
```http
GET /currencies/v1/currencies/country/TH
Accept: application/json
```

**Response (200 OK)**:
```json
{
  "id": "323e4567-e89b-12d3-a456-426614174002",
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

**Edge Cases**:
- **Multi-currency countries** (e.g., Switzerland): Returns primary currency (CHF)
- **Eurozone countries** (e.g., Germany, France): Returns EUR

**Error Responses**:

- **404 Not Found**: Country code does not exist
  ```json
  {
    "error": "NotFound",
    "message": "No currency mapping found for country code 'XX'",
    "timestamp": "2025-11-17T10:00:00Z",
    "correlationId": "a1b2c3d4-..."
  }
  ```

---

### 5. Create Currency (Admin Only)

**User Story**: US5 - FR-040

```http
POST /currencies/v1/currencies
```

**Authentication**: Required (Admin role)

**Request Headers**:
```http
Authorization: Bearer <jwt-token>
Content-Type: application/json
```

**Request Body**:
```json
{
  "code": "BTC",
  "symbol": "₿",
  "name": "Bitcoin",
  "decimalPlaces": 8,
  "isActive": true,
  "isPrimary": false
}
```

**Validation Rules**:
- `code`: Required, 3 uppercase letters, unique
- `symbol`: Required, max 10 characters
- `name`: Required, max 100 characters
- `decimalPlaces`: Required, 0-8
- `isActive`: Optional, default true
- `isPrimary`: Optional, default false

**Response (201 Created)**:
```json
{
  "id": "423e4567-e89b-12d3-a456-426614174003",
  "code": "BTC",
  "symbol": "₿",
  "name": "Bitcoin",
  "decimalPlaces": 8,
  "isActive": true,
  "isPrimary": false,
  "createdAt": "2025-11-17T10:00:00Z",
  "updatedAt": "2025-11-17T10:00:00Z"
}
```

**Response Headers**:
```http
Location: /currencies/v1/currencies/423e4567-e89b-12d3-a456-426614174003
```

**Error Responses**:

- **400 Bad Request**: Validation errors
  ```json
  {
    "error": "BadRequest",
    "message": "Validation failed",
    "timestamp": "2025-11-17T10:00:00Z",
    "correlationId": "a1b2c3d4-...",
    "details": {
      "code": ["Currency code must be exactly 3 uppercase letters"],
      "decimalPlaces": ["Decimal places must be between 0 and 8"]
    }
  }
  ```

- **401 Unauthorized**: Missing or invalid JWT token
  ```json
  {
    "error": "Unauthorized",
    "message": "Authentication required",
    "timestamp": "2025-11-17T10:00:00Z",
    "correlationId": "a1b2c3d4-..."
  }
  ```

- **403 Forbidden**: Insufficient permissions
  ```json
  {
    "error": "Forbidden",
    "message": "Admin role required",
    "timestamp": "2025-11-17T10:00:00Z",
    "correlationId": "a1b2c3d4-..."
  }
  ```

- **409 Conflict**: Currency code already exists
  ```json
  {
    "error": "Conflict",
    "message": "Currency with code 'BTC' already exists",
    "timestamp": "2025-11-17T10:00:00Z",
    "correlationId": "a1b2c3d4-..."
  }
  ```

---

### 6. Update Currency (Admin Only)

**User Story**: US5 - FR-041

```http
PUT /currencies/v1/currencies/{id}
```

**Authentication**: Required (Admin role)

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | UUID | Yes | Currency unique identifier |

**Request Headers**:
```http
Authorization: Bearer <jwt-token>
Content-Type: application/json
If-Match: "d8f3a7b2c1e4f5a6"
```

**Request Body**:
```json
{
  "symbol": "₿",
  "name": "Bitcoin (Updated)",
  "decimalPlaces": 8,
  "isActive": false
}
```

**Validation Rules**:
- `code`: Cannot be changed (immutable)
- `symbol`: Optional, max 10 characters
- `name`: Optional, max 100 characters
- `decimalPlaces`: Optional, 0-8
- `isActive`: Optional

**Response (200 OK)**:
```json
{
  "id": "423e4567-e89b-12d3-a456-426614174003",
  "code": "BTC",
  "symbol": "₿",
  "name": "Bitcoin (Updated)",
  "decimalPlaces": 8,
  "isActive": false,
  "isPrimary": false,
  "createdAt": "2025-11-17T10:00:00Z",
  "updatedAt": "2025-11-17T10:15:00Z"
}
```

**Error Responses**:

- **404 Not Found**: Currency does not exist
- **409 Conflict**: Optimistic concurrency violation (stale data)
  ```json
  {
    "error": "Conflict",
    "message": "Currency was modified by another user. Please refresh and try again.",
    "timestamp": "2025-11-17T10:00:00Z",
    "correlationId": "a1b2c3d4-..."
  }
  ```

- **412 Precondition Failed**: Missing or invalid If-Match header
  ```json
  {
    "error": "PreconditionFailed",
    "message": "If-Match header is required for updates",
    "timestamp": "2025-11-17T10:00:00Z",
    "correlationId": "a1b2c3d4-..."
  }
  ```

---

### 7. Delete Currency (Admin Only)

**User Story**: US5 - FR-042

```http
DELETE /currencies/v1/currencies/{id}
```

**Authentication**: Required (Admin role)

**Path Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | UUID | Yes | Currency unique identifier |

**Request Headers**:
```http
Authorization: Bearer <jwt-token>
```

**Response (204 No Content)**:
```http
HTTP/1.1 204 No Content
```

**Error Responses**:

- **404 Not Found**: Currency does not exist
- **409 Conflict**: Currency has dependencies (exchange rates, snapshots)
  ```json
  {
    "error": "Conflict",
    "message": "Cannot delete currency 'USD' because it has active exchange rates or snapshots",
    "timestamp": "2025-11-17T10:00:00Z",
    "correlationId": "a1b2c3d4-..."
  }
  ```

---

## Data Models

### CurrencyResponse

```typescript
{
  id: string (UUID);
  code: string (3 uppercase letters);
  symbol: string (max 10 chars);
  name: string (max 100 chars);
  decimalPlaces: number (0-8);
  isActive: boolean;
  isPrimary: boolean;
  createdAt: string (ISO 8601);
  updatedAt: string (ISO 8601);
}
```

### PaginatedCurrencyResponse

```typescript
{
  data: CurrencyResponse[];
  pagination: {
    currentPage: number;
    pageSize: number;
    totalPages: number;
    totalCount: number;
  };
}
```

### CreateCurrencyRequest

```typescript
{
  code: string (required, 3 uppercase letters);
  symbol: string (required, max 10 chars);
  name: string (required, max 100 chars);
  decimalPlaces: number (required, 0-8);
  isActive?: boolean (optional, default true);
  isPrimary?: boolean (optional, default false);
}
```

### UpdateCurrencyRequest

```typescript
{
  symbol?: string (optional, max 10 chars);
  name?: string (optional, max 100 chars);
  decimalPlaces?: number (optional, 0-8);
  isActive?: boolean (optional);
}
```

---

## Rate Limiting

**Public Endpoints** (GET):
- 100 requests per minute per IP address
- 429 response with `Retry-After` header

**Authenticated Endpoints** (POST/PUT/DELETE):
- 100 requests per minute per user token
- 429 response with `Retry-After` header

---

## Caching

**Cache Headers**:
- `Cache-Control: public, max-age=300` (5 minutes for GET /currencies)
- `ETag` header for conditional requests (`If-None-Match`)
- 304 Not Modified response when ETag matches

**Cache Invalidation**:
- Automatic invalidation on POST/PUT/DELETE operations
- Distributed cache eviction via Redis Pub/Sub

---

**Contract Version**: 1.0
**Last Updated**: 2025-11-17
