# Stage 3 Architecture Validation - Issue #142

Date: 2026-06-05
Validator: architect
Gate: FAIL

## Scope Reviewed
- docs/constitution.md
- docs/architecture.md
- docs/features/restore-settings-menu-functionality/spec.md
- docs/features/restore-settings-menu-functionality/plan.md

## Findings (Ordered by Severity)

### 1. Missing authoritative settings baseline in plan (High)
The plan states that required sections will be restored, but it does not define the authoritative section/control inventory to implement. This leaves requirement interpretation open and risks divergence from the constitution's testability and deterministic behavior expectations.

Constitution impact:
- Testing Standards: cannot guarantee complete happy-path/error-path coverage when target section set is undefined.
- Development Agreements: risk of regressions that cannot be objectively accepted or rejected.

Required correction:
- Add an explicit baseline matrix in the plan listing each required section and expected controls.

### 2. Navigation contract is referenced but not concretely specified for verification (Medium)
The plan says to preserve Settings route and entry points, but it does not define exact route contract checks and acceptance assertions from all relevant feature flows.

Constitution and architecture impact:
- Navigation contract requires stable Shell URI behavior.
- Without concrete verification steps, route regressions may pass design review.

Required correction:
- Add explicit route checks for //settings reachability from Sources, Viewer, and Output entry paths.

### 3. Persistence compatibility strategy is underspecified (Medium)
The plan allows additive model updates, but does not define migration/defaulting behavior per field or failure-handling for partial/corrupt persisted records.

Constitution impact:
- Persistence reliability and async repository standards require deterministic load/save behavior.

Required correction:
- Add field-level compatibility/defaulting rules and repository error-path handling expectations.

### 4. DI lifetime and ownership decisions are implicit (Low)
The plan indicates interface-first registrations but does not lock expected lifetimes for new settings services/composers.

Constitution impact:
- DI consistency is required at MauiProgram root; missing lifetimes can cause state drift or stale caching.

Required correction:
- Document intended lifetimes for any new abstractions (e.g., singleton/transient) and rationale.

## Required Plan Amendments

1. Add a section: "Required Settings Baseline Matrix"
   - Table columns: Section, Control, Required/Optional, Validation Rule, Persistence Key, Default Value, Intentional Exclusion Note.

2. Add a section: "Navigation Contract Assertions"
   - Explicitly assert route contract remains //settings.
   - Define verification from Sources, Viewer, and Output entry points.

3. Add a section: "Persistence Compatibility and Migration"
   - Define additive field strategy, legacy-value defaulting, and repository behavior on malformed persisted data.
   - Require async-only read/write paths and explicit error-path tests.

4. Add a section: "DI Registration Decisions"
   - Enumerate each new interface/implementation and lifetime in MauiProgram.

## Stage 3 Gate Result

FAIL

Rationale:
The plan aligns with the constitutional architecture directionally, but lacks key concrete design decisions needed for deterministic implementation and verifiable acceptance. Stage 4 readiness is blocked until the required plan amendments above are incorporated.

---

## Stage 5 Implementation Outcome (2026-06-06)

Status: Implemented with environment blockers on runtime UI execution.

Completed architecture-delivery points:

1. Required settings baseline implemented in the MAUI Settings page with explicit section grouping.
2. Settings persistence upgraded with additive schema fields and deterministic fallback for malformed data.
3. Repository validation and sanitization hardened before save/load.
4. Discovery endpoint application routed through repository + orchestrator at NDI bridge boundary.
5. Android-specific settings metadata access isolated via a platform service under Platforms/Android.

Environment blockers:

1. Device and emulator runtime validation could not execute because Appium prerequisites were not configured in this environment.
2. Full APK packaging build stage was not used as a gate because `_BuildApkEmbed` exceeded practical execution time in this run; compile-target and unit test gates were used.