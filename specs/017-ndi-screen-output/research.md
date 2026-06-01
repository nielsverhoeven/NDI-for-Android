# Research: NDI Screen Share Output Redesign

## Decision 1: Discovery Behavior Precedence

- Decision: Use this strict order at start time: configured+reachable discovery server -> register to server; no configured server -> mDNS; configured but unreachable server -> fail start with explicit error.
- Rationale: This matches the clarified product requirement (Option C) and avoids ambiguous partial availability behavior.
- Alternatives considered:
  - Fallback to mDNS when configured server unreachable: rejected by clarified requirement.
  - Continue ACTIVE without any discovery registration: rejected due to poor discoverability and unclear UX.

## Decision 2: Consent Lifecycle

- Decision: Require consent on every new start and clear consent on explicit stop (and force-stop process loss).
- Rationale: Aligns with privacy intent and removes hidden carry-over between distinct output sessions.
- Alternatives considered:
  - Persist consent across starts for convenience: rejected due to user requirement and trust risk.
  - Persist consent until app restart only: rejected because stop must fully reset sharing authorization.

## Decision 3: Background Continuity Model

- Decision: Keep stream running while app is backgrounded using existing foreground-service continuity path and persistent notification with stop action.
- Rationale: Meets the core broadcasting use case while remaining battery-transparent and user-controllable.
- Alternatives considered:
  - Pause stream on background: rejected because it breaks primary usage scenario.
  - WorkManager-based continuation: rejected as unsuitable for continuous low-latency screen output.

## Decision 4: Full-Screen Start UX

- Decision: Provide immediate Share Screen CTA targeting full-device share; rely on Android system chooser behavior when the OS requires app/entire-screen selection.
- Rationale: Delivers the fastest allowed path without violating platform constraints.
- Alternatives considered:
  - Custom pre-selection flow bypassing system dialog: rejected due to platform restrictions.
  - Remove full-screen emphasis and let users pick each time: rejected because it weakens expected UX.

## Decision 5: Error Semantics for Unreachable Discovery Server

- Decision: When a discovery server is configured but unreachable, do not enter ACTIVE; surface a clear actionable error and keep state recoverable.
- Rationale: Required by clarification and enables deterministic validation.
- Alternatives considered:
  - Silent retry loop then eventual fallback: rejected by requirement and introduces non-determinism.
  - Hard crash/terminal failure: rejected because user must recover by fixing settings or network.

## Decision 6: Test Strategy

- Decision: Use failing-test-first updates in output unit tests and add/extend dual-emulator Playwright coverage for start/consent/background/stop/discovery modes, followed by full existing Playwright regression run.
- Rationale: Satisfies constitution TDD and visual-regression obligations.
- Alternatives considered:
  - Manual-only verification: rejected as insufficient for release-quality gates.
  - Espresso-first e2e: rejected because project constitution defaults to Playwright.
