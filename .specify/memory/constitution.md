<!--
Sync Impact Report
Version change: 2.1.0 -> 2.2.0
Modified principles:
- IV. Strict Test-Driven Development (NON-NEGOTIABLE) -> IV. Strict Test-Driven Development (NON-NEGOTIABLE)
Added principles:
- XII. Execution-Ready Validation Environments
Removed sections:
- None
Templates requiring updates:
- updated: .specify/templates/plan-template.md
- updated: .specify/templates/spec-template.md
- updated: .specify/templates/tasks-template.md
- pending: .specify/templates/commands/*.md (directory not present)
- updated: docs/README.md
Follow-up TODOs:
- None
-->

# NDI-for-Android Constitution

## Core Principles

### I. MVVM-Only Presentation Logic

All UI behavior, state transformation, and presentation decisions MUST reside in
ViewModels. Activities, Fragments, and Composables MUST remain thin view layers
that only render state and dispatch user intent.

Rationale: Concentrating presentation logic in ViewModels improves testability,
reduces UI coupling, and prevents lifecycle-related defects.

### II. Single-Activity Navigation Architecture

The app MUST use a single-activity architecture and the Navigation Component for
all in-app navigation flows. New features MUST integrate through navigation
graphs and typed-safe argument passing patterns approved by the team.

Rationale: A unified navigation model reduces back-stack inconsistency and
supports predictable deep-link and state restoration behavior.

### III. Repository-Mediated Data Access

Data access MUST be mediated through repositories. ViewModels and UI layers MUST
NOT directly call network, Room DAO, or platform persistence APIs.

Rationale: Repository boundaries isolate data sources, simplify mocking, and
support maintainable offline/online synchronization strategies.

### IV. Strict Test-Driven Development (NON-NEGOTIABLE)

Every feature and bug fix MUST follow Red-Green-Refactor. Developers MUST write
or update failing tests first, then implement minimal code to pass, then
refactor safely. Unit tests MUST use JUnit. End-to-end flows MUST default to
Playwright. If Playwright is not feasible for a required end-to-end scenario, an
alternative must be proposed with justification and approval. 
If existing Espresso tests are found when working on a feature, they must be converted to Playwright tests as part of the implementation.
Any feature that introduces or changes visual behavior MUST add Playwright e2e
coverage executed on emulator(s) for the new or changed user-visible flow and
MUST verify that all existing Playwright e2e tests still pass.
Any change that touches shared persistence models, settings save paths, or
cross-screen state transfer MUST include regression tests that prove existing
stored fields and behaviors are preserved.

Rationale: Enforced TDD keeps scope controlled, prevents regressions, and
creates executable documentation of expected behavior.

### V. Material Design 3 Compliance

New or modified UI MUST comply with Material Design 3 component, typography,
color, and accessibility guidance unless a documented product exception is
approved.

Rationale: Design-system consistency improves usability, accessibility, and
implementation speed.

### VI. Battery-Conscious Execution

No background work is permitted without explicit technical justification,
lifecycle-bound cancellation behavior, and measurable user value. WorkManager,
foreground services, and persistent jobs MUST include documented energy impact.

Rationale: Battery performance is a product requirement, not an optimization
afterthought.

### VII. Offline-First Data Reliability

User-critical features MUST continue to function offline via local Room-backed
data paths. Synchronization and conflict resolution strategy MUST be documented
for any feature that mutates server-backed data.

Rationale: Offline reliability ensures resilient UX across unstable networks.

### VIII. Least-Permission Security

Android permissions MUST be minimized. Every declared permission MUST include a
feature-level justification and user-visible necessity. Unused or speculative
permissions are prohibited.

Rationale: Restricting permissions lowers security risk, improves trust, and
reduces app-store review friction.

### IX. Feature-First Gradle Modularization

Code organization MUST follow Gradle modularization by feature. New features
MUST be introduced as feature modules with explicit API surfaces and dependency
boundaries.

Rationale: Modular boundaries improve build performance, ownership clarity, and
parallel development.

### X. Release-Grade Optimization & Compatibility Validation

ProGuard/R8 optimization and shrinking MUST be enabled for release builds and
verified in CI for every release candidate. Any upgrade to compileSdk,
targetSdk, AGP, Gradle, Kotlin, JDK/JBR, AndroidX, NDK, CMake, or the NDI SDK
MUST include release-build validation before merge.

Rationale: Size and runtime optimization are required for shipping quality on
mobile networks and constrained devices, and compatibility drift is easiest to
catch in release-mode validation.

### XI. Latest-Stable Android Toolchain

The repository MUST track the latest stable Android SDK, build tools, JDK/JBR,
AGP, Gradle, Kotlin, AndroidX/Jetpack, NDK, and CMake versions that are
mutually compatible and supported by required third-party SDKs. compileSdk and
targetSdk MUST be moved to the latest stable Android API level within one
planned maintenance cycle of release unless a documented blocker exists. Any
toolchain component that lags the latest stable compatible version MUST be
tracked with an owner, blocker, and target resolution date.

Rationale: Android platform lag accumulates security, compatibility, and store
policy risk; enforcing current stable tooling keeps the app buildable and
deployable on current Android standards.

### XII. Execution-Ready Validation Environments

Implementation and validation plans MUST include explicit runtime preflight
checks for required environment dependencies (for example device availability,
dual-emulator readiness, and script/tool prerequisites) before final quality
gates are attempted. Build and install tasks MUST reference deterministic
artifact paths or explicit artifact discovery steps when outputs can be renamed
or versioned by build logic. If a gate is blocked by environment constraints,
the block MUST be recorded with reproducible evidence and a concrete unblocking
step.

Rationale: Most schedule slips in this repository come from environment drift
rather than code defects; preflight and deterministic execution rules reduce
false failures and rework.

## Platform Baseline & Security Constraints

- The minimum supported Android API level MUST be 24.
- compileSdk and targetSdk MUST use the latest stable Android API level that is
  compatible with the current stable AGP/Gradle/Kotlin stack and required
  third-party SDKs.
- The build JDK/JBR baseline MUST be the latest stable version supported by the
  active AGP/Gradle pair; Android Studio stable JBR is an approved baseline.
- Android SDK platform packages, build-tools, command-line tools, platform
  tools, emulator, NDK, and CMake MUST be kept on the latest stable compatible
  versions declared by the repository or its setup documentation.
- Kotlin and AndroidX implementations MUST preserve backward compatibility for
  API 24+ unless explicitly approved as a breaking platform change.
- Room is the required local persistence mechanism for offline-first data needs.
- Manifest, network security, and exported components MUST follow least-privilege
  defaults and explicit declaration.
- Any toolchain version change MUST update build files, CI configuration,
  prerequisite documentation, and validation commands in the same change.

## Delivery Workflow & Quality Gates

1. Every spec and implementation plan MUST include a Constitution Check mapping
  proposed work to all twelve core principles.
2. Every pull request MUST include evidence of test-first workflow:
  failing-test commit or equivalent verifiable history.
3. Pull requests that touch UI MUST include Material 3 compliance verification.
4. Pull requests that add permissions, background work, or new modules MUST
  include explicit justification and reviewer sign-off from code owners.
5. Release branches MUST pass unit, instrumentation/UI, and release build
  validation with R8/ProGuard enabled before merge, and MUST include
  Playwright end-to-end results unless a documented exception applies.
6. Any feature that adds or changes visual UI behavior MUST include emulator-run
  Playwright e2e coverage for the changed functionality before merge.
7. Any feature pull request that changes UI behavior MUST include evidence that
  the existing Playwright e2e suite was executed and remains passing.
8. Each release cycle MUST include a toolchain currency review covering
  compileSdk, targetSdk, AGP, Gradle, Kotlin, JDK/JBR, AndroidX/Jetpack, NDK,
  CMake, and required proprietary SDK compatibility.
9. Features with emulator/device-dependent tests MUST run and record preflight
  checks before executing end-to-end gates.
10. Validation reports MUST distinguish code failures from environment-blocked
  gates and include explicit retry/unblock commands.
11. Install and deployment validation steps MUST use deterministic APK artifact
  selection when build tasks produce versioned filenames.

## Governance

This constitution is the highest-priority engineering policy for this
repository. In case of conflict, this constitution supersedes local conventions
and undocumented team habits.

Amendment process:

1. Propose changes via pull request that includes rationale, migration impact,
  and any required template updates.
2. Obtain approval from at least one mobile maintainer and one reviewer
  responsible for architecture quality.
3. Update dependent templates and governance references in the same change.

Versioning policy (semantic):

1. MAJOR for incompatible governance changes or principle removals/redefinitions.
2. MINOR for new principles/sections or materially expanded obligations.
3. PATCH for clarifications, wording improvements, and non-semantic edits.

Compliance review expectations:

1. Constitution compliance MUST be checked during plan review and PR review.
2. Violations MUST be documented in the implementation plan Complexity Tracking
  table and explicitly approved before merge.
3. Periodic audits MUST be performed each release cycle to verify ongoing
  adherence and remove drift.

**Version**: 2.2.0 | **Ratified**: 2026-03-15 | **Last Amended**: 2026-03-27
