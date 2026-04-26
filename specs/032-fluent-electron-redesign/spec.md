# Feature Specification: Fluent + Electron UX Redesign

**Feature Branch**: `032-fluent-electron-redesign`  
**Created**: 2026-04-27  
**Status**: Draft  
**Input**: User description: "redesign the app to Microsoft Fluent Design system and Electron according to the updated constitution."

## Clarifications

### Session 2026-04-27

- Q: Which design language has governance priority for this redesign? -> A: The updated constitution principle V applies: Fluent + Electron design language compliance is mandatory.
- Q: Is this a visual polish-only request or behavior-inclusive redesign? -> A: Behavior can be adjusted where needed to match Fluent + Electron interaction patterns, while preserving existing discovery, playback, and settings functional contracts.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Use a Coherent Fluent + Electron Shell (Priority: P1)

As a user, I can navigate and operate the app through a consistent Fluent + Electron visual shell so the app feels intentional, modern, and predictable across screens.

**Why this priority**: A coherent shell is the foundation of the redesign. Without it, feature-level refinements remain visually fragmented.

**Independent Test**: Can be fully tested by launching the app, traversing Home/View/Output/Settings flows, and verifying unified typography, spacing, color roles, hierarchy, and navigation treatment are consistently applied.

**Acceptance Scenarios**:

1. **Given** the app opens on any supported form factor, **When** the user navigates between top-level screens, **Then** the same Fluent + Electron visual language and navigation affordances are preserved.
2. **Given** a screen transition occurs, **When** content changes state (loading, success, empty, error), **Then** state messaging and hierarchy follow the same Fluent + Electron semantics.

---

### User Story 2 - Complete Core Flows with Redesigned Components (Priority: P2)

As a user, I can complete source discovery, viewing, output control, and settings tasks with redesigned components that remain clear and efficient under the Fluent + Electron language.

**Why this priority**: The redesign is only valuable if core user outcomes remain fast and understandable in real workflows.

**Independent Test**: Can be fully tested by executing one end-to-end flow per major surface (source list, viewer, output, settings) and validating both task completion and visual conformance criteria.

**Acceptance Scenarios**:

1. **Given** discovered sources are available, **When** the user selects a source and opens View or Output, **Then** controls and status indicators use redesigned patterns without reducing task completion ability.
2. **Given** the user opens Settings and modifies a preference, **When** the preference is saved and revisited, **Then** persistence behavior is unchanged and presentation remains Fluent + Electron compliant.

---

### User Story 3 - Rely on Accessible, Adaptive Redesign Behavior (Priority: P3)

As a user on phone or tablet, I can use the redesigned interface with accessible contrast, readable hierarchy, and stable adaptive layouts across orientation and size changes.

**Why this priority**: The redesign must be inclusive and resilient across target devices, not only visually attractive on one profile.

**Independent Test**: Can be fully tested by validating redesigned screens on phone and tablet emulator profiles, including orientation changes and increased text scale, while verifying no critical control becomes unreachable.

**Acceptance Scenarios**:

1. **Given** the app runs on phone and tablet profiles, **When** the user rotates or resizes context, **Then** redesigned layouts keep key controls visible and usable.
2. **Given** accessibility settings increase text scale, **When** the user performs core flows, **Then** labels and actions remain readable and actionable with no blocking overlap.

---

### Visual Change Quality Gate *(mandatory when UI changes are present)*

- This feature changes visual behavior and must include emulator-run Playwright end-to-end coverage for all redesigned user-visible flows.
- Existing Playwright end-to-end regression suites must be executed and remain passing.
- Validation must include explicit Fluent + Electron compliance evidence for each redesigned surface, including typography, spacing, hierarchy, interaction states, and motion intent.

### Test Environment & Preconditions *(mandatory)*

- **Required runtime dependencies**:
  - Android SDK + emulator toolchain validated via repository prerequisite scripts.
  - At least one phone emulator and one tablet emulator profile.
  - Available NDI source fixture(s) for source list/viewer/output interaction checks.
  - Playwright test harness and ADB connectivity.
- **Preflight check commands (required before e2e)**:
  - `pwsh ./scripts/verify-android-prereqs.ps1`
  - `pwsh ./scripts/verify-e2e-dual-emulator-prereqs.ps1`
  - `adb devices`
- **Blocked result handling**:
  - If emulator/device prerequisites fail, mark validation as `BLOCKED (environment)` with exact failing command and remediation step.
  - If NDI fixtures are unavailable, mark affected scenarios as `BLOCKED (environment)` and record fixture requirements.
- **Pre-existing tests policy**:
  - Existing automated tests are regression protection and MUST remain unchanged unless redesign intentionally changes the covered behavior.
  - Any required test updates must reference the exact redesigned behavior contract that changed.

### Edge Cases

- What happens when a redesigned panel has long source names or status text? Content must wrap/truncate predictably without hiding critical actions.
- How does the system handle no-discovery and error states under redesigned visuals? Recovery actions must remain visible and prioritized.
- What happens when settings forms are displayed on compact-height phones? Primary actions must remain reachable without gesture traps.
- How does the redesign behave with large text scale and high contrast needs? Interactive controls must remain distinguishable and operable.
- What happens if only part of the app has been migrated during rollout? Mixed old/new visuals must be prevented on any one user-visible flow within a release candidate.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST introduce and apply a Fluent + Electron design language baseline for all redesigned surfaces in scope.
- **FR-002**: System MUST define reusable visual rules for typography, spacing, color roles, elevation, and state treatment aligned with Fluent + Electron principles.
- **FR-003**: System MUST keep existing navigation contracts intact while updating navigation presentation and interaction affordances to Fluent + Electron patterns.
- **FR-004**: Users MUST be able to complete source discovery, viewer, output, and settings tasks after redesign without additional required steps compared with current behavior.
- **FR-005**: System MUST preserve existing functional persistence behavior for user settings and cached source metadata.
- **FR-006**: System MUST apply redesigned state patterns for loading, success, empty, and error conditions across in-scope screens.
- **FR-007**: System MUST keep lifecycle-safe behavior and existing foreground/background semantics unchanged unless explicitly specified by a separate behavior requirement.
- **FR-008**: For visual additions/changes, system MUST include emulator-run Playwright e2e coverage for redesigned user flows.
- **FR-009**: For visual additions/changes, system MUST execute and keep passing all existing Playwright e2e tests.
- **FR-010**: For visual additions/changes, system MUST provide validation evidence demonstrating Fluent + Electron compliance for each redesigned screen.
- **FR-011**: For environment-dependent validations, system MUST run and record preflight checks before end-to-end or release gates.
- **FR-012**: Validation reporting MUST classify each failed/blocked gate as code failure or environment blocker with reproduction details.
- **FR-013**: Existing automated tests MUST be preserved as regression protection; tests MAY be changed only when the redesign directly changes expected behavior.
- **FR-014**: Any changed pre-existing automated test MUST document the specific redesigned behavior contract that required the test update.
- **FR-015**: System MUST support adaptive redesign behavior for phone and tablet profiles with no loss of access to core actions.
- **FR-016**: System MUST maintain accessibility semantics and target contrast/readability thresholds consistent with Fluent guidance for text and controls.
- **FR-017**: System MUST ensure redesigned interactions do not increase critical task failure rate for core flows in validation.
- **FR-018**: System MUST avoid introducing design-language regressions where Material-default styling appears without documented Fluent + Electron mapping rationale.

### Key Entities *(include if feature involves data)*

- **Design Language Token Set**: A canonical set of typography, spacing, color role, elevation, and state tokens used to enforce Fluent + Electron consistency.
- **Redesigned Screen Contract**: A per-screen definition of required hierarchy, interactive affordances, and state behavior under the new design language.
- **Visual Compliance Evidence Record**: A validation artifact linking test runs, screenshots/logs, and checklist outcomes for Fluent + Electron conformance.
- **Adaptive View Context**: The runtime context (phone/tablet, orientation, text scale) that determines layout adaptation requirements for redesigned surfaces.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of in-scope redesigned screens pass documented Fluent + Electron compliance checks before merge.
- **SC-002**: 100% of new or changed user-visible flows have emulator-run Playwright coverage and pass in release validation runs.
- **SC-003**: Existing Playwright regression suite remains green with 0 newly introduced failures attributable to redesign changes.
- **SC-004**: In a 20-run task validation sample (across phone and tablet), at least 95% of runs complete core flows (discover -> view/output -> settings update) without facilitator intervention.
- **SC-005**: 0 critical accessibility defects are reported for text readability, control focusability, or blocked primary actions on supported profiles.

## Non-Goals

- This feature does not introduce new NDI protocol capabilities or modify source transport behavior contracts.
- This feature does not change repository/domain ownership boundaries or persistence architecture.
- This feature does not require replacing all Material component dependencies at build level if behavior and visuals can be mapped to Fluent + Electron intent.

## Assumptions

- Existing module and navigation architecture remain authoritative and are preserved.
- A phased migration is acceptable if each delivered flow is internally consistent with the Fluent + Electron language.
- Teams will capture visual compliance evidence in test-results artifacts as part of normal validation.
