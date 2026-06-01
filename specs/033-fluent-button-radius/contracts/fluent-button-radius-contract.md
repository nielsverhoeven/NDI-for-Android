# Contract: Fluent Button Radius Alignment

## 1. Scope Contract

- In-scope flows are Home, Source List, Viewer, Output, and Settings.
- Contract applies to user-visible button surfaces in those flows.
- Out-of-scope flows may retain current styling until separately specified.

## 2. Uniform Corner Profile Contract

- A single less-rounded Fluent-aligned corner profile MUST be used for all in-scope button variants.
- The canonical in-scope button corner radius MUST be exactly 8dp.
- Primary/secondary/outlined/tonal button variants in scope MUST not diverge in corner geometry.
- Default, pressed, focused, and disabled states MUST preserve the same corner profile.

## 3. Visual-Only Change Contract

- This feature MUST NOT change button behavior, click handlers, navigation, persistence, or data flow.
- Any detected behavioral change is a release blocker for this feature.

## 4. Architecture Boundary Contract

- Implementation MUST remain in presentation/theme/style resources and UI composition layers.
- Domain and data module contracts MUST remain unchanged.
- Room schema or persistence models MUST NOT be modified.

## 5. Validation Contract

- Validation MUST include flow coverage for all in-scope flows.
- Validation MUST include dark and light theme checks for representative button states.
- Existing Playwright regression profile MUST pass after feature-specific checks.

## 6. Evidence Contract

- Evidence MUST be persisted under `test-results/` with traceable command/output links.
- Evidence records MUST classify outcomes as `Pass`, `CodeFailure`, or `BlockedEnvironment`.
- Blocked outcomes MUST include explicit remediation details and rerun notes.

## 7. Release Readiness Contract

- Merge readiness requires:
	- Pass status for in-scope flow evidence
	- Pass status for existing Playwright regression profile
	- Pass status for release hardening (`:app:verifyReleaseHardening`) or documented environment blocker
