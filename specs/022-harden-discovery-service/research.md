# Phase 0 Research - Discovery Service Reliability

## Decision 1: Define connection-check success using discovery protocol response

- Decision: Treat a server check as successful only when an NDI discovery-specific handshake/response succeeds for the configured endpoint.
- Rationale: TCP-only checks can report reachable while discovery remains non-functional, and source-presence checks are too strict for empty-but-healthy discovery servers.
- Alternatives considered:
  - TCP socket reachability only: rejected due to false positives.
  - Source-count-based success: rejected because an empty source list does not imply server failure.

## Decision 2: Keep add-time and recheck-time validation in repository boundary

- Decision: Run server validation through discovery server repository operations so validation is applied consistently for add and targeted recheck operations.
- Rationale: Repository-mediated validation preserves architecture rules and keeps UI layers thin.
- Alternatives considered:
  - Fragment-level direct checks: rejected due to MVVM violation and poor testability.
  - Bridge calls from ViewModel: rejected because repository should own data-validation lifecycle.

## Decision 3: Persist per-server diagnostic status metadata

- Decision: Persist last check status, timestamp, and failure detail per registered server entry.
- Rationale: Recheck UX and developer troubleshooting require stable state across refreshes and app restarts.
- Alternatives considered:
  - In-memory status only: rejected because diagnostics disappear across lifecycle changes.
  - Global status blob without per-server keys: rejected because it prevents targeted recheck reporting.

## Decision 4: Keep recheck action server-targeted and non-destructive

- Decision: Add a per-row recheck action that updates only the selected server status.
- Rationale: Developers need precise recovery checks without mutating ordering/enabled state or re-adding entries.
- Alternatives considered:
  - Full-list recheck only: rejected due to lower diagnostic precision.
  - Remove-and-readd workflow: rejected due to unnecessary churn and risk.

## Decision 5: Expand developer diagnostics through existing overlay and log buffer

- Decision: Extend existing diagnostics logging/state pipeline to include discovery check lifecycle events and endpoint-scoped outcomes, visible only when developer mode is enabled.
- Rationale: The app already has a diagnostics repository and redacted log flow; extending this avoids parallel logging systems.
- Alternatives considered:
  - New diagnostics subsystem/module: rejected as unnecessary complexity.
  - Logcat-only diagnostics with no UI surfacing: rejected because requirement asks for additional developer-mode information.

## Decision 6: Preserve native bridge canonical multi-endpoint discovery model

- Decision: Keep bridge behavior that applies all configured endpoints simultaneously and uses persistent finder semantics; integrate validation/logging without regressing this flow.
- Rationale: Existing bridge implementation already encodes known-correct NDI discovery patterns for multi-server discovery.
- Alternatives considered:
  - Per-endpoint reinit loops: rejected because they reset discovery warm-up and source accumulation.
  - Separate finder per endpoint: rejected due to complexity and behavior divergence from current design.

## Decision 7: Validation strategy remains TDD-first with Playwright visual coverage

- Decision: Add/adjust failing JUnit tests first (repository + ViewModel behavior), then add emulator-run Playwright tests for new UI flows, then execute full Playwright regression.
- Rationale: Constitution requires strict test-first development and Playwright coverage for visual changes.
- Alternatives considered:
  - Manual verification only: rejected as non-repeatable.
  - Espresso-first UI tests: rejected because Playwright is the default required e2e path.

## Decision 8: Treat environment readiness as hard gate before end-to-end checks

- Decision: Require prerequisite scripts before e2e gates and classify blocked runs as environment blockers with explicit unblock actions.
- Rationale: Device/sdk drift is a common failure source and is explicitly governed by constitution principle XII.
- Alternatives considered:
  - Best-effort preflight warnings: rejected because blocked-vs-code-failure distinction must be explicit.
