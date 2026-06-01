# Research: Discovery Server Settings Management

## Decision 1: Discovery server list persistence model

- Decision: Store discovery servers as an ordered Room-backed collection with one row per server, including hostname-or-ip, port, enabled flag, and order index.
- Rationale: The feature requires multiple entries, per-server toggle state, and deterministic ordered failover behavior. A collection model is simpler and safer than overloading the existing single-string discovery setting.
- Alternatives considered:
  - Reuse single discovery endpoint setting field only: rejected because it cannot represent multiple servers or order.
  - Serialize list as JSON inside one settings field: rejected because it weakens queryability, migration clarity, and toggle updates.

## Decision 2: Host/port validation and defaulting

- Decision: Keep separate hostname-or-ip and port inputs in UI; normalize and trim input; apply default port 5959 when port is blank; reject invalid ports and empty hostnames.
- Rationale: Matches feature requirements and avoids ambiguous parsing behavior. Separation also improves usability and validation precision.
- Alternatives considered:
  - Single combined endpoint field only: rejected because the spec explicitly requires separate inputs.
  - Allow blank hostname with port-only entries: rejected because endpoint identity is invalid without hostname-or-ip.

## Decision 3: Duplicate handling and uniqueness rule

- Decision: Treat normalized hostname-or-ip plus effective port as the uniqueness key; block exact duplicates with a clear validation error.
- Rationale: Prevents silent no-op entries and keeps toggles/failover behavior predictable.
- Alternatives considered:
  - Allow duplicates and rely on list order only: rejected due to user confusion and unnecessary runtime retries.
  - Uniqueness by hostname only: rejected because different ports on same host are legitimate.

## Decision 4: Runtime selection behavior for enabled servers

- Decision: When multiple servers are enabled, use persisted list order; try enabled servers sequentially and fail over to the next enabled server when unreachable; fail with clear error if all enabled servers are unreachable.
- Rationale: This behavior was clarified in the spec and provides deterministic runtime behavior with practical resilience.
- Alternatives considered:
  - Parallel attempts to all enabled servers: rejected due to unnecessary complexity and harder observability.
  - Only first enabled server used with no failover: rejected because it reduces resilience and value of multi-server management.

## Decision 5: UI architecture and navigation placement

- Decision: Add a dedicated discovery-server submenu from existing settings flow, following existing Fragment -> ViewModel -> Repository layering and app navigation graph patterns.
- Rationale: Aligns with constitution principles (MVVM, single-activity navigation, repository boundaries) and existing codebase architecture.
- Alternatives considered:
  - Inline management directly in current settings screen: rejected because it does not satisfy submenu requirement and increases settings clutter.
  - Implement outside existing feature/presentation boundaries: rejected for architecture violation risk.

## Decision 6: Test strategy and quality gates

- Decision: Use strict test-first workflow with JUnit unit tests for validation and persistence behavior, plus Playwright emulator e2e for submenu/add/default-port/multi-server/toggle flows, followed by full Playwright regression run.
- Rationale: Required by constitution and feature quality gates for visual changes and settings persistence safety.
- Alternatives considered:
  - Manual QA only: rejected as insufficient and non-repeatable.
  - Espresso-first e2e for new visual flows: rejected because this repository defaults e2e to Playwright.

## Decision 7: Environment and blocked-gate handling

- Decision: Run prereq preflight before emulator-based validation and classify failures as code defects vs environment blockers with reproducible evidence.
- Rationale: Required by constitution principle XII and avoids false-negative quality outcomes.
- Alternatives considered:
  - Run tests without explicit preflight: rejected due to recurring environment-drift risk.

## Resolved Clarifications

All planning-time unknowns for this feature have been resolved. No `NEEDS CLARIFICATION` markers remain.
