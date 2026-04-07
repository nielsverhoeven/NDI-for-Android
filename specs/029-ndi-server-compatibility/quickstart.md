# Quickstart: Validate NDI Discovery Server Compatibility

## 1. Preconditions
1. Run Android/tooling preflight:
   - pwsh ./scripts/verify-android-prereqs.ps1
   - ./gradlew.bat --version
2. Confirm target device/emulator availability:
   - adb devices
3. Confirm each target discovery endpoint is reachable enough to test.

## 2. Prepare Validation Targets
1. Register targets:
   - latest known-good server (baseline)
   - failing venue server version
   - each additional older obtainable server version
2. Ensure each target has at least one available NDI source.

## 3. Execute Compatibility Validation Per Target
1. Run discovery against target.
2. Record discovered source visibility result.
3. Attempt stream start for one discovered source.
4. Classify target status:
   - compatible: discovery + stream start succeed
   - limited: discovery works but validated support is discovery-only
   - incompatible: stream start fails in otherwise ready environment
   - blocked: validation cannot complete due to environment/endpoint blocker
5. Record evidence reference and recommended next step for non-compatible outcomes.

## 4. Mixed-Server Validation
1. Configure mixed server endpoints (compatible + non-compatible targets).
2. Verify compatible sources remain discoverable.
3. Verify non-compatible targets are reported as partial/failed compatibility outcomes.
4. Verify overall result is not reported as fully successful when any target is incompatible or blocked.

## 5. Regression and Quality Gates
1. Follow Red-Green-Refactor for any new/changed tests.
2. Preserve existing tests unless directly impacted by this feature.
3. If visible diagnostics rendering changes, run affected Playwright e2e and existing regression suite.
4. Run release hardening verification for release-impacting changes.

## 6. Blocked Result Handling
If any gate is blocked by environment constraints:
1. Mark status blocked.
2. Capture reproducible evidence (failed preflight, endpoint status, command output).
3. Record concrete unblock step and retry command.
