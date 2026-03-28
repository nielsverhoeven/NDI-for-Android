# Validation Report: Discovery Server Settings Management (018)

**Feature**: 018-manage-discovery-servers  
**Date**: 2026-03-28  
**Status**: IN PROGRESS

---

## Phase 0 Preflight

See `test-results/018-manage-discovery-servers-preflight.md` — **PASS**

---

## US1 — Add Discovery Servers from Settings

### Playwright Regression Baseline (T020)

_Results to be filled in after emulator is available._

### Blocked Gate Evidence (T021)

_N/A — environment is ready; emulator must be started separately._

---

## US2 — Manage Multiple Discovery Servers

### Playwright Regression Baseline (T032)

_Results to be filled in after emulator is available._

### Blocked Gate Evidence (T033)

_N/A_

---

## US4 — Edit and Remove Discovery Servers

### Playwright Regression Baseline (T059)

_Results to be filled in after emulator is available._

### Blocked Gate Evidence (T060)

_N/A_

---

## US3 — Enable and Disable Individual Servers

### Playwright Regression Baseline (T042)

_Results to be filled in after emulator is available._

### Blocked Gate Evidence (T043)

_N/A_

---

## Phase 6: Final Validation

### Unit Tests (T050)

Command:

`./gradlew.bat :core:database:testDebugUnitTest :feature:ndi-browser:data:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest :app:testDebugUnitTest`

Result: **PASS**

- Build status: `BUILD SUCCESSFUL`
- Notes: command incremented `version.properties` as part of repo build hooks.

### Playwright Feature Coverage (T051)

Command:

`npm --prefix testing/e2e run test -- tests/settings-discovery-submenu.spec.ts`

Result: **BLOCKED (environment/tooling drift)**

- Preflight and dual-emulator provisioning succeeded.
- Failure point: pre-install script failed APK install on both emulators.
- Error excerpt: `PRE-FLIGHT FAIL: ... INSTALL_FAILED ... expected=0.5.14+101`
- Artifact session: `testing/e2e/artifacts/playwright-1774728347385`

### Release Hardening (T052)

Command:

`./gradlew.bat :app:verifyReleaseHardening :app:assembleRelease`

Result: **PASS**

- Build status: `BUILD SUCCESSFUL`
- Notes: command incremented `version.properties` as part of repo build hooks.

### Material 3 Compliance (T053)

Result: **PASS (manual verification)**

- Discovery submenu uses Material 3 `MaterialToolbar`, `TextInputLayout`, `MaterialButton`, and `MaterialSwitch` patterns.
- Validation and warning states are rendered in-context, consistent with Material guidance.
- Navigation and action affordances are aligned with existing Settings visual language.

### Final Gate Blockers (T054)

Status: **Documented**

Blocker classification: environment/tooling drift (not functional feature regression).

- Symptom: pre-install expected app version (`0.5.14+101`) did not align with currently built APK metadata after auto-increment hooks.
- Reproduction command: `npm --prefix testing/e2e run test -- tests/settings-discovery-submenu.spec.ts`
- Unblock steps:
	1. Align `version.properties` with the version expected by pre-install scripts before running e2e.
	2. Rebuild the target APK.
	3. Re-run the feature Playwright command.
