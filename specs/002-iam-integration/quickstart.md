# Quickstart: IAM Integration Migration

## Prerequisites

- IAM Service (Running or Mocked)
- Redis (for caching)
- PostgreSQL

## Local Development

### 1. Disable Auth (Default)
To run the service without enforcing permissions (legacy mode):
```json
{
  "Features": {
    "PermissionBasedAuthEnabled": false
  }
}
```

### 2. Enable Auth
To test permission-based authorization:
1. Ensure the IAM service is accessible.
2. Update `appsettings.Development.json`:
```json
{
  "Features": {
    "PermissionBasedAuthEnabled": true
  },
  "ExternalServices": {
    "IAM": {
      "BaseUrl": "http://localhost:8080"
    }
  }
}
```
3. Start the service. It will register its permissions and roles with IAM.

## Running Tests

Integration tests will use `Testcontainers` to spin up a mock IAM if necessary, or verify the logic using `WithTestAuth` extension.

```bash
dotnet test Maliev.CurrencyService.Tests
```

## Verifying Public Access

Public endpoints should be accessible without an `Authorization` header:
```bash
curl http://localhost:8080/currency/v1/currencies
```

## Verifying Protected Access

Admin endpoints should return `401 Unauthorized` without a token and `403 Forbidden` with a token lacking the required permission:
```bash
curl -X POST http://localhost:8080/currency/v1/admin/currencies -d '{"code":"BTC", "name": "Bitcoin", "symbol": "₿"}'
```
