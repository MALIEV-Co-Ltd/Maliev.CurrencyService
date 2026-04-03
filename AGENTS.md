# Maliev.CurrencyService Agent Guidelines

> **Workspace root** `B:\maliev` contains **41 independent git repos**. Each `Maliev.*` folder and `maliev-gitops` is its own repo. There is no single repo at the workspace root. Always work within this service directory (`B:\maliev\Maliev.CurrencyService`).

---

## 1. Project Overview

- **Framework**: .NET 10.0
- **Architecture**: Microservice (Aspire-based) with Clean Architecture (Api, Application, Domain, Infrastructure, Tests).
- **Core Dependencies**:
    - **Data**: Entity Framework Core (PostgreSQL), StackExchange.Redis.
    - **Messaging**: MassTransit (RabbitMQ).
    - **Observability**: OpenTelemetry, HealthChecks.
    - **Resilience**: Polly.

---

## 2. Build, Test & Lint Commands

All commands run from within this service directory (`B:\maliev\Maliev.CurrencyService`).

```powershell
# Build (treats warnings as errors — all must be fixed)
dotnet build Maliev.CurrencyService.slnx

# Run all tests
dotnet test Maliev.CurrencyService.slnx --verbosity normal

# Run a single test method
dotnet test --filter "FullyQualifiedName~CurrencyServiceTests.GetCurrencyById_ReturnsExpectedCurrency"

# Run all tests in a class
dotnet test --filter "FullyQualifiedName~CurrencyServiceTests"

# Run with code coverage
dotnet test Maliev.CurrencyService.slnx --collect:"XPlat Code Coverage"

# Format check
dotnet format Maliev.CurrencyService.slnx

# Run the API project
dotnet run --project Maliev.CurrencyService.Api/Maliev.CurrencyService.Api.csproj

# EF Core migrations (Infrastructure project only)
dotnet ef migrations add <Name> --project Maliev.CurrencyService.Infrastructure --startup-project Maliev.CurrencyService.Infrastructure
```

**Important**: Tests require Docker to be running (uses Testcontainers for Postgres, Redis, RabbitMQ).

---

## 3. Code Style & Conventions

### Workspace Structure
```
Maliev.CurrencyService/
├── Maliev.CurrencyService.Api/              # Controllers, Consumers, Middleware
├── Maliev.CurrencyService.Application/      # Use cases, DTOs, Interfaces, Handlers
├── Maliev.CurrencyService.Domain/           # Entities, value objects, domain interfaces
├── Maliev.CurrencyService.Infrastructure/   # EF Core DbContext, repositories, HTTP clients
├── Maliev.CurrencyService.Tests/            # Unit + Integration tests (xUnit)
├── Directory.Build.props                    # Central package versioning
└── Maliev.CurrencyService.slnx             # Solution file (.slnx preferred over .sln)
```

### C# Naming & Formatting
- **Namespaces**: File-scoped (`namespace Maliev.CurrencyService.Api.Services;`)
- **Classes/Methods/Properties**: `PascalCase`
- **Private fields**: `_camelCase` (underscore prefix)
- **Parameters/locals**: `camelCase`
- **Async methods**: Suffix with `Async` (e.g., `GetCurrencyAsync`)
- **Interfaces**: Prefix with `I` (e.g., `ICurrencyService`)
- **Permissions**: GCP-style `{domain}.{plural-resource}.{action}` as `public const string` in a `Permissions` static class
  - Valid: `currency.currencies.create`, `currency.rates.update`
  - Invalid: `currency.currency.create` (singular), `currency.create` (missing resource)
- **XML docs**: Required on ALL public methods and properties
- **Nullable**: Enabled (`<Nullable>enable</Nullable>`). Use `?` explicitly
- **Imports**: System first, then third-party, then local. Alphabetize within groups. Remove unused `using`
- **Braces**: Allman style (new line) for methods and control structures. Expression-bodied for properties/accessors
- **Indentation**: 4 spaces, LF line endings, UTF-8, trim trailing whitespace

### C# Patterns
- **DI**: Constructor injection with `private readonly` fields (not Primary Constructors)
- **Controllers**: `[ApiController]`, `[ApiVersion("1")]`, `[Route("currency/v{version:apiVersion}")]`
- **Logging**: `ILogger<T>` with structured placeholders (never interpolate): `_logger.LogInformation("Getting currency {Id}", id)`
  - Use `LoggerMessage` source generators for high-volume logs (see `Program.cs` for examples) if applicable
- **Error handling**: Global exception middleware. Return `ProblemDetails` / `ErrorResponse` DTOs. Never expose stack traces
  - Success: Return `Ok(result)`, `Created(...)`, or `NoContent()`
  - Errors: Return `ErrorResponse` object (Status 400/404/500)
  - **Do not** return raw strings or exceptions to the client
  - **Concurrency**: Handle `DbUpdateConcurrencyException` for 412/409 responses using ETags (`If-Match`)
- **JSON**: Check existing conventions in this service for naming policy
- **Manual mapping**: Static extension methods (`ToDto()`, `ToEntity()`). AutoMapper is banned
- **Validation**: `System.ComponentModel.DataAnnotations` on DTOs. FluentValidation is banned

### Async/Await
- Always use `async/await` for I/O bound operations
- Pass `CancellationToken` to all async methods and EF Core calls

---

## 4. Database & Data Access

- **EF Core**: Use `CurrencyDbContext`
- **Queries**: Use `AsNoTracking()` for read-only queries to improve performance
- **Migrations**: Database migrations run on startup (`app.MigrateDatabaseAsync`)

### EF Core Design Package
- ❌ `Microsoft.EntityFrameworkCore.Design` MUST NOT be in Api projects
- ✅ It belongs ONLY in the Infrastructure project where migrations live
- Migration commands must target Infrastructure as both project and startup-project:
  ```
  dotnet ef migrations add <Name> --project Maliev.CurrencyService.Infrastructure --startup-project Maliev.CurrencyService.Infrastructure
  ```

### PostgreSQL xmin Concurrency — Mandatory Pattern
Use shadow property ONLY. Never add a Xmin/xmin property to domain entities.
```csharp
entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
```
- ❌ Never use `UseXminAsConcurrencyToken()` (removed in Npgsql EF v7)
- ❌ Never use entity property `public uint Xmin { get; set; }` or `public uint xmin { get; set; }`
- ❌ Never use `.Ignore(e => e.Xmin)` — remove the entity property instead

---

## 5. File System Operations
- **Paths**: Always use **absolute paths** when using tools
- **Modifications**: Verify existence of files before editing. Use `grep`/`glob` to find usages before refactoring

---

## 6. Banned Libraries (Build Will Fail)

| Banned | Use Instead |
|--------|-------------|
| AutoMapper | Manual mapping extensions |
| FluentValidation | DataAnnotations or manual validation |
| FluentAssertions | Standard xUnit `Assert.*` |
| Swashbuckle/Swagger | Scalar (at `/{service}/scalar`) |
| InMemoryDatabase (EF Core) | Testcontainers with real PostgreSQL |

---

## 7. Testing Rules

- **Framework**: xUnit with standard `Assert` (`Assert.Equal`, `Assert.NotNull`, etc.)
- **Naming**: `MethodName_StateUnderTest_ExpectedBehavior` or `HTTP_METHOD_Path_Scenario_ExpectedStatus`
- **Coverage**: Minimum 80% per service
- **Integration tests**: `BaseIntegrationTestFactory<TProgram, TDbContext>` with Testcontainers (PostgreSQL, Redis, RabbitMQ). Never InMemoryDatabase
- **System tests** (Tier 3): `AspireTestFixture` with `[Collection("AspireDomainTests")]` — shared AppHost, never one per class
- **Eventual consistency**: Use `TestHelpers.WaitForAsync`. Never `Task.Delay`
- **MassTransit consumers**: Must have consumer tests using `AddMassTransitTestHarness()`

### Testing Strategy (4-Tier Pyramid Context)

This service's tests cover **Tier 1 (Unit)** and **Tier 2 (Service Integration)** of the Maliev testing pyramid:

| Tier | What to Test | Infrastructure |
|------|-------------|---------------|
| **Unit** | Business logic, domain models, service methods with mocked dependencies | None (mocks only) |
| **Service Integration** | API endpoints, database persistence, permission enforcement, input validation | `BaseIntegrationTestFactory` + Testcontainers (Postgres/Redis/RabbitMQ) |

**Tier 3 (System Integration)** — cross-service workflows and event chains — is tested in `Maliev.Aspire.Tests/`.

> Full ecosystem test strategy: `Maliev.Aspire.Tests/TEST_PLAN.md`

---

## 8. Mandatory Rules

- **`TreatWarningsAsErrors = true`**: Zero warnings allowed. No suppression
- **`[RequirePermission("currency.resources.action")]`**: On all endpoints, not plain `[Authorize]`
- **API versioning**: All routes versioned (`v1/`)
- **Service prefix**: Routes prefixed with service domain (e.g., `/currency`)
- **Scalar docs**: Configured at `/currency/scalar`
- **Secrets**: Never hardcoded. Use GCP Secret Manager or environment variables
- **Async/await**: All the way down. Pass `CancellationToken`
- **EF Core Design package**: Only in Infrastructure project, never in Api
- **PostgreSQL xmin**: Shadow property only — `entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion()`. Never add entity property
- **Temporary files**: Generate in `/temp` folder, clean up afterwards

---

## 9. Git Rules

- Each `Maliev.*` folder is an independent git repo. `cd` into it before git commands
- **Commit early and often** after every meaningful unit of work. Do not accumulate changes
- **Never use `git checkout` to restore files** — commit first, then `git revert` or `git reset --soft`
- Feature branches merged to `develop` via PR. Do not push without being asked
