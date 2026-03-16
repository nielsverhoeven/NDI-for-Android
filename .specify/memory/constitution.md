<!--
Sync Impact Report
Version change: 1.0.0 -> 1.1.0
Modified principles:
- X. Release-Grade Optimization by Default -> X. Release-Grade Optimization & Compatibility Validation
- New principle -> XI. Latest-Stable Android Toolchain
Added sections:
- None
Removed sections:
- None
Templates requiring updates:
- ✅ .specify/templates/plan-template.md
- ✅ .specify/templates/spec-template.md
- ✅ .specify/templates/tasks-template.md
- ⚠ pending: .specify/templates/commands/*.md (directory not present)
- ✅ docs/android-prerequisites.md
- ✅ docs/ndi-feature.md
- ✅ .github/agents/android.app-builder.agent.md
- ✅ .github/agents/copilot-instructions.md
- ✅ .github/workflows/android-ci.yml
- ✅ specs/001-scan-ndi-sources/research.md
- ✅ specs/001-scan-ndi-sources/plan.md
- ✅ specs/001-scan-ndi-sources/spec.md
- ✅ specs/001-scan-ndi-sources/quickstart.md
- ✅ specs/001-scan-ndi-sources/tasks.md
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
refactor safely. Unit tests MUST use JUnit; UI/integration flows MUST use
Espresso where UI behavior is affected.

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
  proposed work to all eleven core principles.
2. Every pull request MUST include evidence of test-first workflow:
  failing-test commit or equivalent verifiable history.
3. Pull requests that touch UI MUST include Material 3 compliance verification.
4. Pull requests that add permissions, background work, or new modules MUST
  include explicit justification and reviewer sign-off from code owners.
5. Release branches MUST pass unit, instrumentation/UI, and release build
  validation with R8/ProGuard enabled before merge.
6. Each release cycle MUST include a toolchain currency review covering
  compileSdk, targetSdk, AGP, Gradle, Kotlin, JDK/JBR, AndroidX/Jetpack, NDK,
  CMake, and required proprietary SDK compatibility.

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

**Version**: 1.1.0 | **Ratified**: 2026-03-15 | **Last Amended**: 2026-03-15
