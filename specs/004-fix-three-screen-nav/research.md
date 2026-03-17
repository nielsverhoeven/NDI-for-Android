# Phase 0 Research: Three-Screen Navigation Repairs and E2E Compatibility

## Decision 1: Fix View selection routing to Viewer route (not Stream route)

- Decision: Route source selections from View root list to `ViewerRoute(sourceId)` only, and remove any fallback path that opens stream setup/output control.
- Rationale: This directly resolves the reported regression and satisfies FR-005.
- Alternatives considered: Keep current route and patch viewer launch from stream screen (rejected as incorrect UX), add conditional reroute after wrong screen opens (rejected as brittle and confusing).

## Decision 2: Define explicit back behavior from View root

- Decision: Keep one-step back from viewer to View root, and one-step back from View root to Home.
- Rationale: This implements clarified behavior and keeps top-level navigation predictable.
- Alternatives considered: Exit app from View root (rejected by clarification), return to prior arbitrary destination (rejected because state-history variance complicates predictability and e2e assertions).

## Decision 3: Make top-level highlight state single-source-of-truth

- Decision: Derive active destination highlight exclusively from top-level destination state owned by navigation/viewmodel state, including stream setup/control screens.
- Rationale: Prevents incorrect highlight drift when entering screens via deep links or cross-flow transitions.
- Alternatives considered: Infer highlight from fragment class names ad hoc (rejected as error-prone), manually toggle highlight in each screen (rejected due to duplication and regressions).

## Decision 4: Preserve existing app architecture boundaries

- Decision: Keep route composition in `app`, preserve `Fragment -> ViewModel -> Repository` flow, keep repository contracts in domain and implementations in data.
- Rationale: Satisfies constitution gates and AGENTS.md architecture constraints.
- Alternatives considered: Move navigation logic into feature data modules (rejected for boundary violations), direct persistence reads in fragments (rejected by repository-mediated access principle).

## Decision 5: Use one unified Playwright suite with runtime version branching

- Decision: Keep one dual-emulator Playwright suite and branch behavior at runtime per detected device Android version.
- Rationale: Matches clarification and avoids drift across multiple version-specific scripts.
- Alternatives considered: Separate script per version pair (rejected due to maintenance overhead), single publisher-only version branch (rejected because mixed versions are allowed and must be handled per device).

## Decision 6: Version detection and support-window policy

- Decision: Detect Android major version per device at run start, then evaluate support eligibility using a rolling latest-five-major-version window derived at runtime.
- Rationale: Implements FR-010 and latest clarification without hardcoding a static major list in spec behavior.
- Alternatives considered: Fixed list in code/spec (rejected by clarification), no support gating (rejected because unsupported flows would create false confidence).

## Decision 7: Fail fast on unsupported versions

- Decision: If either emulator is outside the computed support window, terminate the e2e run with non-zero status and explicit diagnostics before any stream/view interaction steps.
- Rationale: Ensures deterministic CI signal and aligns with clarification Option A.
- Alternatives considered: Warn and continue (rejected due to noisy reliability), partial smoke run only (rejected because core assertions would remain untrusted).

## Decision 8: Consent flow strategy across Android UI variants

- Decision: Implement a per-device consent-flow selector that prioritizes full-screen sharing path and supports known variants such as option pickers and explicit confirm buttons (e.g., "Share entire screen" then "Share screen").
- Rationale: Android consent UI varies by version/build; robust branching keeps the suite stable across supported versions.
- Alternatives considered: One fixed tap sequence (rejected as fragile), OCR/image-only matching (rejected due to complexity and lack of need vs UI dump text).

## Decision 9: Cap intentional static waits to <= 1 second

- Decision: Limit all intentional fixed delays in UI helpers to <= 1000ms and rely on dynamic polling/state waits for asynchronous operations.
- Rationale: Meets FR-012 while retaining reliability for emulator latency.
- Alternatives considered: Aggressive zero-delay execution (rejected due to flakiness), keeping legacy 1.5-2s delays (rejected as too slow).

## Decision 10: Validation evidence and diagnostics

- Decision: Keep screenshot and log artifacts for both roles, and attach version-detection/support-window diagnostics to the test output.
- Rationale: Improves triage when failures are due to version gating, consent-path mismatch, or route regressions.
- Alternatives considered: Minimal pass/fail output only (rejected because debugging dual-device failures is too costly).

## Clarification Resolution Status

All feature clarifications are resolved and encoded in design decisions.
No `NEEDS CLARIFICATION` markers remain.
