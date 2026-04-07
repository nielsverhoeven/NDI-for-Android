# Implementation Tasks: Developer Log Configured IP Display

**Feature**: 028-fix-configured-ip-log  
**Branch**: `028-fix-configured-ip-log`  
**Created**: 2026-04-07  
**Target**: Android minSdk 24, compile SDK 34, Kotlin 2.2.10

## Dependencies & Execution Strategy

### User Story Completion Order

1. **User Story 1 (P1)** - Show Real Configured IPs (Core Feature)
   - Unblocks: US2, US3
   - MVP Scope: Implement runtime address resolution, update View logging, add single-address e2e test.
   - Parallel Opportunities: Address validation logic (T007) and ViewModel integration (T006) can execute in parallel with initial View screen updates (T008), provided T005 (address resolution interface) completes first.

2. **User Story 2 (P2)** - Multi-Address Accuracy (Completeness)
   - Depends on: US1 (address resolution infrastructure)
   - Parallel Opportunities: Multi-address deduplication logic (T013) and multi-address ViewModel support (T011) can run in parallel after T005.

3. **User Story 3 (P3)** - Safe Fallback Behavior (Robustness)
   - Depends on: US1 (address resolution), US2 (multi-address support)
   - Parallel Opportunities: Fallback message logic (T018) and fallback ViewModel integration (T017) can run in parallel.

### Parallel Execution Example (Phase 3 - US1)

**Sequential Critical Path**: T005 → T006 → T008  
**Parallel Opportunity Batch 1** (while T006 executes): T007 + Playwright framework setup  
**Batched Example**:
```shell
# Task T005 (Address resolution interface) completes
# Then, execute in parallel:
./gradlew :feature:ndi-browser:presentation:test -k AddressValidationTest
./gradlew :feature:ndi-browser:presentation:test -k ViewerLogFormattingTest
```

---

## Phase 1: Setup & Infrastructure

### 1.1 Validate Prerequisite Environment

- [X] T001 Run prerequisite validation for Android SDK and build tools in `./scripts/verify-android-prereqs.ps1`
- [X] T002 Confirm device authorization via adb and check emulator status in `./scripts/verify-e2e-dual-emulator-prereqs.ps1`
- [X] T003 [P] Create local session log for validation evidence in `test-results/028-ip-display-validation.md`

---

## Phase 2: Foundational Tasks (Blocking for All User Stories)

### 2.1 Address Validation & Resolution Infrastructure

**Goal**: Establish reusable address validation and resolution logic that all user stories depend on.  
**Independent Test**: Can be tested in isolation by invoking address validation on representative IPv4/IPv6/hostname inputs and confirming correct pass/fail/fallback behavior.

- [X] T004 [P] Create address validation interface in `feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/AddressValidation.kt`
  - Define methods: `isValidAddress(value: String): Boolean`, `validateAndFilterAddresses(addresses: List<String>): List<String`, `getDisplayText(addresses: List<String>): String`
  - Support IPv4, IPv6, hostname validation rules
  
- [X] T005 [P] Implement single-address validation utility in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/validation/AddressValidator.kt`
  - Implement IPv4 regex pattern matching
  - Implement IPv6 regex pattern matching
  - Implement hostname pattern matching
  - Implement deduplication logic preserving order
  - Implement fallback text generation
  
- [X] T006 [P] Create developer mode address resolution in ViewModel layer in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerDeveloperLogResolver.kt`
  - Resolve active configured address set at runtime
  - Query developer mode state
  - Integrate address validation and formatting

### 2.2 Data Model & State Integration

**Goal**: Wire address state into existing ViewModel/logging architecture.  
**Independent Test**: Can be tested by invoking data layer to retrieve configured addresses and verifying state transitions.

- [X] T007 [P] Update ViewerViewModel to expose developer mode address state in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModel.kt`
  - Add `developerModeAddresses: StateFlow<List<String>>`
  - Add `developerModeEnabled: StateFlow<Boolean>`
  - Wire to existing developer mode configuration source
  
- [X] T008 [P] Create unit test for address validation in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/AddressValidatorTest.kt`
  - Red: Test IPv4 validation pass cases (192.168.1.1, 0.0.0.0, 255.255.255.255)
  - Red: Test IPv4 rejection (999.999.999.999, incomplete octets)
  - Red: Test IPv6 validation (ff02::1, ::1, valid expanded forms)
  - Red: Test IPv6 rejection (invalid colons, malformed)
  - Red: Test hostname validation (ndi-host.local, example.com, localhost)
  - Red: Test hostname rejection (spaces, invalid chars)
  - Red: Test deduplication preserves order
  - Green: Implement AddressValidator methods to pass all tests

---

## Phase 3: User Story 1 - Show Real Configured IPs (P1)

**Story Goal**: Developers can see actual configured IP addresses in View screen logs instead of redacted placeholders.  
**Independent Test Criteria**: Enable developer mode, configure an IP address, trigger log emission on View screen, verify actual address appears in log output.  
**Acceptance**: SC-001 (100% of dev-mode sessions show actual addresses), SC-002 (non-dev-mode suppression works).

### 3.1 Core Implementation (US1)

- [X] T009 [P] [US1] Locate existing View screen developer log emission point in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt` or `ViewerViewModel.kt`
  - Document current redacted placeholder location
  - Identify log event construction path
  - Note developer mode condition check point
  
- [X] T010 [US1] Create developer log entry data class in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/logging/DeveloperLogEntry.kt`
  - Fields: `timestamp: java.time.Instant, eventType: String, message: String, configuredAddresses: List<String>, isDeveloperModeEnabled: Boolean`
  - Use java.time.Instant for timestamp field (Kotlin stdlib compatible)
  - Constructor enforces suppression rule (addresses empty if dev mode off)
  
- [X] T011 [US1] Update View screen logging call to emit actual addresses in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt`
  - Depends on T010 (requires DeveloperLogEntry data class)
  - Replace redacted placeholder emission with call to ViewerDeveloperLogResolver
  - Integrate address validation at emission site
  - Respect developer mode suppression
  
- [X] T012 [P] [US1] Add JUnit test for single-address log output in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/ViewerDeveloperLogTest.kt`
  - Red: Test developer mode ON + one IPv4 address => log contains actual address string
  - Red: Test developer mode OFF => log suppression (no configured-address output)
  - Green: Implement log emission to satisfy both tests

### 3.2 End-to-End Validation (US1)

- [X] T013 [P] [US1] Create Playwright test for single-address developer log in `testing/e2e/tests/viewer-developer-log-single-address.spec.ts`
  - Setup: Build app via `./gradlew.bat :app:installDebug`
  - Enable developer mode in test
  - Configure single IPv4 address (192.168.1.10)
  - Open View screen and trigger relevant log
  - Assert log output contains "192.168.1.10" (not redacted)
  - Assert no platform/emulator errors
  
- [X] T014 [US1] Add test assertion helper for log text extraction in `testing/e2e/helpers/viewer-log-assertions.ts`
  - Helper: `extractConfiguredAddressFromLog(logText: string): string`
  - Helper: `assertLogContainsAddress(screen: Screen, expectedAddress: string): Promise<void>`

---

## Phase 4: User Story 2 - Multi-Address Accuracy (P2)

**Story Goal**: Multiple configured addresses display correctly in logs with no duplicates and proper ordering.  
**Independent Test Criteria**: Configure 2-3 mixed-format addresses, trigger log emission, verify all appear in correct order with no duplicates.  
**Acceptance**: SC-003 (100% of expected addresses appear), SC-004 (regression passes).

### 4.1 Multi-Address Support

- [X] T015 [P] [US2] Implement multi-address collection formatting utility in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/validation/MultiAddressFormatter.kt`
  - Method: `formatMultipleAddresses(addresses: List<String>, maxDisplay: Int = 5): String`
  - Deduplicates while preserving ordered insertion
  - Limit display to first 5 addresses per spec edge case requirement (test with 5+ scenarios in T019)
  - Formats as: "Configured addresses: 192.168.1.10, ff02::1, ndi-host.local"
  
- [X] T016 [P] [US2] Update ViewModel to collect multiple developer addresses in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModel.kt`
  - Extend Task T007: Add `developerModeAddressesList: StateFlow<List<String>>`
  - Ensure order is preserved from configuration source
  
- [X] T017 [US2] Add JUnit test for multi-address deduplication and order in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/MultiAddressFormatterTest.kt`
  - Red: Test 3 unique addresses (IPv4, IPv6, hostname) => all appear in order
  - Red: Test duplicate entries [192.168.1.10, 192.168.1.10] => deduplicated to single entry
  - Red: Test mixed duplicates [(192.168.1.10, ff02::1, 192.168.1.10)] => reordered to [192.168.1.10, ff02::1]
  - Green: Implement MultiAddressFormatter to pass all tests
  
- [X] T018 [P] [US2] Update log emission to use multi-address formatter in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt`
  - Integrate MultiAddressFormatter output into log message
  - Ensure deduplication happens at emission time
  
- [X] T019 [P] [US2] Create Playwright test for multi-address developer log in `testing/e2e/tests/viewer-developer-log-multi-address.spec.ts`
  - Setup: Build and install app
  - Enable developer mode
  - Test scenario A: Configure 3 addresses [192.168.1.10, ff02::1, ndi-host.local]
    - Open View screen and trigger log
    - Assert all 3 appear in log in correct order, no duplicates
  - Test scenario B: Configure 5+ addresses (e.g., 7) to verify max display limit
    - Assert first 5 appear in log, additional truncated (per spec edge case)
    - Verify no misleading partial addresses
  
- [X] T020 [US2] Add helper to extract and validate multi-address output in `testing/e2e/helpers/viewer-log-assertions.ts`
  - Helper: `extractAllAddressesFromLog(logText: string): string[]`
  - Helper: `assertLogContainsAllAddressesInOrder(screen: Screen, expectedAddresses: string[]): Promise<void>`

---

## Phase 5: User Story 3 - Safe Fallback Behavior (P3)

**Story Goal**: When no valid addresses are configured, logs show clear fallback messaging instead of redacted placeholders.  
**Independent Test Criteria**: Disable address configuration or set invalid entries, trigger log, verify "not configured" message appears.  
**Acceptance**: SC-002 (fallback text renders), SC-004 (regression passes).

### 5.1 Fallback Implementation

- [X] T021 [P] [US3] Create fallback message logic in `feature/ndi-browser/data/src/main/java/com/ndi/feature/ndibrowser/data/logging/DeveloperLogFallback.kt`
  - Constant: `FALLBACK_NO_VALID_ADDRESSES = "Configured addresses: not configured"`
  - Method: `getFallbackMessage(reason: String): String`
  
- [X] T022 [US3] Update log emission to emit fallback when no valid addresses in `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt`
  - Check if validated address list is empty
  - Emit fallback message instead of empty/redacted output
  
- [X] T023 [P] [US3] Add JUnit test for fallback behavior in `feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/viewer/DeveloperLogFallbackTest.kt`
  - Red: Test empty address list => fallback message "not configured" appears
  - Red: Test all-malformed list => fallback message appears
  - Green: Implement fallback emission to pass both tests
  
- [X] T024 [P] [US3] Create Playwright test for fallback developer log in `testing/e2e/tests/viewer-developer-log-fallback.spec.ts`
  - Setup: Build and install app
  - Enable developer mode
  - Clear all configured addresses (or set invalid entries)
  - Open View screen and trigger log
  - Assert log contains fallback message "not configured"
  - Assert no redacted placeholders appear
  
- [X] T025 [US3] Add helper to verify fallback messaging in `testing/e2e/helpers/viewer-log-assertions.ts`
  - Helper: `assertLogShowsFallbackMessage(screen: Screen): Promise<void>`

---

## Phase 6: Testing & Quality Assurance

**Goal**: Validate feature completeness, ensure no regressions, confirm preflight procedures work.  
**Independent Test Criteria**: All preflight gates pass, full e2e suite passes, evidence captured.

### 6.1 Regression & Sign-Off

- [X] T026 [P] Run full Playwright regression suite against updated code in `testing/e2e`
  - Command: `./gradlew.bat :feature:ndi-browser:presentation:connectedAndroidTest`
  - Expected: 0 failing existing tests, all 3 new scenarios passing (T013, T019, T024)
  
- [X] T027 [P] Execute release hardening validation in `./scripts/verify-release-hardening.sh`
  - Confirm R8/ProGuard obfuscation rules present in `app/proguard-rules.pro`
  - Confirm `isMinifyEnabled=true` in `app/build.gradle.kts`
  - Run `./gradlew.bat :app:assembleRelease` and verify no errors
  
- [X] T028 Document validation evidence in `test-results/028-ip-display-validation.md`
  - Record preflight script output
  - Attach single-address scenario screenshot
  - Attach multi-address scenario screenshot
  - Attach fallback scenario screenshot
  - Record full regression test output
  - Note any environment blockers and unblocking commands
  
- [X] T029 Sign off on feature completion with validation Summary
  - Confirm SC-001 (100% dev-mode IP display): ✓
  - Confirm SC-002 (non-dev-mode suppression): ✓
  - Confirm SC-003 (multi-address accuracy): ✓
  - Confirm SC-004 (regression zero failures): ✓

---

## Summary

### Task Count by Phase

| Phase | Description | Task Count |
|-------|-------------|-----------|
| 1 | Setup & Infrastructure | 3 |
| 2 | Foundational (Blocking) | 5 |
| 3 | User Story 1 (P1) | 6 |
| 4 | User Story 2 (P2) | 6 |
| 5 | User Story 3 (P3) | 5 |
| 6 | Testing & QA | 4 |
| **TOTAL** | | **29 tasks** |

### Task Breakdown by User Story

- **User Story 1 (P1)**: 6 tasks (T009–T014) — foundational address resolution and single-address display
- **User Story 2 (P2)**: 6 tasks (T015–T020) — multi-address deduplication and ordering
- **User Story 3 (P3)**: 5 tasks (T021–T025) — fallback behavior for invalid/empty configuration

### Parallel Opportunities

- **Phase 2 Parallelization** (Foundational):
  - T004 + T005 (address validation logic): Can run in parallel
  - T006 + T007 + T008 (ViewModel integration + tests): T008 depends on T004+T005, but T006+T007 can run in parallel with T004+T005
  
- **Phase 3 Parallelization** (US1):
  - T009 (location discovery) must complete first → unblocks T010+T011 (implementation) and T012+T013 (tests)
  - T012 (unit tests) and T013 (Playwright) can run in parallel after T011 completes
  
- **Phase 4 Parallelization** (US2):
  - T015 + T016 (multi-address logic and ViewModel) can run in parallel after Phase 2
  - T017 (unit tests) can run in parallel with T018 (integration)
  - T019 (Playwright) depends on T018 completion
  
- **Phase 5 Parallelization** (US3):
  - T021 + T023 (fallback logic and unit tests) can run in parallel
  - T024 (Playwright) depends on T022 (integration)

### MVP Scope (Phase 3 Only)

For minimum viable product delivery, focus on:
1. T009–T014: Show real configured IPs with single-address display
2. Skip T015–T025: Defer multi-address and fallback to Phase 4/5

Regression validation (T026–T029) applies to all phases.

### Implementation Strategy

**Red-Green-Refactor Path**: 
1. Write failing unit tests first (T012, T017, T023)
2. Implement code to pass tests (T010, T011, T015, T018, T022)
3. Refactor for clarity (inline within step 2)
4. Add Playwright e2e (T013, T019, T024) after code feature-complete
5. Validate regression (T026) after all new scenarios pass

**Constitution Compliance**:
- MVVM: Logic in ViewerViewModel and data layer (T007, T016, T006)
- TDD: Failing tests written before implementation (T012, T017, T023)
- Playwright: e2e visual validation required (T013, T019, T024)
- Preflight: Environment blocker handling (T001, T002)
- Release hardening: R8/ProGuard validation (T027)
