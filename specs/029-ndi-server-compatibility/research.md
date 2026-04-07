# Research: NDI Discovery Server Compatibility

## Decision 1: Compatibility scope includes baseline, venue, and all obtainable older versions
- Decision: The validation matrix will include latest known-good server, failing venue server, and every older version the team can obtain as a runnable test target.
- Rationale: This directly addresses the venue failure while preventing blind spots in older deployments.
- Alternatives considered: Validate only latest+venue (rejected: misses nearby older versions); define fixed minimum version without evidence (rejected: speculative).

## Decision 2: Final compatibility taxonomy is four-state with temporary unknown
- Decision: Final matrix statuses are compatible, limited, incompatible, and blocked. Unknown compatibility is temporary until enough evidence is collected.
- Rationale: Keeps release decisions explicit while allowing short-lived uncertainty during initial probing.
- Alternatives considered: Permanent unknown state (rejected: weak completion signal); binary compatible/incompatible (rejected: loses discovery-only support nuance).

## Decision 3: Limited versus incompatible boundary is capability-based
- Decision: Limited means discovery-only support. Incompatible means stream start fails even once in an otherwise ready environment.
- Rationale: Aligns classification with the operator workflow that matters in venue usage.
- Alternatives considered: Define incompatible as discovery failure only (rejected: ignores stream-start breakage).

## Decision 4: Diagnostics delivery uses existing in-app surfaces plus evidence artifacts
- Decision: Reuse existing diagnostics/overlay surfaces and persist validation evidence in test-results artifacts; no dedicated compatibility screen.
- Rationale: Minimizes UI churn and preserves architecture while still making outcomes actionable.
- Alternatives considered: New dedicated compatibility UI (rejected: out-of-scope expansion and extra regression burden).

## Decision 5: Mixed-server discovery must avoid false full-success signaling
- Decision: In mixed-version configurations, preserve usable sources from compatible servers but report non-compatible endpoints as partial/failed compatibility outcomes.
- Rationale: Prevents misleading operator feedback when one server fails compatibility.
- Alternatives considered: Fail all discovery on any incompatible server (rejected: discards usable data); ignore incompatible servers silently (rejected: hides operational risk).

## Decision 6: Validation is preflight-first with explicit blocked handling
- Decision: Compatibility validation runs preflight checks before quality gates and records blocked outcomes with repro evidence and retry conditions.
- Rationale: Environment drift is common in emulator/device/network-dependent Android validation.
- Alternatives considered: Run tests directly and triage later (rejected: high false-failure noise).

## Decision 7: Test policy remains regression-first for existing tests
- Decision: Existing tests are regression protection and are not changed first; only directly impacted tests may be updated with explicit requirement traceability.
- Rationale: Matches constitution v2.3.0 and reduces accidental contract drift.
- Alternatives considered: Freely updating brittle tests during feature work (rejected: masks regressions).
