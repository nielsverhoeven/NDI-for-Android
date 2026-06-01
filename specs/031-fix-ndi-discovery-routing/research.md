# Research: NDI Discovery Routing Reliability

**Branch**: `031-fix-ndi-discovery-routing` | **Date**: 2026-04-26

## Decision 1: Evaluate discovery mode once at run start in the repository

- Decision: Determine mode in `NdiDiscoveryRepositoryImpl` at the beginning of each discovery run using the current enabled server list from `NdiDiscoveryConfigRepository.getCurrentEndpoints()`. Use multicast/mDNS only when enabled server count is `0`; otherwise use discovery-server-only for the full run.
- Rationale: This directly implements FR-001, FR-002, and FR-016 while matching the existing repository boundary and avoiding UI/native leakage of policy decisions.
- Alternatives considered: Persisting a global mode flag in the bridge state (rejected: risks stale mode and violates per-run evaluation), deciding mode in ViewModel (rejected: violates repository-mediated data access principle).

## Decision 2: Enforce hard timeout in repository coroutine boundary

- Decision: Wrap discovery-server-mode fetch in a 5-second coroutine timeout at repository level and emit explicit timeout diagnostics when exceeded, with no same-run fallback to multicast.
- Rationale: FR-005 and FR-017 require a hard boundary and explicit reasoning. Repository-level timeout keeps cancellation and diagnostics deterministic while preserving native bridge abstraction.
- Alternatives considered: Native-side timeout only (rejected: less transparent in Android diagnostics path), automatic retry/fallback inside same run (rejected: forbidden by FR-017).

## Decision 3: Preserve canonical source row on identity conflicts

- Decision: For same canonical identity with changed endpoint, update the existing cached row endpoint fields and discovery timestamp using newest discovery record while preserving retained preview continuity fields.
- Rationale: Implements FR-009 and FR-018 and maintains SC-004 cache continuity; avoids destructive replacement that drops preview metadata.
- Alternatives considered: Full-row replace (`REPLACE`) (rejected: can erase optional continuity fields), identity-by-display-name (rejected: unstable and not contract-authoritative).

## Decision 4: Capture per-server diagnostics in discovery-server mode

- Decision: Record per-server response timing and timeout/failure diagnostics in discovery-server mode and surface them through existing diagnostics pathways.
- Rationale: FR-019 and SC-005 require actionable operator evidence and blocker classification; existing diagnostics telemetry paths can carry this without new UI architecture.
- Alternatives considered: Aggregate-only single timing metric (rejected: insufficient to identify slow/unreachable servers), silent timeout counters without context (rejected: not actionable).

## Decision 5: Keep data flow aligned with existing module boundaries

- Decision: Keep domain contracts in `feature/ndi-browser/domain`, implement behavior only in `feature/ndi-browser/data`, retain Room persistence in `core/database`, and keep presentation layers read/render only.
- Rationale: Required by AGENTS architecture rules and constitution principles I, III, VII, IX.
- Alternatives considered: Direct DAO writes in presentation (rejected: violates constitution), introducing a new module for this slice (rejected: unnecessary for current scope).

## Decision 6: Validation strategy prioritizes regression-first with environment preflight

- Decision: Add/adjust targeted JUnit tests for mode selection, timeout/no-fallback, canonical merge behavior, and diagnostics classification first; then run Playwright discovery-path scenarios plus existing regression profile after required preflights.
- Rationale: Satisfies FR-011..FR-015 and constitution principle IV (strict TDD) and XII (execution-ready environment checks).
- Alternatives considered: Manual-only validation for discovery timing (rejected: insufficient regression guard), editing legacy tests first to fit behavior (rejected: regression-first policy).
