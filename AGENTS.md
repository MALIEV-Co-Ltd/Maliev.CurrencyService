# Maliev.CurrencyService Agent Guidelines

This document provides essential information for agentic coding assistants working on the Maliev.CurrencyService codebase.

## 1. Project Overview

- **Framework**: .NET 10.0
- **Architecture**: Microservice (Aspire-based) with Layered Architecture (Api, Data, Tests).
- **Core Dependencies**:
    - **Data**: Entity Framework Core (PostgreSQL), StackExchange.Redis.
    - **Messaging**: MassTransit (RabbitMQ).
    - **Observability**: OpenTelemetry, HealthChecks.
    - **Resilience**: Polly.

## 2. Build & Test Commands

### Build
To build the entire solution:
```bash
dotnet build Maliev.CurrencyService.slnx
```

### Test
**Important**: Tests require Docker to be running (uses Testcontainers for Postgres, Redis, RabbitMQ).
To run all tests:
```bash
dotnet test Maliev.CurrencyService.slnx
```

To run a specific test project:
```bash
dotnet test Maliev.CurrencyService.Tests/Maliev.CurrencyService.Tests.csproj
```

To run a single test class:
```bash
dotnet test Maliev.CurrencyService.Tests/Maliev.CurrencyService.Tests.csproj --filter "FullyQualifiedName~Maliev.CurrencyService.Tests.Services.CurrencyServiceTests"
```

### Run
To run the API project:
```bash
dotnet run --project Maliev.CurrencyService.Api/Maliev.CurrencyService.Api.csproj
```

## 3. Code Style & Conventions

### General
- **Formatting**: Follow standard C# conventions. `ImplicitUsings` and `Nullable` are enabled.
- **Namespaces**: Use **file-scoped namespaces** (e.g., `namespace Maliev.CurrencyService.Api.Services;`).
- **Constructors**: Use explicit constructor injection (not Primary Constructors yet).

### Naming
- **Classes/Methods**: `PascalCase`
- **Parameters/Locals**: `camelCase`
- **Private Fields**: `_camelCase` (e.g., `_currencyService`)
- **Interfaces**: Prefix with `I` (e.g., `ICurrencyService`)

### Error Handling
- **Exceptions**: Use standard exceptions (`InvalidOperationException`, `ArgumentException`) for logic errors.
- **API Responses**:
    - Success: Return `Ok(result)`, `Created(...)`, or `NoContent()`.
    - Errors: Return `ErrorResponse` object (Status 400/404/500).
    - **Do not** return raw strings or exceptions to the client. Use `ErrorResponse`.
- **Concurrency**: Handle `DbUpdateConcurrencyException` for 412/409 responses using ETags (`If-Match`).

### Logging
- Inject `ILogger<T>`.
- Use structured logging (e.g., `_logger.LogInformation("Getting currency {Id}", id)`).
- Use `LoggerMessage` source generators for high-volume logs (see `Program.cs` for examples) if applicable.

### Async/Await
- Always use `async/await` for I/O bound operations.
- Pass `CancellationToken` to all async methods and EF Core calls.

### Attributes & Metadata
- **Controllers**: Decorate with `[ApiController]`, `[ApiVersion]`, `[Route]`.
- **Documentation**: All public APIs and Controllers **must** have XML documentation (`/// <summary>`) for Swagger/OpenAPI generation.
- **Authorization**: Use `[Authorize]` and `[RequirePermission(...)]` for protected endpoints.

## 4. Database & Data Access
- **EF Core**: Use `CurrencyDbContext`.
- **Queries**: Use `AsNoTracking()` for read-only queries to improve performance.
- **Migrations**: 
  - Database migrations run on startup (`app.MigrateDatabaseAsync`).
  - **Never** add `Microsoft.EntityFrameworkCore.Design` to the Api project. It must only be in the Infrastructure project where migrations live.
  - To run migrations, use `--startup-project` pointing to **Infrastructure** project.

## 5. File System Operations
- **Paths**: Always use **absolute paths** when using tools.
- **Modifications**: Verify existence of files before editing. Use `grep`/`glob` to find usages before refactoring.


## Git & Version Control — Mandatory Rules

### 🚨 CRITICAL: Always Commit Code Changes (Non-Negotiable)
- **You MUST commit your changes to the local repository after completing any meaningful unit of work.**
- **Never accumulate uncommitted changes.** Do not wait until end of session or until something breaks.
- **Commit early and often** — if a change is meaningful (even a small fix or refactor), commit it.
- **You do NOT need to push to remote** — local commits are sufficient to protect against accidental loss.
- **If you are unsure whether to commit, commit anyway.** Extra commits are harmless; lost work is irreversible.
- This rule applies even if you are just "testing" or "exploring" — use git branches to isolate experimental work and commit those changes too.

### 🚨 CRITICAL: Never Use `git checkout` to Restore Broken Files
- **NEVER use `git checkout` to restore or recover files.** This operation discards uncommitted changes permanently and will result in data loss.
- **To undo/recover from broken files: first commit your current changes, then use `git revert` or `git reset --soft` to safely undo.**

## Database & EF Core — Mandatory Rules

### EF Core Design Package
- ❌ `Microsoft.EntityFrameworkCore.Design` MUST NOT be in Api projects
- ✅ It belongs ONLY in the Infrastructure (or Data) project where migrations live
- Migration commands must target Infrastructure as both project and startup-project (since EF Core Design package is in Infrastructure):
  ```
  dotnet ef migrations add <Name> --project Maliev.<Domain>Service.Infrastructure --startup-project Maliev.<Domain>Service.Infrastructure
  ```

### PostgreSQL xmin Concurrency — Mandatory Pattern
Use shadow property ONLY. Never add a Xmin/xmin property to domain entities.
```csharp
entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
```
- ❌ Never use `UseXminAsConcurrencyToken()` (removed in Npgsql EF v7)
- ❌ Never use entity property `public uint Xmin { get; set; }` or `public uint xmin { get; set; }`
- ❌ Never use `.Ignore(e => e.Xmin)` — remove the entity property instead
