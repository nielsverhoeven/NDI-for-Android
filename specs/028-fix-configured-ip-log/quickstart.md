# Quickstart: Validate Developer Log Configured Address Display

## 1. Preflight
1. Run prerequisite checks:
- `./scripts/verify-android-prereqs.ps1`
- `./scripts/verify-e2e-dual-emulator-prereqs.ps1`
2. Confirm an authorized device/emulator is connected:
- `adb devices`

If preflight fails, record result as `Blocked - Environment`, capture command output, and unblock before proceeding.

## 2. Build and Install
1. Build/install debug app:
- `./gradlew.bat :app:installDebug`

## 3. Manual Verification (View Screen)
1. Enable developer mode in app settings.
2. Configure addresses including representative values:
- IPv4 (example: `192.168.1.10`)
- IPv6 (example: `ff02::1`)
- Hostname (example: `ndi-host.local`)
3. Open View screen and trigger relevant log emission.
4. Verify logs show actual configured addresses (not redacted placeholders).
5. Disable developer mode and verify configured-address developer log output is hidden.
6. Configure malformed/empty values and verify fallback "not configured" behavior.

## 4. Automated Validation
1. Add/execute Playwright tests for:
- Developer mode ON with single address.
- Developer mode ON with multiple mixed-format addresses.
- Developer mode OFF suppression behavior.
2. Run existing Playwright suite and confirm zero regressions.

## 5. Evidence
- Save command outputs and test reports.
- Distinguish `code failure` vs `environment blocker` for any failed gate.
