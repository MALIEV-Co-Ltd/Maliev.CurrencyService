# Feature Specification: Currency WebAPI Service

**Feature Branch**: `001-currency-service`
**Created**: 2025-11-17
**Status**: Draft
**Input**: User description: "Create a Currency WebAPI service that stores and manages global currency metadata, detects the correct currency for a given country ISO2 or ISO3 code, and returns spot or snapshot exchange rates between any two currencies. The service must treat THB as the application primary currency, prioritize minimal resource usage, and deliver sub-50ms p95 read responses for cached lookups on small instances. Exchange-rate lookup must consult external, free, no-API-key providers in the configured order: first Fawazahmed, then Frankfurter. The service must automatically perform step conversions when a direct pair is missing by routing via a reliable intermediary currency (USD as default intermediary) and compute combined rates deterministically."

## Clarifications

### Session 2025-11-17

- Q: What RBAC roles should be defined for administrative access control? → A: Two roles: "Admin" (full CRUD + ingestion) and "ReadOnlyAdmin" (view admin data, job status)
- Q: What format(s) should the service accept for snapshot data ingestion? → A: JSON format (structured, API-native, easy validation)
- Q: How should rate limiting identify clients for enforcement on public endpoints? → A: Per API key/token (granular control, client accountability)
- Q: Which currency pairs should be pre-loaded during cache warming on startup? → A: Top N most-requested pairs from configuration file (flexible, tunable)
- Q: What types of provider calls should use retry logic (FR-065 "metadata calls")? → A: Retry all live rate queries to external providers (Fawazahmed, Frankfurter)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Currency Metadata Lookup (Priority: P1)

As an API consumer, I need to query currency information by country code so that I can display the correct currency for a user's location or determine which currency pairs to request.

**Why this priority**: This is the foundation capability - without being able to discover available currencies and their country associations, consumers cannot effectively use any exchange rate functionality. This enables basic currency discovery and country-to-currency mapping.

**Independent Test**: Can be fully tested by querying the currencies endpoint with various country codes (ISO2/ISO3) and verifying correct currency metadata is returned, including cases where multiple currencies exist for one country.

**Acceptance Scenarios**:

1. **Given** the service has currency metadata loaded, **When** I request currencies for country code "TH" (ISO2), **Then** I receive THB currency details including code, name, and symbol
2. **Given** the service has currency metadata loaded, **When** I request currencies for country code "THA" (ISO3), **Then** I receive the same THB currency details as ISO2 lookup
3. **Given** the service has currency metadata loaded, **When** I request all available currencies, **Then** I receive a paginated list of all supported currencies
4. **Given** I provide an invalid country code, **When** I request currencies for that code, **Then** I receive a clear error message indicating the country code is not found
5. **Given** a country has multiple currencies, **When** I request currencies for that country code, **Then** I receive all associated currencies

---

### User Story 2 - Live Exchange Rate Retrieval (Priority: P1)

As an API consumer, I need to get current exchange rates between any two currencies so that I can display real-time pricing or perform currency conversions for users.

**Why this priority**: This is the core value proposition of the service - providing live exchange rates. Combined with currency metadata lookup, this creates a complete MVP that delivers immediate business value.

**Independent Test**: Can be fully tested by requesting exchange rates for various currency pairs and verifying that current rates are returned within performance SLA, including validation of transitive conversions when direct pairs are unavailable.

**Acceptance Scenarios**:

1. **Given** external providers are available, **When** I request a live exchange rate from USD to EUR, **Then** I receive the current rate with provider source and timestamp
2. **Given** a direct currency pair is not available from any provider, **When** I request a rate (e.g., THB to SGD), **Then** the service computes a transitive rate via USD intermediary and returns the combined rate
3. **Given** the first provider (Fawazahmed) is unavailable, **When** I request a live rate, **Then** the service automatically falls back to Frankfurter provider
4. **Given** a rate exists in cache and is still fresh, **When** I request the same rate, **Then** I receive the cached response in under 50ms (p95)
5. **Given** I request an invalid currency pair, **When** I submit the request, **Then** I receive a clear error indicating which currency code is invalid
6. **Given** all external providers are down, **When** I request a live rate, **Then** I receive a stale cached rate with a header indicating staleness, if available

---

### User Story 3 - Snapshot Exchange Rate Query (Priority: P2)

As an API consumer, I need to query historical exchange rates from specific snapshots so that I can perform accurate accounting reconciliation, historical analysis, or audit compliance.

**Why this priority**: While live rates serve immediate operational needs, snapshot queries enable critical business functions like financial reporting, historical analysis, and regulatory compliance. This is independent of live rates but builds on the same currency metadata.

**Independent Test**: Can be fully tested by ingesting known snapshot data, then querying for rates at specific snapshot times and verifying accuracy against the ingested data.

**Acceptance Scenarios**:

1. **Given** snapshot data has been ingested for a specific date, **When** I request an exchange rate for that snapshot date, **Then** I receive the exact rate that was valid at that time
2. **Given** snapshot data exists in cache, **When** I query for a snapshot rate, **Then** I receive the response in under 50ms (p95)
3. **Given** I request a snapshot for a date with no data, **When** I submit the query, **Then** I receive a clear message indicating no snapshot exists for that date
4. **Given** ETag support is enabled, **When** I make a conditional GET with a valid ETag, **Then** I receive a 304 Not Modified response if data hasn't changed
5. **Given** snapshot data has been updated, **When** I query for rates, **Then** the cache is invalidated and I receive the new data

---

### User Story 4 - Snapshot Batch Ingestion (Priority: P2)

As a system administrator, I need to ingest bulk exchange rate snapshots asynchronously so that I can populate historical data or update rates from trusted sources without impacting live query performance.

**Why this priority**: This administrative function enables the service to maintain a rich dataset for snapshot queries. It's independent of live queries and can be tested/deployed separately, but it directly supports User Story 3.

**Independent Test**: Can be fully tested by submitting batch snapshot data in JSON format, monitoring the ingestion job status, and verifying that ingested data is queryable and cache invalidation occurs correctly.

**Acceptance Scenarios**:

1. **Given** I have a valid batch of exchange rate snapshots, **When** I submit them via the admin endpoint, **Then** ingestion starts asynchronously and I receive a job ID for tracking
2. **Given** ingestion is in progress, **When** I query the job status, **Then** I receive current progress information
3. **Given** I want to validate data before applying, **When** I submit a batch with dry-run flag, **Then** I receive a validation report without persisting any data
4. **Given** ingestion completes successfully, **When** new rates are applied, **Then** related cache entries are atomically invalidated
5. **Given** I submit invalid snapshot data, **When** validation runs, **Then** I receive detailed error messages identifying the invalid entries
6. **Given** I attempt batch ingestion without proper authorization, **When** I submit the request, **Then** I receive a 403 Forbidden response

---

### User Story 5 - Currency Metadata Management (Priority: P3)

As a system administrator, I need to create, update, and delete currency metadata so that I can maintain accurate currency information as countries adopt new currencies or update existing ones.

**Why this priority**: This administrative capability ensures the service can adapt to real-world currency changes (new currencies, retired currencies, metadata updates), but it's not required for basic operation since initial currency data can be seeded at deployment.

**Independent Test**: Can be fully tested by performing CRUD operations on currency records with proper authorization, including optimistic concurrency control validation and cache invalidation verification.

**Acceptance Scenarios**:

1. **Given** I have admin privileges, **When** I create a new currency with complete metadata, **Then** the currency is persisted and immediately available via lookup endpoints
2. **Given** a currency exists, **When** I update it with a valid If-Match header, **Then** the update succeeds and cache is invalidated
3. **Given** a currency exists, **When** I attempt to update with an outdated If-Match header, **Then** I receive a 412 Precondition Failed response
4. **Given** I want to retire a currency, **When** I delete it via the admin endpoint, **Then** it's marked as inactive and excluded from future lookups
5. **Given** I attempt currency management without authorization, **When** I submit the request, **Then** I receive a 403 Forbidden response
6. **Given** I create or update currency metadata, **When** the operation completes, **Then** related cache entries are invalidated

---

### Edge Cases

- **What happens when both external providers are unavailable?** Service serves stale cached rates with X-Stale header indicating age, or returns 503 if no cached data exists, ensuring consumers know data freshness status
- **How does the system handle rate requests for recently added currencies?** New currencies may not have rates in cache; service attempts provider lookup immediately and caches successful results
- **What happens when intermediary currency (USD) rate is missing?** Service attempts alternative intermediary currencies in configured fallback order (EUR, GBP), or returns error if no path exists
- **How does system handle concurrent snapshot ingestion jobs?** Second ingestion request returns 409 Conflict until first job completes, preventing data consistency issues
- **What happens when distributed cache is unavailable?** Service falls back to instance-local cache only; performance may degrade but service remains operational
- **How are very old snapshot queries handled?** Service returns 404 if snapshot date exceeds configured retention window, with clear message about retention policy
- **What happens during cache warming on startup?** Background cache warming occurs asynchronously for configured top N currency pairs; queries may have slightly higher latency until warming completes
- **How does system handle partial provider responses?** Service validates response completeness; partial/malformed data triggers fallback to next provider in chain
- **What happens with rate requests during snapshot update?** Snapshot updates use staging area first, then atomic swap with cache invalidation to prevent serving inconsistent data during transition
- **How are duplicate currency codes handled?** Currency codes are unique identifiers; duplicate creation attempts return 409 Conflict with clear error message

## Requirements *(mandatory)*

### Functional Requirements

#### Currency Metadata Management

- **FR-001**: System MUST store currency metadata including currency code (ISO 4217), full name, symbol, decimal precision, and active status
- **FR-002**: System MUST maintain associations between currencies and countries using both ISO2 and ISO3 country codes
- **FR-003**: System MUST support one-to-many relationship between countries and currencies (e.g., countries with multiple legal tender currencies)
- **FR-004**: System MUST allow querying currencies by ISO2 country code with exact match
- **FR-005**: System MUST allow querying currencies by ISO3 country code with exact match
- **FR-006**: System MUST return paginated lists of all available currencies
- **FR-007**: System MUST treat THB as the primary application currency for default operations
- **FR-008**: Administrators MUST be able to create new currency records via protected endpoints
- **FR-009**: Administrators MUST be able to update existing currency records via protected endpoints
- **FR-010**: Administrators MUST be able to delete (soft delete/deactivate) currency records via protected endpoints
- **FR-011**: Currency update operations MUST use optimistic concurrency control via If-Match headers or version fields

#### Exchange Rate Retrieval

- **FR-012**: System MUST provide endpoint to request live exchange rates between any two supported currency codes
- **FR-013**: System MUST provide endpoint to request snapshot (historical) exchange rates for specific dates
- **FR-014**: System MUST support explicit mode selection between live fetch and snapshot query
- **FR-015**: System MUST consult external rate providers in configured order: Fawazahmed first, then Frankfurter
- **FR-016**: System MUST automatically fall back to next provider if current provider fails or returns no data for requested pair
- **FR-017**: System MUST perform transitive rate conversion via intermediary currency (default: USD) when direct pair is unavailable from any provider
- **FR-018**: System MUST use configured intermediary currency with fallback alternatives (USD -> EUR -> GBP) for transitive conversions
- **FR-019**: System MUST compute combined rates deterministically using multiplication for transitive conversions (e.g., THB->USD * USD->EUR = THB->EUR)
- **FR-020**: System MUST include provider source, timestamp, and confidence/freshness metadata in rate responses
- **FR-021**: System MUST validate currency codes against known currency metadata before querying providers
- **FR-022**: System MUST return clear error messages for invalid currency codes or unsupported pairs

#### Caching Strategy

- **FR-023**: System MUST implement two-tier caching: instance-local cache for hottest items and distributed shared cache for common keys
- **FR-024**: System MUST cache live exchange rates with configurable TTL (default 300 seconds / 5 minutes, configurable via CacheOptions:LiveRateTtlSeconds)
- **FR-025**: System MUST cache snapshot exchange rates with longer TTL appropriate for immutable historical data
- **FR-026**: System MUST cache currency metadata lookups to minimize database queries
- **FR-027**: System MUST implement stale-while-revalidate pattern with configurable grace period (default 60 seconds, configurable via CacheOptions:StaleWhileRevalidateSeconds) to prevent thundering herd
- **FR-028**: System MUST perform cache warming on application startup by pre-loading the top N most-requested currency pairs defined in configuration file
- **FR-029**: System MUST invalidate related cache entries atomically when currency metadata is updated
- **FR-030**: System MUST invalidate related cache entries atomically when snapshot data is updated
- **FR-031**: System MUST serve stale cached data with X-Stale response header when providers are unavailable
- **FR-032**: System MUST continue operating using instance-local cache if distributed cache is unavailable

#### HTTP Caching Support

- **FR-033**: System MUST generate ETags for currency metadata and snapshot rate responses
- **FR-034**: System MUST include Last-Modified headers in responses where applicable
- **FR-035**: System MUST support conditional GET requests using If-None-Match (ETag) headers
- **FR-036**: System MUST support conditional GET requests using If-Modified-Since headers
- **FR-037**: System MUST return 304 Not Modified for conditional requests when content hasn't changed
- **FR-038**: System MUST enable response compression for all endpoints to minimize bandwidth usage

#### Snapshot Batch Ingestion

- **FR-039**: System MUST provide admin endpoint for bulk snapshot ingestion accepting JSON format data
- **FR-040**: System MUST process snapshot ingestion asynchronously as background jobs
- **FR-041**: System MUST use staging area for incoming snapshot data before applying to production
- **FR-042**: System MUST support dry-run mode for snapshot validation without persistence
- **FR-043**: System MUST validate JSON snapshot structure and return validation reports identifying invalid or malformed snapshot entries
- **FR-044**: System MUST provide job status endpoint for tracking ingestion progress
- **FR-045**: System MUST perform atomic cache invalidation when applying validated snapshots
- **FR-046**: System MUST prevent concurrent snapshot ingestion jobs to avoid data inconsistency
- **FR-047**: System MUST limit retention of historical snapshots to configurable time window (e.g., 12 months default)
- **FR-048**: System MUST automatically purge snapshots older than retention window

#### Security & Authorization

- **FR-049**: System MUST require HTTPS for all endpoints
- **FR-050**: System MUST enforce role-based access control (RBAC) for administrative endpoints with two defined roles: "Admin" role with full access to currency CRUD operations and snapshot ingestion, and "ReadOnlyAdmin" role with view-only access to administrative data and job status monitoring
- **FR-051**: System MUST apply rate limiting on public endpoints per API key/token to protect provider usage quotas and prevent abuse
- **FR-052**: System MUST validate and sanitize all inputs to prevent injection attacks
- **FR-053**: System MUST mask sensitive information in application logs
- **FR-054**: System MUST reject admin operations with 403 Forbidden when proper authorization is missing

#### Observability & Health

- **FR-055**: System MUST expose health check endpoint indicating service availability
- **FR-056**: System MUST expose readiness check endpoint indicating service is ready to handle requests
- **FR-057**: System MUST expose Prometheus-compatible metrics for request rates per endpoint
- **FR-058**: System MUST expose metrics for provider latency and error rates
- **FR-059**: System MUST expose metrics for cache hit/miss ratios (both in-process and distributed)
- **FR-060**: System MUST expose metrics for background job status and completion rates
- **FR-061**: System MUST log provider source and response times for observability

#### Failure Handling & Resilience

- **FR-062**: System MUST favor availability over consistency when providers or persistence are unavailable
- **FR-063**: System MUST serve stale cached rates with clear staleness indicator when providers are down
- **FR-064**: System MUST return 503 Service Unavailable for mutation operations when persistence layer is unavailable
- **FR-065**: System MUST implement conservative retry logic for live rate queries to external providers (Fawazahmed, Frankfurter) but not for bulk snapshot ingestion operations
- **FR-066**: System MUST continue serving read requests using cache when database is temporarily unavailable

### Key Entities

- **Currency**: Represents a global currency with attributes including currency code (e.g., "USD", "THB"), full name (e.g., "Thai Baht"), display symbol (e.g., "฿"), decimal precision (e.g., 2), and active status. Each currency may be associated with multiple countries.

- **Country-Currency Association**: Represents the mapping between countries and their legal tender currencies, including both ISO2 code (e.g., "TH") and ISO3 code (e.g., "THA"). Supports one-to-many relationships for countries with multiple currencies.

- **Exchange Rate**: Represents a conversion rate between two currencies at a specific point in time, including source currency code, target currency code, rate value, timestamp, provider source, and whether it's computed via transitive conversion.

- **Rate Snapshot**: Represents a batch of exchange rates captured at a specific point in time, used for historical queries. Includes snapshot date, collection of rates, ingestion timestamp, and validation status. Snapshots are immutable once applied.

- **Staged Snapshot**: Temporary storage for incoming snapshot batches during validation and dry-run testing. Includes validation results, error details, and job metadata. Deleted after successful application or rejection.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Cached currency metadata lookups respond in under 50ms at p95 latency on small instance types (2 vCPU, 4GB RAM)
- **SC-002**: Cached exchange rate queries respond in under 50ms at p95 latency on small instance types
- **SC-003**: Service successfully retrieves live rates from at least one configured provider 99.5% of the time during provider availability
- **SC-004**: Transitive currency conversions via intermediary produce mathematically correct rates within 0.01% precision
- **SC-005**: Service handles 1000 concurrent read requests without degradation on two-replica deployment
- **SC-006**: Cache hit ratio exceeds 80% for exchange rate queries during normal operation
- **SC-007**: Service remains operational for read queries when distributed cache is unavailable, degrading gracefully to in-process cache
- **SC-008**: Administrative snapshot ingestion completes for 10,000 rate entries within 60 seconds
- **SC-009**: Cache invalidation after snapshot update completes atomically within 5 seconds
- **SC-010**: Service responds to health checks within 100ms indicating accurate service status
- **SC-011**: Provider failover completes within 2 seconds when primary provider is unavailable
- **SC-012**: Memory usage remains under 500MB per instance during normal operation with two replicas
- **SC-013**: Service consumes less than 50% CPU on small instances during normal load (100 req/sec)
- **SC-014**: Stale cache data is served with appropriate headers when all providers are down, maintaining availability
- **SC-015**: Rate limiting protects external provider quotas by enforcing configurable limits per API key/token (e.g., 100 req/min default)

## Assumptions

1. **Initial Currency Data**: A seed dataset of global currencies and country associations will be provided at initial deployment
2. **Provider Reliability**: Fawazahmed and Frankfurter providers have >95% uptime and no authentication requirements
3. **Rate Update Frequency**: External provider rates update frequently enough that 60-300 second cache TTL provides acceptable freshness
4. **Snapshot Sources**: Snapshot data will be ingested from trusted external sources (e.g., central banks, financial data vendors) via manual or scheduled admin operations
5. **Deployment Environment**: Service runs in containerized environment (Docker/Kubernetes) with access to PostgreSQL database and Redis cache
6. **Network Latency**: External provider API calls complete within 500-1000ms under normal conditions
7. **THB Primary Currency**: THB being primary currency means it's the default base for conversions and reporting, not that all rates must go through THB
8. **Intermediary Currency Availability**: USD rates are available from providers for >99% of supported currency pairs
9. **Historical Data Retention**: 12-month retention window for snapshots is sufficient for typical accounting and compliance needs
10. **Small Instance Definition**: Small instance means 2 vCPU, 4GB RAM, appropriate for cost-conscious deployment
11. **Authentication Mechanism**: RBAC implementation uses standard bearer token authentication (OAuth2/JWT pattern); public endpoints require API key for rate limiting
12. **Rate Limit Defaults**: Default rate limit of 100 requests per minute per API key/token balances provider protection with usability
13. **Cache Warming Configuration**: A default set of top N most-requested currency pairs (e.g., N=20-50) will be provided in configuration, tunable per deployment region and usage patterns
14. **Provider Retry Policy**: Conservative retry means 2-3 retry attempts with exponential backoff for transient failures on live rate queries; bulk operations fail-fast to avoid cascading delays

## Dependencies

- **External Services**: Fawazahmed and Frankfurter free exchange rate APIs must be accessible from deployment environment
- **Infrastructure**: PostgreSQL database for persistent storage of currency metadata and snapshots
- **Infrastructure**: Redis instance for distributed caching layer
- **Infrastructure**: HTTPS/TLS certificate infrastructure for secure communication

## Out of Scope

- Real-time streaming or WebSocket-based rate updates
- Cryptocurrency exchange rates
- Advanced financial analytics or charting capabilities
- User account management or multi-tenancy (assumes API key/token provisioning handled externally or via simple admin interface)
- Payment processing or money transfer capabilities
- Currency conversion calculators or UIs (API only)
- Historical trend analysis or forecasting
- Custom provider integration beyond Fawazahmed and Frankfurter
- Guaranteed rate accuracy or legal/regulatory compliance certifications

## Artifacts Requested

The following artifacts should be created during planning and implementation:

1. **OpenAPI 3.0 Specification (YAML)**: Complete API documentation including:
   - Currency metadata endpoints (list currencies, query by country code)
   - Exchange rate endpoints (live and snapshot queries)
   - Administrative endpoints (currency CRUD, snapshot ingestion)
   - JSON schema for snapshot batch ingestion payload
   - Request/response examples for all scenarios
   - Provider metadata and staleness header examples
   - Error response formats

2. **Database Schema (SQL DDL)**: Schema definitions for:
   - Currencies table (compact design with indexes on country codes)
   - Country-currency association table
   - Rate snapshots table (optimized for date-range queries)
   - Staged snapshots table (temporary storage)
   - Appropriate indexes for query performance

3. **Sequence Diagrams**:
   - Live provider lookup flow with fallback chain
   - Transitive conversion via intermediary currency
   - Snapshot ingestion workflow (staging -> validation -> atomic apply)
   - Cache warming on startup

4. **Operational Runbook**: Covering:
   - Cache warming procedures and best practices (configuring top N currency pairs list)
   - Snapshot batch import steps (JSON format preparation -> dry-run -> validation -> apply)
   - Provider credential rotation (if authentication is added later)
   - Switching intermediary currency without downtime
   - Changing provider order in configuration
   - Troubleshooting common failure scenarios
   - Database backup and restoration procedures
   - Performance tuning guidelines (adjusting cache warming pairs, TTL values)
