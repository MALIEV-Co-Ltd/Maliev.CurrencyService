# Implementation Plan: IAM Integration Migration

**Branch**: `002-iam-integration` | **Date**: 2025-12-22 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/002-iam-integration/spec.md`

## Summary

Migrate the CurrencyService to a permission-based authorization model integrated with a central IAM service. The service will register 19 granular permissions and 4 predefined roles on startup. Public read-only access for currency lookups and rate conversions will be maintained, while administrative and system operations will be protected by specific permissions.

## Technical Context

**Language/Version**: .NET 10  
**Primary Dependencies**: `Maliev.Aspire.ServiceDefaults` (NuGet), `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.Extensions.Http.Resilience`  
**Storage**: PostgreSQL (via EF Core), Redis (via StackExchange.Redis)  
**Testing**: xUnit, Testcontainers (PostgreSQL, Redis, RabbitMQ)  
**Target Platform**: Linux (Docker)
**Project Type**: ASP.NET Core Web API  
**Performance Goals**: < 100ms P95 for public rate conversion; negligible overhead for permission checks via local JWT inspection.  
**Constraints**: Zero warnings, NO AutoMapper, NO FluentValidation, NO FluentAssertions.  
**Scale/Scope**: 19 permissions, 4 roles, protection across 3 main controllers.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Service Autonomy | PASS | Service manages its own permissions/roles definitions. |
| III. Test-First | PASS | Integration tests using Testcontainers required for Phase 4. |
| IV. Real Infrastructure | PASS | Using real Redis and PostgreSQL for all tests. |
| X. Docker Best Practices | PASS | Dockerfile already inside API project. |
| XIII. .NET Aspire | PASS | Using ServiceDefaults package and standard registration. |
| XIV. Code Quality | PASS | No AutoMapper/FluentValidation/FluentAssertions allowed. |

## Project Structure

### Documentation (this feature)

```text
specs/002-iam-integration/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── checklists/
│   └── requirements.md  # Spec validation
└── contracts/           # Phase 1 output
```

### Source Code (repository root)

```text
Maliev.CurrencyService.Api/
├── Authorization/
│   ├── CurrencyPermissions.cs
│   └── CurrencyPredefinedRoles.cs
├── Controllers/
│   ├── CurrenciesController.cs
│   ├── RatesController.cs
│   └── SnapshotsController.cs
├── Services/
│   └── CurrencyIAMRegistrationService.cs
└── Program.cs

Maliev.CurrencyService.Tests/
├── Integration/
│   └── AuthorizationTests.cs
```

**Structure Decision**: Single project (DEFAULT). Following existing project structure for .NET apps.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |