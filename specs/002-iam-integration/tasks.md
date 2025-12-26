# Tasks: IAM Integration Migration

**Input**: Design documents from `/specs/002-iam-integration/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are requested in the specification (Phase 4 of plan.md). TDD approach will be used for protected operations.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure for authorization constants.

- [x] T001 [P] Define 19 permissions in Maliev.CurrencyService.Api/Authorization/CurrencyPermissions.cs
- [x] T002 [P] Define 4 predefined roles and their permission mappings in Maliev.CurrencyService.Api/Authorization/CurrencyPredefinedRoles.cs
- [x] T003 [P] Add authorization feature flags and IAM configuration to Maliev.CurrencyService.Api/appsettings.json

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure for IAM integration and rate limiting that MUST be complete before user stories.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T004 Implement CurrencyIAMRegistrationService (IHostedService) with Fail Fast behavior (ensure process termination on registration failure) in Maliev.CurrencyService.Api/Services/CurrencyIAMRegistrationService.cs
- [x] T005 Register IAM registration service in Maliev.CurrencyService.Api/Program.cs
- [x] T006 [P] Configure JWT Authentication to look for 'permissions' claim in Maliev.CurrencyService.Api/Program.cs
- [x] T007 [P] Configure Service-Level Rate Limiting in Maliev.CurrencyService.Api/Program.cs
- [x] T008 [P] Add RequirePermissionAttribute (if not already in ServiceDefaults) or verify availability in Maliev.CurrencyService.Api/Authorization/

**Checkpoint**: Foundation ready - IAM registration and Auth infrastructure is in place.

---

## Phase 3: User Story 1 - Public Read Access (Priority: P1) 🎯 MVP

**Goal**: Ensure currencies can be listed and rates converted without authentication.

**Independent Test**: Verify `GET /api/v1/currencies` and `GET /api/v1/rates/convert` return 200 OK without an Authorization header.

### Implementation for User Story 1

- [x] T009 [US1] Apply [AllowAnonymous] and IP-based rate limiting to public endpoints in Maliev.CurrencyService.Api/Controllers/CurrenciesController.cs
- [x] T010 [US1] Apply [AllowAnonymous] and IP-based rate limiting to public endpoints in Maliev.CurrencyService.Api/Controllers/RatesController.cs
- [x] T011 [P] [US1] Create integration test for anonymous currency lookup in Maliev.CurrencyService.Tests/Integration/AuthorizationTests.cs
- [x] T012 [P] [US1] Create integration test for anonymous rate conversion in Maliev.CurrencyService.Tests/Integration/AuthorizationTests.cs

**Checkpoint**: User Story 1 (Public Access) is fully functional and verified.

---

## Phase 4: User Story 2 - Admin Currency Management (Priority: P1)

**Goal**: Protect currency CRUD operations with permissions.

**Independent Test**: Verify `POST /api/v1/currencies` returns 401/403 without the 'currency.currencies.create' permission.

### Implementation for User Story 2

- [x] T013 [P] [US2] Create integration test for protected currency creation (401/403/201 cases) in Maliev.CurrencyService.Tests/Integration/AuthorizationTests.cs
- [x] T014 [US2] Apply [RequirePermission(CurrencyPermissions.CurrenciesCreate)] to Create endpoint in Maliev.CurrencyService.Api/Controllers/CurrenciesController.cs
- [x] T015 [US2] Apply [RequirePermission(CurrencyPermissions.CurrenciesUpdate)] to Update endpoint in Maliev.CurrencyService.Api/Controllers/CurrenciesController.cs
- [x] T016 [US2] Apply [RequirePermission(CurrencyPermissions.CurrenciesDelete)] to Delete endpoint in Maliev.CurrencyService.Api/Controllers/CurrenciesController.cs
- [x] T017 [US2] Apply [RequirePermission(CurrencyPermissions.CurrenciesActivate)] to Activate/Deactivate endpoints in Maliev.CurrencyService.Api/Controllers/CurrenciesController.cs

**Checkpoint**: Currency Management is fully protected and verified.

---

## Phase 5: User Story 3 - Secure Rate Management (Priority: P2)

**Goal**: Protect rate update operations with permissions.

**Independent Test**: Verify `PUT /api/v1/rates` returns 403 for users without 'currency.rates.update'.

### Implementation for User Story 3

- [x] T018 [P] [US3] Create integration test for rate update protection in Maliev.CurrencyService.Tests/Integration/AuthorizationTests.cs
- [x] T019 [US3] Apply [RequirePermission(CurrencyPermissions.RatesUpdate)] to Rate Update endpoint in Maliev.CurrencyService.Api/Controllers/RatesController.cs
- [x] T020 [US3] Apply [RequirePermission(CurrencyPermissions.RatesBulkUpdate)] to Bulk Update endpoint in Maliev.CurrencyService.Api/Controllers/RatesController.cs
- [x] T021 [US3] Apply [RequirePermission(CurrencyPermissions.RatesSetSource)] to Set Source endpoint in Maliev.CurrencyService.Api/Controllers/RatesController.cs

**Checkpoint**: Rate Management is fully protected and verified.

---

## Phase 6: User Story 4 - Snapshot & System Operations (Priority: P3)

**Goal**: Protect snapshots and system maintenance tasks.

**Independent Test**: Verify snapshot creation requires 'currency.snapshots.create'.

### Implementation for User Story 4

- [x] T022 [P] [US4] Create integration test for snapshot operation protection in Maliev.CurrencyService.Tests/Integration/AuthorizationTests.cs
- [x] T023 [US4] Apply [RequirePermission] to all endpoints in Maliev.CurrencyService.Api/Controllers/SnapshotsController.cs per specification
- [x] T024 [US4] Apply [RequirePermission(CurrencyPermissions.SystemRefreshRates)] to refresh endpoint in Maliev.CurrencyService.Api/Controllers/RatesController.cs
- [x] T024b [US4] Apply [RequirePermission(CurrencyPermissions.SystemRebuildCache)] to cache management endpoints in Maliev.CurrencyService.Api/Services/CacheTagService.cs or Controllers/
- [x] T024c [US4] Apply [RequirePermission(CurrencyPermissions.SystemViewStats)] to metrics/telemetry endpoints if exposed via Controller in Maliev.CurrencyService.Api/Controllers/

**Checkpoint**: All snapshots and system operations are fully protected.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final verification and documentation.

- [x] T025 [P] Verify Fail Fast behavior when IAM service is down (Integration Test) in Maliev.CurrencyService.Tests/Integration/AuthorizationTests.cs
- [x] T026 Update README.md with new authorization and rate limiting details
- [x] T027 Run full test suite and ensure 100% pass rate
- [x] T028 Run quickstart.md validation for both AuthEnabled=true and AuthEnabled=false

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Defines constants, must be first.
- **Foundational (Phase 2)**: Depends on Phase 1. BLOCKS all user stories.
- **User Stories (Phase 3-6)**: Depend on Phase 2 completion. Can be implemented in priority order or in parallel.
- **Polish (Phase 7)**: Final verification after implementation.

### Parallel Opportunities

- T001-T003 can be done in parallel.
- T006-T008 can be done in parallel.
- User Story integration tests (T011, T012, T013, T018, T022, T025) can be developed in parallel with implementation if multiple developers are involved.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL)
3. Complete Phase 3: User Story 1 (Public Access)
4. **STOP and VALIDATE**: Test that the service works for public users with IAM integration active.

### Incremental Delivery

1. Foundation ready (Phase 1 & 2)
2. MVP (Public Access US1)
3. Admin Access (US2)
4. Rate Management (US3)
5. Snapshots & System (US4)
6. Polish and Final Validation
