# 031 US3 Red-State Evidence (Phase 5 T041a)

Date: 2026-04-26
Feature: 031-fix-ndi-discovery-routing
User Story: US3 - Cached Source Reuse After Discovery
Status: RED captured before implementation

## Commands Executed

1. `./gradlew.bat :feature:ndi-browser:data:testDebugUnitTest --tests "*CachedSourceRepositoryImplTest*" -x lint`
2. `./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest --tests "*SourceListViewModelTest*" -x lint`
3. `npm --prefix testing/e2e exec playwright test tests/031-discovery-routing.spec.ts --grep "cache-relaunch"`

## Failing Unit Tests

### Data Module

Task: `:feature:ndi-browser:data:testDebugUnitTest`

Failure:
- `CachedSourceRepositoryImplTest > upsertFromDiscovery_whenRediscoveryOmitsLastValidated_preservesExistingLastSeenMetadata`
- Assertion failure at `CachedSourceRepositoryImplTest.kt:264`

Summary:
- `9 tests completed, 1 failed`
- Build task failed as expected for red state.

### Presentation Module

Task: `:feature:ndi-browser:presentation:testDebugUnitTest`

Failure:
- `SourceListViewModelTest > cacheFlow_updatesWhileLiveDiscoveryStillInProgress_separateFromLiveFlow`
- Assertion failure at `SourceListViewModelTest.kt:405`

Summary:
- `17 tests completed, 1 failed`
- Build task failed as expected for red state.

## Failing Playwright Tests

Command output included these failures (expected red state placeholders):

- `US2 discovery-server-only mode disables multicast for the run`
- `US2 endpoint handoff uses persisted source endpoint and not discovery endpoint`
- `US2 timeout surfaces explicit diagnostics in <= 5 seconds without same-run fallback`
- `US3 cache-relaunch shows cached rows before live discovery completion (SC-004)`

US3-specific failure details:
- Spec: `testing/e2e/tests/031-discovery-routing.spec.ts:20`
- Assertion: expected `cached-before-live=true`, got `cached-before-live=false`
- Secondary assertion in same test expects `last-seen-marker=present`

## Red-State Conclusion

US3 red-state evidence has been captured for newly added unit-test assertions and the US3 Playwright scenario stub. Implementation tasks T042-T046 can proceed.
