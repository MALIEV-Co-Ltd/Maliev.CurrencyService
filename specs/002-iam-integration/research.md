# Research: IAM Integration Migration

## Decision: Granular Permission Model
**Decision**: Use a hierarchical permission string format `currency.{module}.{action}`.
**Rationale**: Aligns with the specification and provides clear boundaries between Currencies, Rates, Snapshots, and System operations.
**Alternatives considered**: Numeric permission IDs (rejected: harder to debug and map), Role-only auth (rejected: lacks granularity required by spec).

## Decision: IAM Registration Service
**Decision**: Implement `IHostedService` (`CurrencyIAMRegistrationService`) to register permissions and roles on startup.
**Rationale**: Ensures the IAM service always has the latest definitions from the service. "Fail Fast" behavior will be enforced by terminating if the registration fails when auth is enabled.
**Alternatives considered**: Manual migration scripts (rejected: higher risk of drift), Middleware-based registration (rejected: redundant and slow).

## Decision: JWT Permission Validation
**Decision**: Use `[RequirePermission]` attribute (provided by `ServiceDefaults`) to inspect the `permissions` custom claim in the JWT.
**Rationale**: Standardized approach in MALIEV ecosystem. Efficient as it doesn't require a per-request call to IAM.
**Alternatives considered**: Policy-based auth (rejected: more boilerplate than custom attribute).

## Decision: Rate Limiting Implementation
**Decision**: Use `Microsoft.AspNetCore.RateLimiting` middleware.
**Rationale**: Built-in, supported, and handles IP-based (anonymous) and Identity-based (authenticated) limits as requested in clarifications.
**Alternatives considered**: External Gateway rate limiting (rejected: user specifically requested service-level implementation).

## IAM API Patterns
**Decision**: 
- POST `/iam/v1/permissions/register`
- POST `/iam/v1/roles/register`
**Rationale**: Standard REST patterns for idempotent resource registration.
