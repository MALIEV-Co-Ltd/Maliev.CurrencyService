# Feature Specification: Permission-Based Authorization Migration

**Feature Branch**: `002-iam-integration`
**Created**: 2025-12-22
**Status**: Draft
**Input**: User description: "Implement permission-based authorization for CurrencyService"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Public Read Access (Priority: P1)

Anonymous users (public) must be able to view currency details and convert exchange rates without authentication, ensuring the service's primary public function remains accessible.

**Why this priority**: High Priority. This ensures the core public functionality of the CurrencyService is not disrupted by the migration.

**Independent Test**: Can be fully tested by making HTTP GET requests to public endpoints without an Authorization header and verifying 200 OK responses.

**Acceptance Scenarios**:

1. **Given** the service is running and auth is enabled, **When** an anonymous user requests `GET /currencies`, **Then** the system returns a list of currencies (200 OK).
2. **Given** the service is running, **When** an anonymous user requests `GET /rates/convert`, **Then** the system returns the converted amount (200 OK).
3. **Given** the service is running, **When** an anonymous user requests `GET /currencies/{id}`, **Then** the system returns the currency details (200 OK).

---

### User Story 2 - Admin Currency Management (Priority: P1)

Administrators must be able to create, update, and delete currencies, while unauthorized users are blocked.

**Why this priority**: High Priority. Critical for data integrity and security, closing the current gap where anyone can modify reference data.

**Independent Test**: Can be tested by attempting CRUD operations with and without the `currency-admin` role.

**Acceptance Scenarios**:

1. **Given** an unauthenticated user, **When** they request `POST /currencies` to create a new currency, **Then** the system returns 401 Unauthorized or 403 Forbidden.
2. **Given** a user with the `currency-viewer` role, **When** they request `DELETE /currencies/{id}`, **Then** the system returns 403 Forbidden.
3. **Given** a user with the `currency-admin` role, **When** they request `POST /currencies` with valid data, **Then** the system creates the currency and returns 201 Created.

---

### User Story 3 - Secure Rate Management (Priority: P2)

Authorized managers must be able to update exchange rates, preventing unauthorized manipulation of market data.

**Why this priority**: Medium Priority. Essential for maintaining accurate financial data, though less frequent than read operations.

**Independent Test**: Can be tested by attempting rate updates with `currency-manager` role vs `currency-viewer`.

**Acceptance Scenarios**:

1. **Given** a user with `currency-manager` role, **When** they request `POST /rates` to update a rate, **Then** the system updates the rate and returns success.
2. **Given** a user with only `currency-viewer` role, **When** they request `POST /rates`, **Then** the system returns 403 Forbidden.
3. **Given** a user with `currency-admin` role, **When** they request `POST /rates/bulk` for bulk updates, **Then** the system processes the updates successfully.

---

### User Story 4 - Snapshot & System Operations (Priority: P3)

Operators must be able to manage snapshots and trigger system refresh tasks securely.

**Why this priority**: Low Priority. Administrative maintenance tasks that are less critical for daily public usage but important for audit and health.

**Independent Test**: Verify snapshot creation and system refresh endpoints enforce permissions.

**Acceptance Scenarios**:

1. **Given** a user with `currency-operator` role, **When** they request `POST /snapshots`, **Then** the system creates a rate snapshot.
2. **Given** an anonymous user, **When** they request `POST /system/refresh-rates`, **Then** the system returns 401/403.
3. **Given** a user with `currency-manager` role, **When** they request `GET /snapshots/{id}`, **Then** the system returns the snapshot details.

## Clarifications

### Session 2025-12-22

- Q: How should the service behave if the IAM service is unavailable or returns an error during startup registration? → A: Fail Fast: The service should terminate or fail its health check if registration fails.
- Q: Where exactly in the JWT token should the service look for these permissions? → A: `permissions` claim: Expect a custom claim array named `permissions` containing the strings.
- Q: Should rate limiting be implemented within the service or by infrastructure? → A: Service-Level: Implement rate limiting within CurrencyService middleware (e.g., using AspNetCore.RateLimiting).

### Edge Cases

- **Startup Failure**: If the external IAM service is unavailable during the startup registration phase, the service MUST fail to start (or report unhealthy) to prevent operation with an unverified security configuration.
- How does the system handle a user with a valid token but missing required claims? (Standard 403)
- What happens if the Feature Flag `PermissionBasedAuthEnabled` is set to `false`? (All endpoints should operate as before, likely allowing anonymous or unrestricted access).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST enforce 19 specific permissions for granular access control (as defined in `data-model.md`).
- **FR-002**: System MUST register the 4 predefined roles (`currency-admin`, `currency-manager`, `currency-operator`, `currency-viewer`) and their permission mappings on startup.
- **FR-003**: System MUST allow anonymous (public) access to `GET /currencies` (list, details, search) and `GET /rates` (get, convert, historical).
- **FR-004**: System MUST require `currency.currencies.create`, `.update`, `.delete` permissions for respective currency management operations.
- **FR-005**: System MUST require `currency.rates.update` permissions for modifying exchange rates.
- **FR-006**: System MUST require `currency.snapshots.*` permissions for snapshot management operations.
- **FR-007**: System MUST support a configuration flag `PermissionBasedAuthEnabled` that, when false, disables permission checks (or reverts to legacy behavior).
- **FR-008**: System MUST require `currency.system.*` permissions for maintenance tasks like cache rebuilding and rate refreshing.
- **FR-009**: System MUST implement service-level rate limiting (IP-based for anonymous, identity-based for authenticated) to protect endpoints.

### Key Entities *(include if feature involves data)*

- **Permission**: A granular capability string (e.g., `currency.currencies.create`) determining access to a specific action.
- **Role**: A named collection of permissions (e.g., `currency-manager`) assigned to users.
- **UserProfile**: (Implicit) The authenticated entity making requests, possessing roles and a `permissions` claim array in their JWT.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All 19 permissions and 4 roles are successfully registered with the IAM service upon service startup.
- **SC-002**: Public endpoints (currencies list/get, rate convert) return 200 OK for 100% of valid anonymous requests.
- **SC-003**: 100% of Admin/Write endpoints return 401/403 for unauthenticated or unauthorized requests.
- **SC-004**: Authenticated requests with correct permissions successfully perform create/update/delete operations.
- **SC-005**: System successfully starts up and operates with `PermissionBasedAuthEnabled=false` feature flag, maintaining legacy access levels.
