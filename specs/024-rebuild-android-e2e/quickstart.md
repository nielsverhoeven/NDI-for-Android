# Quickstart: Rebuild Android E2E Suite

## 1. Prerequisites

- Windows environment with PowerShell available.
- Android SDK configured (`ANDROID_SDK_ROOT` or `ANDROID_HOME`).
- Node.js 20 for Playwright harness commands.
- Repository checked out and dependencies installable.

## 2. Environment Preflight

Run from repository root:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1 -CiMode -AllowMissingNdiSdk
pwsh ./scripts/verify-e2e-dual-emulator-prereqs.ps1 -AllowMissingNdiSdk
```

If any check fails, stop and resolve before executing e2e.

## 3. Install E2E Dependencies

```powershell
npm --prefix testing/e2e ci
npm --prefix testing/e2e exec playwright install --with-deps
```

## 4. Run Rebuilt Suite Locally

Primary gate profile:

```powershell
npm --prefix testing/e2e run test:pr:primary
```

Dual-emulator preflight only:

```powershell
powershell -ExecutionPolicy Bypass -File ./testing/e2e/scripts/run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556 -PreflightOnly
```

Dual-emulator execution:

```powershell
powershell -ExecutionPolicy Bypass -File ./testing/e2e/scripts/run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556
```

## 5. CI Verification (GitHub Actions)

Validate workflow wiring by confirming:

- [android-ci.yml](../../.github/workflows/android-ci.yml) runs primary e2e gate.
- [e2e-dual-emulator.yml](../../.github/workflows/e2e-dual-emulator.yml) can execute dual-emulator path.
- Artifacts are uploaded for pass/fail/blocked triage.

## 6. Developer Mode Target Policy Check

- On developer-mode-enabled targets: developer mode scenarios must run and gate.
- On non-capable targets: developer mode scenarios must report not-applicable, without failing settings/navigation core gates.

## 7. Expected Outputs

- JSON result files and markdown summaries under [testing/e2e/artifacts](../../testing/e2e/artifacts).
- CI artifact uploads containing scenario-level evidence and aggregate summaries.
