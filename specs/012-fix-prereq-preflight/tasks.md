# Tasks: Fix Prereq Preflight

**Input**: Design documents from `/specs/012-fix-prereq-preflight/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are REQUIRED by constitution. Every user story MUST include test tasks and failing-test-first sequencing. For infrastructure scripts, include PowerShell unit tests and CI integration tests.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **CI Infrastructure**: `scripts/`, `.github/workflows/` at repository root

## Phase 1: Setup (Research & Planning)

**Purpose**: Understand current implementation and plan modifications

- [ ] T001 Analyze current verify-android-prereqs.ps1 script structure and logic in scripts/verify-android-prereqs.ps1
- [ ] T002 Review CI workflow preflight job configuration in .github/workflows/android-ci.yml
- [ ] T003 Document current failure patterns and missing prerequisites based on research.md

---

## Phase 2: Foundational (Script Architecture)

**Purpose**: Establish the core script modifications that enable installation functionality

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 Add installation function framework to scripts/verify-android-prereqs.ps1
- [X] T005 Define Prerequisite and InstallationResult data structures in scripts/verify-android-prereqs.ps1
- [X] T006 Implement prerequisite state tracking mechanism in scripts/verify-android-prereqs.ps1
- [X] T007 Add logging infrastructure for installation operations in scripts/verify-android-prereqs.ps1

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Successful PR Build (Priority: P1) 🎯 MVP

**Goal**: Ensure CI preflight passes by installing missing prerequisites automatically

**Independent Test**: Create a PR with code changes and verify preflight job completes successfully without manual setup

### Tests for User Story 1 (REQUIRED) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T008 [P] [US1] Unit test for package installation function in scripts/test-install-functions.ps1
- [X] T009 [P] [US1] Integration test for prerequisite verification with installation in scripts/test-integration.ps1
- [X] T010 [US1] CI integration test - run preflight on clean environment and verify success

### Implementation for User Story 1

- [X] T011 [US1] Implement basic Android SDK package installation using sdkmanager in scripts/verify-android-prereqs.ps1
- [X] T012 [US1] Add automatic SDK license acceptance in scripts/verify-android-prereqs.ps1
- [X] T013 [US1] Integrate installation check into main verification loop in scripts/verify-android-prereqs.ps1
- [X] T014 [US1] Add success/failure reporting for installation attempts in scripts/verify-android-prereqs.ps1

**Checkpoint**: At this point, basic automatic installation should work for missing packages

---

## Phase 4: User Story 2 - Automatic Installation (Priority: P2)

**Goal**: Robust automatic installation with error handling and retries

**Independent Test**: Run script on environment with network issues and verify graceful handling

### Tests for User Story 2 (REQUIRED) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T015 [P] [US2] Unit test for retry logic in installation failures in scripts/test-retry-logic.ps1
- [X] T016 [P] [US2] Error handling test for network failures in scripts/test-error-handling.ps1
- [X] T017 [US2] End-to-end test with simulated failures in scripts/test-e2e-failures.ps1

### Implementation for User Story 2

- [X] T018 [US2] Implement retry mechanism with exponential backoff for failed installations in scripts/verify-android-prereqs.ps1
- [X] T019 [US2] Add comprehensive error handling for installation failures in scripts/verify-android-prereqs.ps1
- [X] T020 [US2] Implement actionable error messages for troubleshooting in scripts/verify-android-prereqs.ps1
- [X] T021 [US2] Add installation progress logging and status reporting in scripts/verify-android-prereqs.ps1

**Checkpoint**: Installation is robust and provides clear feedback on failures

---

## Phase 5: User Story 3 - Optimized Installation Time (Priority: P3)

**Goal**: Ensure installation completes within 5 minutes for fast CI feedback

**Independent Test**: Measure installation time on clean CI environment and verify <5 minutes

### Tests for User Story 3 (REQUIRED) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T022 [P] [US3] Performance test for installation timing in scripts/test-performance.ps1
- [X] T023 [US3] Timeout handling test to prevent hanging installations in scripts/test-timeout.ps1

### Implementation for User Story 3

- [X] T024 [US3] Add timeout controls for installation operations in scripts/verify-android-prereqs.ps1
- [X] T025 [US3] Optimize package download process (prioritize critical packages) in scripts/verify-android-prereqs.ps1
- [X] T026 [US3] Add caching mechanism for license acceptance in scripts/verify-android-prereqs.ps1
- [X] T027 [US3] Implement parallel installation where possible in scripts/verify-android-prereqs.ps1

**Checkpoint**: Installation performance meets the 5-minute target

---

## Final Phase: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, documentation, and cleanup

- [X] T028 Update script documentation and usage examples in scripts/verify-android-prereqs.ps1
- [X] T029 Add CI-specific parameters and flags in scripts/verify-android-prereqs.ps1
- [X] T030 Validate backward compatibility with existing usage in scripts/verify-android-prereqs.ps1
- [X] T031 Run comprehensive testing across different environments
- [X] T032 Update CI workflow documentation if changes made in .github/workflows/android-ci.yml
- [X] T033 Create troubleshooting guide for installation issues

---

## Dependencies

**Story Completion Order** (must be implemented in this sequence):
1. US1 (P1) - Basic automatic installation
2. US2 (P2) - Robust error handling  
3. US3 (P3) - Performance optimization

**Task Dependencies**:
- T001-T003 → T004-T007 (research before foundational)
- T004-T007 → All US1 tasks (foundation before stories)
- US1 tasks → US2 tasks (basic before enhanced)
- US2 tasks → US3 tasks (robust before optimized)

## Parallel Execution Examples

**Per Story**:
- US1: T008, T009 can run in parallel with T011-T014
- US2: T015, T016 can run in parallel with T018-T021  
- US3: T022 can run in parallel with T024-T027

**Cross-Story**: None (stories must be sequential due to dependencies)

## Implementation Strategy

**MVP Scope**: Complete US1 for basic automatic installation
**Incremental Delivery**: Add US2 robustness, then US3 optimization
**Testing First**: All test tasks must be written and failing before implementation
**CI Integration**: Validate each increment in actual CI environment</content>
<parameter name="filePath">c:\github\NDI-for-Android\specs\012-fix-prereq-preflight\tasks.md