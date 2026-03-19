# Success Criteria Evidence Report (SC-001..SC-006)

Date: 2026-03-17
Feature: 004-fix-three-screen-nav

| Criterion | Evidence | Status |
|----------|----------|--------|
| SC-001 View source routes to viewer | Coordinator + ViewModel tests and nav action wiring (`TopLevelNavigationCoordinatorTest`, `TopLevelNavViewModelTest`) | PASS |
| SC-002 Viewer/View back reliability >=98% | Repeated-run matrix in `us1-back-reliability-matrix.md`: 20/20 passing, 100.00% pass rate | PASS |
| SC-003 Correct icon mapping | Menu + drawable mapping and icon tests in `TopLevelNavViewModelTest` | PASS |
| SC-004 Singular active highlight | Canonical destination item state + destination observer mapping in MainActivity | PASS |
| SC-005 Unsupported version fail-fast with diagnostics | Support-window helpers + runner diagnostics + helper tests | PASS |
| SC-006 Median dual-emulator runtime improvement >=25% | Runtime benchmark in `e2e-runtime-improvement-report.md`: baseline median 138.64s vs post-change median 140.79s, improvement -1.55% | FAIL |

## Notes

- SC-002 is PASS with repeated-run evidence in `us1-back-reliability-matrix.md`.
- SC-006 is FAIL because measured median improvement is -1.55%, below the >=25% threshold.
