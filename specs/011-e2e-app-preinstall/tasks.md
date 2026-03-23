# Tasks: E2E App Pre-Installation Gate

**Feature**: `011-e2e-app-preinstall`  
**Input**: `specs/011-e2e-app-preinstall/{spec.md,plan.md,research.md,data-model.md,contracts/,quickstart.md}`  
**Generated**: 2026-03-23

**Tests**: Required by constitution. TDD (failing-first) sequencing applied. Playwright spec written before orchestrator implementation. PowerShell Pester tests written before abort/timeout logic.

**Organization**: Tasks grouped by user story. Each story is independently testable. US1 = MVP — implement and verify before starting US2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Parallelizable — different files, no incomplete dependency
- **[US1/2/3]**: User story mapping

---

## Phase 1: Setup

**Purpose**: Ensure runtime artifact directory is tracked and aapt tooling is confirmed available before any implementation begins.

- [ ] T001 Confirm `testing/e2e/artifacts/runtime/` directory is listed in `.gitignore` (or the parent `artifacts/runtime/` is already excluded) so `preinstall-report.json` is never committed — update `.gitignore` if the runtime output directory is not already excluded in `testing/e2e/.gitignore` or `.gitignore`
- [ ] T002 [P] Verify `aapt`/`aapt2` is available on the CI runner by checking `verify-e2e-dual-emulator-prereqs.ps1` — if the script does not already confirm `aapt` availability, add a check for `aapt` in `scripts/verify-e2e-dual-emulator-prereqs.ps1` (required for APK metadata extraction per research Decision 1)
- [ ] T003 [P] Confirm default APK output path `app/build/outputs/apk/debug/app-debug.apk` by running `./gradlew.bat :app:assembleDebug` in a local shell and verifying the artifact is produced at the expected path (research Decision 6 — no code change; validation only)

**Checkpoint**: Runtime directory confirmed, aapt reachable, APK path validated — Phase 2 can begin.

---

## Phase 2: Foundational — ADB Helper Extensions

**Purpose**: Extend `emulator-adb.ps1` with the two new functions required by the orchestrator. Must be complete before Phase 3 orchestrator implementation begins.

**⚠️ CRITICAL**: `install-app-preinstall.ps1` (Phase 3) cannot be written until `Get-InstalledAppVersion` and `Test-AppLaunchable` are present in `emulator-adb.ps1`.

- [ ] T004 Add `Get-InstalledAppVersion` function to `testing/e2e/scripts/helpers/emulator-adb.ps1`: accepts `-Serial <string>` and `-PackageName <string>`; runs `adb -s <serial> shell dumpsys package <packageName>`; parses `versionName=` and `versionCode=` lines; returns `@{ versionName = <string>; versionCode = <int> }`; reuses existing `Invoke-Adb` helper; throws on unexpected errors (per plan Phase A, data-model `EmulatorInstallRecord.installedVersionName/Code`)
- [ ] T005 Add `Test-AppLaunchable` function to `testing/e2e/scripts/helpers/emulator-adb.ps1`: accepts `-Serial <string>`, `-PackageName <string>`, `-ActivityName <string>` (default `com.ndi.app.MainActivity`); runs `adb -s <serial> shell am start -W -n <pkg>/<activity>`; parses output for `Status: ok`; runs `adb -s <serial> shell am force-stop <pkg>` after verification to leave emulator in clean state; returns `[bool]`; reuses `Invoke-Adb` (per plan Phase A, research Decision 2)
- [ ] T006 [P] Write Pester-style PowerShell tests for `Get-InstalledAppVersion` in `testing/e2e/scripts/tests/emulator-adb-version.tests.ps1`: test that the function is defined in `emulator-adb.ps1`, that it parses a known `dumpsys package` output string correctly (mock via stub), and that it throws when ADB returns non-zero (pattern mirrors `testing/e2e/scripts/tests/provision-dual-emulator.tests.ps1`)
- [ ] T007 [P] Write Pester-style PowerShell tests for `Test-AppLaunchable` in `testing/e2e/scripts/tests/emulator-adb-launch.tests.ps1`: test that the function is defined, that it returns `$true` when `am start -W` output contains `Status: ok`, that it returns `$false` on failed launch output, and that it invokes `am force-stop` after any launch attempt

**Checkpoint**: `Get-InstalledAppVersion` and `Test-AppLaunchable` exported from `emulator-adb.ps1` and covered by unit tests — US1 implementation can begin.

---

## Phase 3: User Story 1 — Fresh App Installation Before Test Run (Priority: P1) 🎯 MVP

**Goal**: Before any e2e test executes, the latest debug APK is installed on every registered emulator, version is confirmed, and a Pre-Flight Report is written. CI workflow enforces build → install → test ordering with no bypass.

**Independent Test**: Build a new APK, trigger `install-app-preinstall.ps1`, check `preinstall-report.json` shows `overallStatus: PASS` for both `emulator-5554` and `emulator-5556` with matching `versionName`/`versionCode`.

### Tests for User Story 1 — Write Failing First ⚠️

> Write these before the orchestrator. They must run RED until Phase 3 implementation is complete.

- [ ] T008 [P] [US1] Write TypeScript interfaces in `testing/e2e/tests/support/app-preinstall.ts` matching `contracts/pre-flight-report.contract.md` JSON schema: export `AppBuildArtifact`, `EmulatorInstallRecord`, `PreFlightReport` interfaces with all required fields and status enum literals (`PASS | INSTALL_FAILED | VERSION_MISMATCH | LAUNCH_FAILED | TIMEOUT | UNREACHABLE` for device, `PASS | FAIL` for overall) — these compile but the spec tests below fail until the report file exists
- [ ] T009 [US1] Write `readPreFlightReport(repoRoot: string): PreFlightReport` and `assertPreFlightPass(report: PreFlightReport): void` helper functions in `testing/e2e/tests/support/app-preinstall.ts`: `readPreFlightReport` reads and JSON-parses `testing/e2e/artifacts/runtime/preinstall-report.json`; `assertPreFlightPass` makes Playwright `expect` assertions for `overallStatus === "PASS"`, each device `status === "PASS"`, each device `launchVerified === true`, each device `elapsedMs <= 60000`, and `abortedBeforeInstall === false` (per plan Phase C, data-model PreFlightReport)
- [ ] T010 [P] [US1] Write failing Playwright spec `testing/e2e/tests/support/app-preinstall.spec.ts` tagged `@preinstall` with four test cases: (1) `pre-flight report is present and parseable` — reads report JSON, asserts it is valid; (2) `overall pre-flight status is PASS`; (3) `each device has the correct version installed` — asserts `installedVersionCode` matches `buildArtifact.versionCode`; (4) `each device is launch-verified within 60 seconds` — asserts `launchVerified: true` and `elapsedMs <= 60000` per device; import from `app-preinstall.ts`; tag: `@preinstall` (per plan Phase D, research Decision 9, TDD anchor)

### Implementation for User Story 1

- [ ] T011 [US1] Create `testing/e2e/scripts/install-app-preinstall.ps1` with parameter block: `[string]$ApkPath`, `[string]$PackageName = "com.ndi.app.debug"`, `[string[]]$Serials`, `[int]$TimeoutSeconds = 60`, `[string]$OutputPath`; resolve defaults from env vars `APP_APK_PATH`, `EMULATOR_A_SERIAL`, `EMULATOR_B_SERIAL`; default `$Serials = @("emulator-5554", "emulator-5556")`; default `$OutputPath = "testing/e2e/artifacts/runtime/preinstall-report.json"` relative to repo root (per `contracts/install-script.contract.md` Parameters table)
- [ ] T012 [US1] Implement APK path resolution + existence check in `install-app-preinstall.ps1`: resolve `$ApkPath` to absolute path; if file does not exist, immediately write abort `PreFlightReport` JSON with `abortedBeforeInstall: true`, `overallStatus: "FAIL"`, `failureReason` containing `"Run './gradlew :app:assembleDebug' before executing the e2e test suite."`, empty `devices: []`, then exit 1 (FR-003; see data-model `AppBuildArtifact.exists` validation + contract FAIL example)
- [ ] T013 [US1] Implement pre-install APK metadata extraction in `install-app-preinstall.ps1`: invoke `aapt dump badging <ApkPath>` and parse `package: name=` line to extract `versionName` and `versionCode`; populate `AppBuildArtifact` struct with `path`, `variant = "debug"`, `packageName`, `versionName`, `versionCode`, `buildTimestamp` (APK file last-modified time), `exists = true` (research Decision 1)
- [ ] T014 [US1] Implement per-device install loop in `install-app-preinstall.ps1`: for each serial in `$Serials`, start a `Start-Job` that calls `. testing/e2e/scripts/helpers/emulator-adb.ps1` and `Install-ApkToEmulator -Serial $s -ApkPath $AbsApkPath`; call `Wait-Job -Timeout $TimeoutSeconds`; if job times out call `Remove-Job -Force`; capture install success/failure per device; record per-device start/end time for `elapsedMs` (research Decision 3, FR-008)
- [ ] T015 [US1] Implement post-install version confirmation in `install-app-preinstall.ps1`: after successful install for each device, call `Get-InstalledAppVersion -Serial $s -PackageName $PackageName`; compare returned `versionCode` to `AppBuildArtifact.versionCode`; set `status = "VERSION_MISMATCH"` if codes differ; populate `installedVersionName`, `installedVersionCode` fields in `EmulatorInstallRecord` (FR-006, data-model VERSION_MISMATCH state)
- [ ] T016 [US1] Implement `PreFlightReport` JSON serialization and write in `install-app-preinstall.ps1`: construct report object with `reportId` (new GUID), `timestamp` (UTC ISO 8601), `buildArtifact`, `devices` array, `overallStatus = "PASS"` iff all device statuses are `"PASS"`, `failureReason = $null` on PASS or consolidated device error messages on FAIL, `totalElapsedMs`, `abortedBeforeInstall = $false`; ensure parent directory exists; write via `ConvertTo-Json -Depth 10 | Set-Content` to `$OutputPath`; conform to `contracts/pre-flight-report.contract.md` schema (data-model PreFlightReport, idempotency FR-007)
- [ ] T017 [US1] Emit final stdout summary line in `install-app-preinstall.ps1`: print `PRE-FLIGHT PASS: All N devices verified (versionName=X versionCode=Y)` on exit 0, or `PRE-FLIGHT FAIL: <consolidated reason>` on exit 1; exit with code 0 (PASS) or 1 (FAIL) (per `contracts/install-script.contract.md` Stdout/Stderr Protocol)
- [ ] T018 [US1] Extend `testing/e2e/tests/support/global-setup-dual-emulator.ts` to call `install-app-preinstall.ps1` after the `provision-dual-emulator.ps1` call and before the `start-relay-server.ps1` call: add `runPowerShellScript("../../scripts/install-app-preinstall.ps1")` — non-zero exit propagates as thrown error and aborts global setup (plan Phase E, research Decision 5)
- [ ] T019 [US1] Add `Build app debug APK` step to `.github/workflows/e2e-dual-emulator.yml` after `Build NDI bridge release APK` and before `Validate emulator images (32-35)`: `shell: pwsh`, `run: ./gradlew.bat :app:assembleDebug`, no `if:` condition (FR-009 — no bypass; plan Phase F, research Decision 8)
- [ ] T020 [US1] Add `Install app on emulators` step to `.github/workflows/e2e-dual-emulator.yml` after `Provision dual emulators` and before `Start relay server`: `shell: pwsh`, `run: ./testing/e2e/scripts/install-app-preinstall.ps1`, no `if:` condition (FR-009 — no bypass; plan Phase F)
- [ ] T021 [US1] Add `Run app pre-flight spec` step to `.github/workflows/e2e-dual-emulator.yml` after `Run support validation specs` and before `Run latency consumer suite (feature 009)`: `shell: pwsh`, `working-directory: testing/e2e`, `run: npx playwright test tests/support/app-preinstall.spec.ts --project=android-primary`, no `if:` condition (plan Phase F, quickstart CI step 14)

### Verification for User Story 1

- [ ] T022 [US1] Run `testing/e2e/tests/support/app-preinstall.spec.ts` locally (`npx playwright test tests/support/app-preinstall.spec.ts --project=android-primary`) against the completed implementation and confirm all four `@preinstall` tests pass (TDD green phase)
- [ ] T023 [P] [US1] Contract conformance — `pre-flight-report.contract.md`: confirm `preinstall-report.json` produced by a PASS run matches the JSON Schema in `contracts/pre-flight-report.contract.md` for all required fields, types, and enum values (verify against the "Successful Pre-Flight Run" example)
- [ ] T024 [P] [US1] Contract conformance — `install-script.contract.md` exit codes + stdout: confirm exit code is `0` on PASS and stdout final line matches `PRE-FLIGHT PASS: All 2 devices verified (versionName=... versionCode=...)` pattern; confirm the step ordering in `.github/workflows/e2e-dual-emulator.yml` matches the "Caller Contract (CI Step)" table in `contracts/install-script.contract.md`

**Checkpoint**: US1 complete — `app-preinstall.spec.ts` passes, CI workflow has build + install steps, report conforms to schema. US2 implementation can begin.

---

## Phase 4: User Story 2 — Installation Failure Aborts the Run with a Clear Error (Priority: P2)

**Goal**: If the APK is missing, an emulator is unreachable, or installation exceeds 60 seconds, the run is aborted immediately with a machine-readable + human-readable error that identifies device and cause. No test ever executes in a bad-artifact state.

**Independent Test**: Launch `install-app-preinstall.ps1` with no APK on disk — verify run aborts with exit 1, `preinstall-report.json` has `abortedBeforeInstall: true`, `failureReason` references the missing artifact path, and no test cases execute.

### Tests for User Story 2 — Write Failing First ⚠️

> Write these PowerShell unit tests before implementing abort/timeout logic in the orchestrator. They must run RED until Phase 4 implementation is complete.

- [ ] T025 [P] [US2] Write failing PowerShell test for missing-APK abort in `testing/e2e/scripts/tests/install-app-preinstall.tests.ps1`: test that the script file defines a guard block that writes `abortedBeforeInstall = $true` and exits 1 when the APK does not exist; use static-analysis / content matching approach (mirrors `provision-dual-emulator.tests.ps1` pattern — check file content with `Should Match`)
- [ ] T026 [P] [US2] Write failing PowerShell test for UNREACHABLE emulator status in `testing/e2e/scripts/tests/install-app-preinstall.tests.ps1`: test that the script content handles `UNREACHABLE` status and populates `errorMessage` identifying the device serial (static-analysis content match)
- [ ] T027 [P] [US2] Write failing PowerShell test for TIMEOUT status enforcement in `testing/e2e/scripts/tests/install-app-preinstall.tests.ps1`: test that the script uses `Wait-Job -Timeout` and `Remove-Job -Force` pattern for per-device job isolation, and that `TIMEOUT` status is assigned when the job exceeds the limit (static-analysis content match)

### Implementation for User Story 2

- [ ] T028 [US2] Implement `UNREACHABLE` device detection in `install-app-preinstall.ps1`: before starting the install job for each device, call `Test-AdbDeviceConnected -Serial $s` (already in `emulator-adb.ps1`); if device is not connected, record `EmulatorInstallRecord` with `reachable: false`, `apkInstalled: false`, `launchVerified: false`, `status: "UNREACHABLE"`, `errorMessage: "<serial>: Device not reachable via ADB. Verify emulator is running and 'adb devices' lists it."` — do not attempt install on unreachable devices (FR-004, data-model state UNREACHABLE)
- [ ] T029 [US2] Implement TIMEOUT status in `install-app-preinstall.ps1`: when `Wait-Job -Timeout $TimeoutSeconds` fires before job completion, call `Remove-Job -Force` on the job, set `elapsedMs = ($TimeoutSeconds * 1000) + 1` (or wall-clock elapsed), and record `status: "TIMEOUT"`, `errorMessage: "<serial>: Installation exceeded <N>-second limit. ADB install job timed out. Verify emulator storage availability and ADB connectivity."` (FR-008, data-model state TIMEOUT, contract TIMEOUT example)
- [ ] T030 [US2] Implement partial failure abort in `install-app-preinstall.ps1`: after processing all devices, collect non-PASS records; if any exist, set `overallStatus: "FAIL"`, join all `errorMessage` values into `failureReason` with device serial prefixes; exit 1; ensure stdout final line is `PRE-FLIGHT FAIL: <consolidated reason>` (FR-004, spec US2 acceptance scenario 3)
- [ ] T031 [US2] Implement `INSTALL_FAILED` capture in `install-app-preinstall.ps1`: when the `Install-ApkToEmulator` job exits with a thrown error (non-zero ADB exit), catch the exception; set `status: "INSTALL_FAILED"`, `apkInstalled: false`, `errorMessage: "<serial>: adb install failed — <error text>"` (FR-004, data-model state INSTALL_FAILED)

### Verification for User Story 2

- [ ] T032 [US2] Verify US2 acceptance scenario 1 (missing APK): delete or rename `app/build/outputs/apk/debug/app-debug.apk`, run `install-app-preinstall.ps1`, confirm exit code 1, `abortedBeforeInstall: true`, `failureReason` contains `assembleDebug` reference, no test cases run
- [ ] T033 [US2] Verify US2 acceptance scenario 2 (unreachable emulator): simulate unreachable emulator by passing a non-existent serial (`-Serials @("emulator-9999")`), confirm exit 1, report `status: "UNREACHABLE"` with device serial in `errorMessage`
- [ ] T034 [P] [US2] Contract conformance — `pre-flight-report.contract.md` FAIL examples: confirm abort report (missing artifact) and timeout report match the JSON Schema for FAIL cases — all enum values, nullable fields, and `abortedBeforeInstall` boolean match exactly
- [ ] T035 [P] [US2] Contract conformance — `install-script.contract.md` error stdout: confirm `PRE-FLIGHT FAIL:` final stdout line appears on all FAIL scenarios and includes actionable text matching SC-004 (5-minute-resolution target)

**Checkpoint**: US2 complete — abort scenarios handled, error messages conform to contracts. US3 implementation can begin.

---

## Phase 5: User Story 3 — Post-Installation Launch Verification (Priority: P3)

**Goal**: After APK install succeeds, the harness confirms the app can actually launch on each emulator before handing control to tests. Corrupt or incomplete installs that appear to succeed are caught and abort the run with a distinct `LAUNCH_FAILED` error (not `INSTALL_FAILED`).

**Independent Test**: Corrupt a simulated install by manually placing an invalid APK (which ADB installs without error but whose activity fails to start), run `install-app-preinstall.ps1`, confirm exit 1, `status: "LAUNCH_FAILED"` (distinct from `INSTALL_FAILED`), and no test executes.

### Tests for User Story 3 — Write Failing First ⚠️

> Extend the existing `app-preinstall.spec.ts` and write a new PowerShell test before implementing launch verification in the orchestrator.

- [ ] T036 [P] [US3] Add `LAUNCH_FAILED` scenario test to `testing/e2e/scripts/tests/install-app-preinstall.tests.ps1`: test that the script content invokes `Test-AppLaunchable` and records `status = "LAUNCH_FAILED"` with a distinct error message string (static-analysis content match; runs RED until T039)
- [ ] T037 [P] [US3] Add failing test to `testing/e2e/tests/support/app-preinstall.spec.ts`: add a test case `each device launch error distinction` — assert that when `launchVerified: false`, the `status` field is `"LAUNCH_FAILED"` (not `"INSTALL_FAILED"`); this test is contingent on a FAIL report fixture and can be skipped in standard pass-path runs (annotate with `test.skip` until a fixture is available, converting to a fixture-based test)

### Implementation for User Story 3

- [ ] T038 [US3] Integrate `Test-AppLaunchable` into per-device post-install sequence in `install-app-preinstall.ps1`: after successful install and version confirmation, call `Test-AppLaunchable -Serial $s -PackageName $PackageName`; if it returns `$false`, record `status: "LAUNCH_FAILED"`, `launchVerified: false`, `errorMessage: "<serial>: App installed but failed to launch. Check for corrupt APK or missing activity declaration. Distinct from installation failure."` — force-stop is handled inside `Test-AppLaunchable` (FR-005, research Decision 2)
- [ ] T039 [US3] Ensure `launchVerified: true` is only set in `EmulatorInstallRecord` when `Test-AppLaunchable` returns `$true` and overall status becomes `PASS`; ensure `launchVerified: false` is set for all non-PASS final states (data-model `EmulatorInstallRecord.launchVerified` semantics + FR-005)
- [ ] T040 [US3] Add `launchVerified` assertion to `assertPreFlightPass` in `testing/e2e/tests/support/app-preinstall.ts`: confirm `expect(device.launchVerified).toBe(true)` is included in the helper function (should already be present from T009; if omitted, add it; verify spec test T010 case 4 still passes)

### Verification for User Story 3

- [ ] T041 [US3] Verify US3 acceptance scenario 1: run full install sequence on a working emulator with valid APK; confirm `launchVerified: true` for all devices in the report and `app-preinstall.spec.ts` case 4 passes
- [ ] T042 [US3] Verify US3 acceptance scenario 3: confirm test report records `pre-flight verification succeeded` by checking `overallStatus === "PASS"` in the report consumed by the Playwright spec
- [ ] T043 [P] [US3] Contract conformance — distinct `LAUNCH_FAILED` error message: confirm the `errorMessage` text for `LAUNCH_FAILED` status is clearly different from `INSTALL_FAILED` text (per spec US3 acceptance scenario 2 — the errors are "distinct"); verify the distinction is observable in the `preinstall-report.json` output
- [ ] T044 [P] [US3] Contract conformance — `pre-flight-report.contract.md` status enum: confirm `"LAUNCH_FAILED"` is one of the six valid `status` enum values in the generated report and that no out-of-enum strings are ever written by the orchestrator

**Checkpoint**: All three user stories complete. Full end-to-end: build → install → launch-verify → report. Tests pass for all happy paths.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Idempotency guarantee, regression gate for existing tests, no-bypass enforcement, artifact collection, and documentation.

- [ ] T045 Verify FR-007 idempotency: run `install-app-preinstall.ps1` twice in succession with the same APK (no rebuild between runs) on both emulators; confirm both runs exit 0, the `reportId` fields differ between runs (UUID changes), and all version/status fields are identical — confirms `-r` flag on `adb install` enables forced replacement without error
- [ ] T046 Verify FR-009 no-bypass enforcement: inspect `.github/workflows/e2e-dual-emulator.yml` and confirm that neither `Build app debug APK` (T019) nor `Install app on emulators` (T020) nor `Run app pre-flight spec` (T021) has an `if:` condition — all three steps are unconditional; record this in the PR description
- [ ] T047 Verify FR-010 regression gate: run `npx playwright test tests/support/e2e-infrastructure.spec.ts tests/support/dual-emulator-provisioning.spec.ts tests/support/relay-connectivity.spec.ts --project=android-primary` and confirm all existing support validation specs continue to pass after the global-setup extension (T018) is merged; record results in `test-results/android-test-results.md`
- [ ] T048 [P] Verify SC-001 end-to-end: trigger the full CI workflow (or a local run via `run-dual-emulator-e2e.ps1`) and confirm from the `preinstall-report.json` artifact that both `emulator-5554` and `emulator-5556` show `status: "PASS"`, `versionName`/`versionCode` logged, and no manual steps were required
- [ ] T049 [P] Verify SC-004 operator actionability: for each of the three failure scenarios (missing APK, UNREACHABLE, TIMEOUT), confirm the `failureReason` in the report plus the stdout `PRE-FLIGHT FAIL:` line is sufficient for a developer to identify and resolve the cause within 5 minutes without inspecting raw device logs
- [ ] T050 Update `testing/e2e/README.md` (if the file exists) or `docs/testing.md` to reference the pre-installation gate: add a section describing the pre-flight step, default APK path, how to skip automation locally (`DUAL_EMULATOR_AUTOMATION=0`), and a reference to `specs/011-e2e-app-preinstall/quickstart.md` for full details

---

## Dependencies

```
Phase 1 (Setup)
  └── Phase 2 (Foundational — ADB Extensions)
        └── Phase 3 (US1 — Install Gate + CI workflow) [MVP]
              ├── Phase 4 (US2 — Failure Abort + Error Messages)
              │     └── Phase 5 (US3 — Launch Verification)
              │           └── Phase 6 (Polish)
              └── Phase 6 (Polish) [partial — T047 regression gate]
```

**Independent story execution after Phase 2**:
- US1 can be implemented and verified independently (Phases 2→3)
- US2 extends US1 error paths (Phase 4 depends on Phase 3 orchestrator scaffold)
- US3 adds a post-install step to the US1 orchestrator (Phase 5 depends on Phase 3)

---

## Parallel Execution Per Story

### Phase 3 (US1) Parallel Opportunities

```
T008 [P] app-preinstall.ts interfaces      ||  T010 [P] app-preinstall.spec.ts (TDD)
T009     readPreFlightReport/assertPasst   ||  (depends on T008)
─────────────────────────────────────────────────────────────────────────────
T011 orchestrator param block              →  (sequential: T012 → T013 → T014 → T015 → T016 → T017)
T018 global-setup extension            [P after T017]
T019 CI: assembleDebug step            [P after T017]
T020 CI: install step                  [P after T017]
T021 CI: pre-flight spec step          [P after T017]
─────────────────────────────────────────────────────────────────────────────
T022 run spec (verify green)           →  (after T011–T021)
T023 [P] contract conformance report   ||  T024 [P] contract conformance CI/stdout
```

### Phase 4 (US2) Parallel Opportunities

```
T025 [P] missing-APK test    ||  T026 [P] UNREACHABLE test  ||  T027 [P] TIMEOUT test
─────────────────────────────────────────────────────────────────────────────
T028 UNREACHABLE impl        ||  T029 TIMEOUT impl          ||  T031 INSTALL_FAILED
T030 partial failure abort   →  (after T028, T029, T031 all done)
─────────────────────────────────────────────────────────────────────────────
T032 verify scenario 1       ||  T033 verify scenario 2
T034 [P] contract FAIL       ||  T035 [P] contract stdout
```

### Phase 5 (US3) Parallel Opportunities

```
T036 [P] LAUNCH_FAILED test  ||  T037 [P] spec fixture test
─────────────────────────────────────────────────────────────────────────────
T038 integrate Test-AppLaunchable  →  T039 launchVerified flag  →  T040 assertPreFlightPass
─────────────────────────────────────────────────────────────────────────────
T041 verify scenario 1       ||  T042 verify scenario 3
T043 [P] contract distinction ||  T044 [P] enum conformance
```

---

## Implementation Strategy

**MVP Scope (US1 only — Phases 1–3)**  
Implement Phases 1–3 and merge. This alone satisfies SC-001 (fresh install guarantee) and FR-009 (CI enforcement). US2 and US3 harden the failure paths.

**Incremental Delivery**  
1. Phase 1–2: ADB extensions — no user-visible change, safe to merge immediately  
2. Phase 3: Full US1 + CI workflow — the core value; all new CI steps added  
3. Phase 4: US2 abort hardening — error clarity improvement on top of US1  
4. Phase 5: US3 launch verification — defensive check on top of US1 install  
5. Phase 6: Cross-cutting — verification and documentation pass  

**TDD Sequence Per Story**  
- US1: T008–T010 (write failing spec) → T011–T021 (make it pass) → T022–T024 (verify green + contracts)  
- US2: T025–T027 (write failing PS tests) → T028–T031 (make them pass) → T032–T035 (verify + contracts)  
- US3: T036–T037 (write failing tests) → T038–T040 (make them pass) → T041–T044 (verify + contracts)  

---

## Contract Conformance Summary

| Contract | Verified By | Task(s) |
|----------|-------------|---------|
| `pre-flight-report.contract.md` — PASS schema | T023 | Phase 3 verification |
| `pre-flight-report.contract.md` — FAIL/abort schema | T034 | Phase 4 verification |
| `pre-flight-report.contract.md` — status enum completeness | T044 | Phase 5 verification |
| `install-script.contract.md` — exit codes + stdout | T024 | Phase 3 verification |
| `install-script.contract.md` — error stdout on FAIL | T035 | Phase 4 verification |
| `install-script.contract.md` — caller step ordering | T024 | Phase 3 verification |
| `install-script.contract.md` — idempotency guarantee | T045 | Phase 6 |

---

## Format Validation

All tasks follow `- [ ] [ID] [P?] [Story?] Description with file path` format:
- Checkboxes: ✅ all present  
- Task IDs: ✅ T001–T050, sequential  
- [P] markers: ✅ applied only to parallelizable tasks (different files, no incomplete deps)  
- [Story] labels: ✅ present on all user-story phase tasks; absent on Setup, Foundational, Polish phases  
- File paths: ✅ every implementation/test task includes an explicit file path  
- Total task count: **50 tasks**  
