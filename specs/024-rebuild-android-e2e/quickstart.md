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

Validated command-contract path (preflight + primary gate):

```powershell
pwsh ./testing/e2e/scripts/validate-command-contract.ps1 -Execute
pwsh ./testing/e2e/scripts/run-primary-pr-e2e.ps1 -Profile pr-primary
```

Matrix profile execution:

```powershell
npm --prefix testing/e2e run test:matrix
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

## 7. Playwright Agent Workflow (Required)

- Use Playwright planner agent to produce scenario planning output for each scenario-authoring user story (US1, US2, US3). US4 (CI wiring) does not require planner or generator entries.
- Use Playwright generator agent to create/update `.spec.ts` scenario files from approved plans (US1, US2, US3).
- Use Playwright healer agent when runs fail to generate remediation actions and updated evidence.
- Save agent outputs in `test-results/024-*.md` or `testing/e2e/artifacts/` for auditability.

## 8. Reliability and Triage Verification

- Confirm reliability artifact reports latest 20 unchanged-code required-profile runs and at least 19 nondeterministic-failure-free outcomes.
- For failed runs, confirm triage artifact includes failure timestamp and first classification timestamp within 15 minutes.

## 9. Expected Outputs

- JSON result files and markdown summaries under [testing/e2e/artifacts](../../testing/e2e/artifacts).
- CI artifact uploads containing scenario-level evidence and aggregate summaries.
