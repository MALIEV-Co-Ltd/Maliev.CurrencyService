# IAM Integration API Contract

## Registration Endpoints

These endpoints are called by the CurrencyService during its startup phase.

### Register Permissions
`POST /iam/v1/permissions/register`

**Request Body**:
```json
{
  "serviceName": "CurrencyService",
  "permissions": [
    {
      "permissionId": "currency.currencies.read",
      "description": "Read currency details"
    }
  ]
}
```

### Register Roles
`POST /iam/v1/roles/register`

**Request Body**:
```json
{
  "serviceName": "CurrencyService",
  "roles": [
    {
      "roleId": "currency-admin",
      "roleName": "Currency Administrator",
      "description": "Full control over currency and rate data",
      "permissions": ["currency.currencies.read", "..."]
    }
  ]
}
```

## Authentication

### JWT Token Structure
The service expects a JWT with the following custom claim:

```json
{
  "sub": "user-uuid",
  "permissions": [
    "currency.currencies.create",
    "currency.rates.update"
  ],
  "iss": "iam-service",
  "exp": 1734850000
}
```
