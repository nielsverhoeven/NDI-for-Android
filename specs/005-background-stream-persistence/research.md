# Phase 0 Research: Background Stream Persistence

## Decision 1: Preserve active stream during app backgrounding

- Decision: Keep active output session running when broadcaster navigates to Home or opens another app, and stop only on explicit user stop action.
- Rationale: The feature goal is continuity for viewers during broadcaster multitasking; existing domain intent already states active output must not stop on top-level navigation.
- Alternatives considered: Stop on any app background event (rejected because it violates requested behavior), auto-restart after stop (rejected because it introduces extra lifecycle complexity and can hide interruptions).

## Decision 2: Use existing continuity model and repository boundaries

- Decision: Reuse existing continuity contracts (`StreamContinuityRepository`, `StreamContinuityState`, `OutputSession`) and keep behavior decisions in ViewModel/repository layers.
- Rationale: This matches architecture constraints in `AGENTS.md` and constitution principles for MVVM and repository-mediated access.
- Alternatives considered: Add continuity state directly in Fragment/activity (rejected due to lifecycle fragility), bypass repository with direct bridge/database calls (rejected by architecture policy).

## Decision 3: Implement one deterministic dual-emulator six-step scenario

- Decision: Extend unified Playwright dual-emulator suite with a scenario that executes the exact ordered sequence: start stream A, view on B, open Chrome on A, assert Chrome visible on B, navigate to `https://nos.nl` on A, assert nos.nl visible on B.
- Rationale: This directly encodes the requested acceptance path while keeping one suite source-of-truth.
- Alternatives considered: Manual-only checklist (rejected because it is non-deterministic), split into multiple independent tests (rejected because order dependency is a key requirement).

## Decision 4: Drive cross-app navigation through ADB intent commands

- Decision: Use ADB-driven launcher/intents from test helpers to move Emulator A from app to Chrome and to open `https://nos.nl`.
- Rationale: Existing e2e harness already uses ADB command orchestration and is stable across emulator runs.
- Alternatives considered: UI-only launcher tapping for all transitions (rejected because launcher layouts vary and can increase flakiness), deep-link through app internals for Chrome step (rejected because it does not validate real app-switch behavior).

## Decision 5: Validate receiver continuity with visual evidence, not only text state

- Decision: Use viewer-surface region checks (non-black visibility + baseline delta + publisher similarity) and explicit step checkpoints for Chrome and nos.nl visibility.
- Rationale: A `PLAYING` label alone does not prove visual content continuity.
- Alternatives considered: State/telemetry-only assertions (rejected because they miss visual regression), pixel-perfect full-frame equality (rejected due to rendering variance across emulator frames).

## Decision 6: Keep fail-fast diagnostics with step-level failure context

- Decision: Preserve and extend run diagnostics to include which checkpoint failed and attach screenshots/UI dumps/logcat for both emulator roles.
- Rationale: Dual-device failures are expensive to triage without structured artifacts.
- Alternatives considered: Single pass/fail output (rejected due to poor debuggability), retry-only without diagnostics (rejected because root cause remains opaque).

## Decision 7: Maintain battery-conscious governance with explicit justification

- Decision: Treat background continuity as user-initiated foreground-value streaming work, requiring explicit cancellation on user stop and no autonomous persistent jobs.
- Rationale: Satisfies constitution Principle VI while enabling requested continuity behavior.
- Alternatives considered: New always-on background service regardless of stream state (rejected due to battery impact and policy mismatch), disabling background continuity to avoid energy concerns (rejected because it violates feature intent).

## Clarification Resolution Status

All planning clarifications are resolved for this feature.
No `NEEDS CLARIFICATION` markers remain.
