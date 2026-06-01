# Contract: Fluent + Electron UX Redesign

## 1. Scope and Flow-Slice Contract

- In-scope surfaces are Source List, Viewer, Output, Settings, and top-level navigation shell.
- Delivery MUST be phased by complete user flows.
- No shipped in-scope flow may mix legacy and redesigned visual language within one end-to-end journey.

## 2. Design Language Baseline Contract

- A reusable Fluent + Electron token baseline MUST define typography, spacing, color roles, elevation, and interaction-state treatment.
- Redesigned in-scope screens MUST consume this baseline consistently.
- Any retained Material-default styling MUST include explicit Fluent + Electron mapping rationale.

## 3. Screen State Contract

- Each in-scope redesigned screen MUST define and implement loading, success, empty, and error states using consistent hierarchy semantics.
- Recovery actions MUST remain visible and prioritized in error/empty states.
- Primary user actions MUST remain reachable on phone and tablet profiles.

## 4. Behavior Preservation Contract

- Redesign MUST preserve existing discovery, viewer/output, and settings persistence outcomes unless explicitly changed by requirement.
- Navigation and deep-link contracts MUST remain compatible with existing app graph behavior.
- Repository/domain boundaries and data ownership MUST remain unchanged.

## 5. Accessibility and Adaptivity Contract

- Redesigned flows MUST pass readability/focusability checks for supported phone and tablet profiles.
- Increased text scale validations MUST show no blocked critical action.
- Orientation/adaptive layouts MUST keep core actions discoverable and operable.

## 6. Validation Evidence Contract

- Every redesigned in-scope screen MUST have feature-scoped compliance evidence under `test-results/`.
- Evidence MUST include checklist outcomes and traceable links to test outputs.
- Validation outcomes MUST be classified as `Pass`, `Code failure`, or `BLOCKED (environment)` with blocker remediation when blocked.

## 7. Test Coverage Contract

- Redesign changes MUST follow failing-test-first sequencing where automated tests exist.
- Playwright coverage MUST be added/updated for redesigned user-visible flows.
- Existing Playwright e2e regression suite MUST be executed and remain passing.
- Preflight checks MUST run before e2e/release gates:
  - `pwsh ./scripts/verify-android-prereqs.ps1`
  - `pwsh ./scripts/verify-e2e-dual-emulator-prereqs.ps1`
  - `adb devices`

## 8. Release Readiness Contract

- Merge readiness requires:
  - Compliance evidence for all in-scope redesigned screens in the active flow slice
  - Passing redesigned-flow Playwright coverage
  - Passing existing Playwright regression profile
  - Passing release hardening gate (`:app:verifyReleaseHardening`) unless blocked by documented environment constraints
