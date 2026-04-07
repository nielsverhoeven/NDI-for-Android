# FR-013 Traceability: US1 Test Updates

Date: 2026-04-07
Feature: 029-ndi-server-compatibility
Requirement: FR-013

## Updated Pre-Existing Tests

1. feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/NdiDiscoveryRepositoryContractTest.kt
- Task: T013
- Why updated:
  - Added mixed-server partial-success assertions to enforce FR-006.
  - Ensures incompatible/blocked endpoints do not collapse into a fully successful overall result.
- Requirement/contract links:
  - FR-006 mixed-server reporting
  - Contract section: Mixed-Server Reporting Rule

2. feature/ndi-browser/presentation/src/test/java/com/ndi/feature/ndibrowser/source_list/SourceListViewModelTest.kt
- Task: T015
- Why updated:
  - Added compatibility snapshot propagation coverage so source-list state reflects partial compatibility outcomes.
- Requirement/contract links:
  - FR-006 mixed outcomes are visible and not silently treated as full success
  - FR-007 actionable diagnostics through existing in-app surfaces

## New/Expanded Compatibility Boundary Coverage

3. feature/ndi-browser/data/src/test/java/com/ndi/feature/ndibrowser/data/DiscoveryCompatibilityClassifierTest.kt
- Task: T014
- Coverage added:
  - Discovery-only classification remains limited.
  - Any stream-start failure after discovery is incompatible.
  - No discovery success remains pending.
- Requirement/contract links:
  - FR-004a limited
  - FR-004b incompatible
  - Contract section: Classification Rules
