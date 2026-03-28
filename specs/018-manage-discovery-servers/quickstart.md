# Quickstart: Discovery Server Settings Management Validation

## 1. Prerequisites

- Windows host with Android SDK tooling configured.
- Repository baseline Java/JBR toolchain available.
- NDI SDK prerequisites installed per docs/android-prerequisites.md.
- At least one emulator/device for submenu validation; dual emulators recommended for discovery behavior validation.

## 2. Preflight

Run prerequisite validation before feature tests:

```powershell
./scripts/verify-android-prereqs.ps1
```

Optional dual-emulator preflight:

```powershell
./scripts/verify-e2e-dual-emulator-prereqs.ps1 -AllowMissingNdiSdk
```

If preflight fails, mark the gate as environment-blocked and record concrete unblocking steps.

## 3. Build

```powershell
./gradlew.bat :app:assembleDebug
```

## 4. Test-First Execution (Red-Green-Refactor)

1. Add failing unit tests first for:
   - port defaulting to 5959 when omitted
   - duplicate (hostOrIp + port) prevention
   - enabled-state persistence after restart path
   - ordered failover attempt behavior with mixed reachability
2. Implement minimal code to pass tests.
3. Refactor safely while keeping tests green.

Suggested test targets:

```powershell
./gradlew.bat :feature:ndi-browser:data:testDebugUnitTest
./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest
./gradlew.bat :core:database:testDebugUnitTest
```

## 5. Playwright Emulator Validation

Install and prepare e2e dependencies:

```powershell
npm --prefix testing/e2e ci
npm --prefix testing/e2e run preflight:dual-emulator
```

Run feature-focused e2e coverage:

```powershell
npm --prefix testing/e2e run test -- tests/settings-discovery-submenu.spec.ts
```

Run primary regression gate:

```powershell
npm --prefix testing/e2e run test:pr:primary
```

## 6. Manual Acceptance Spot Checks

- Discovery Servers submenu is reachable from Settings.
- Add server form uses separate hostname-or-ip and port inputs.
- Blank port saves as 5959.
- Multiple servers can be added and persist across restart.
- Per-server toggle on or off persists across restart.
- Runtime behavior attempts enabled servers in order and fails over when first enabled server is unreachable.

## 7. Material 3 and Architecture Checks

- Verify updated settings and submenu UI follow Material 3 component patterns already used in repository settings/theme flows.
- Confirm Fragment -> ViewModel -> Repository flow with no business logic in Fragment.

## 8. Release Hardening Gate

```powershell
./gradlew.bat :app:verifyReleaseHardening :app:assembleRelease
```

## 9. Evidence Capture

Collect and retain:

- `testing/e2e/artifacts/**`
- `testing/e2e/playwright-report/**`
- module unit test reports under `**/build/reports/tests/**`

For blocked gates, classify as environment blocker and include exact retry/unblock commands.
