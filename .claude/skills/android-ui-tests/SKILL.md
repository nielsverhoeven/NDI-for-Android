---
name: android-ui-tests
description: Run, dispatch, and heal the Appium UI e2e suite (tests/MauiApp.UITests) for this repository — locally against a connected device/emulator or via the emulator-tests.yml CI workflow. Use when asked to 'run the UI tests', 'run e2e tests', 'dispatch the emulator workflow', 'check the UI e2e gate', or before opening/merging any PR whose base is main.
allowed-tools: Bash
---

## Purpose

Provide one canonical, repeatable procedure for running the Appium UI e2e suite that gates every
PR into `main` (issue #362), reading its results, and healing the most common failure shapes —
without inventing flags or env vars that the fixture does not actually read.

Use this skill when:
- a PR's base is (or will be) `main` and no e2e run link exists for the branch yet
- the user asks to run, dispatch, or check the UI e2e suite
- a UI test fails and the failure needs triage before deciding test-fix vs. production-fix
- a task restructured XAML and a `TestIds` AutomationId may have moved without its page object

---

## The gate rule (non-negotiable)

Before opening or merging a PR whose base is `main`, `tests/MauiApp.UITests` must be green —
**either**:
- (A) a dispatched `emulator-tests.yml` run on the branch, with its run link recorded in the PR, or
- (B) the PR's own `e2e-tests` job ("Run Emulator UI Tests" in `ndi-for-android-cicd.yml`), which
  runs automatically because its `if:` condition matches `github.base_ref == 'main'`.

Never merge while that check (or any check) is still pending or failing — waiting for it to
finish is part of the gate, not an optional courtesy. This is what PR #299 skipped: it merged
while its own run was pending, the run then failed, and it blocked the next release (#361).

When a restructure moves or renames a control, its `TestIds` AutomationId
(`src/Core/Testing/TestIds.cs`) moves with it in the **same** PR, and the page object(s) under
`tests/MauiApp.UITests/Pages/` that reference it are re-run before merge (#342 skipped this; the
gap was only found later, in #360).

---

## Option A — Local run (fastest inner loop)

Needs a connected device or emulator, and Appium.

### 1. One-time setup
```powershell
npm install -g appium
appium driver install uiautomator2
```

### 2. Start the Appium server (its own terminal — it must keep running)
```powershell
appium
```
Default port `4723`, matching `AppiumDriverFixture`'s default `APPIUM_SERVER_URL`
(`http://127.0.0.1:4723/`) — no env var needed unless the server runs elsewhere.

### 3. Build the app and point the suite at the signed APK
```powershell
dotnet build src/MauiApp/NdiForAndroid.csproj -f net10.0-android -c Debug
$env:ANDROID_APK_PATH = "src/MauiApp/bin/Debug/net10.0-android/com.ndi.android-Signed.apk"
```
Always the **`-Signed.apk`** — the unsigned variant fails to install
(`INSTALL_PARSE_FAILED_NO_CERTIFICATES`). The Debug build embeds its managed assemblies
(`EmbedAssembliesIntoApk=true` in `NdiForAndroid.csproj`), so a plain `dotnet build` is sufficient
for the installed app to reflect the current branch. If the installed .NET SDK does not expose
the `maui-android` workload to MSBuild on this machine, set `DOTNETSDK_WORKLOAD_MANIFEST_ROOTS`
to a manifest set that does before building.

### 4. Run the suite
```powershell
dotnet test tests/MauiApp.UITests
```
Leave `E2E_REQUIRE_DEVICE` unset locally — an unavailable device then produces a **skip**, not a
failure. Set it to `true` only if you specifically want a missing device to fail the run (this is
always `true` in CI). Other env vars the fixture reads, both optional:
- `E2E_ARTIFACT_DIR` — where per-failure screenshots/page-source land (default `./e2e-artifacts`).
- `A11Y_MAX_VIOLATIONS` — accessibility budget (default `200`; a ratchet — lower it as violations
  are fixed, never raise it to turn a red run green).

---

## Option B — Dispatch on CI

Use when no device is attached, or to produce the run link a PR into `main` needs.

```powershell
gh workflow run emulator-tests.yml --repo nielsverhoeven/NDI-for-Android --ref <branch> -f app_ref=<branch-or-sha> -f test_filter="<dotnet test filter or blank>"
```
- `app_ref` — blank builds the APK from `<branch>` itself; set it to an older commit only when
  proving a regression test would have caught its bug (pair with `-f expect_failure=true`, which
  inverts the result so the run is green only when the test *fails* against that older build).
- `test_filter` — a `dotnet test --filter` expression; blank runs everything.

Then wait for it — do not treat dispatch itself as the gate satisfied:
```powershell
gh run watch <run-id>
```

### Reading a failed run
```powershell
gh run view <run-id> --log-failed     # prints only the failing steps' logs
gh run download <run-id>              # evidence artifacts, see below
```
Evidence artifacts uploaded by every run:
| Artifact | Contents |
|---|---|
| `emulator-test-results` | The TRX (pass/fail/skip counters and per-test outcome). |
| `emulator-diagnostics` | Appium/logcat logs, plus `FailureEvidence`'s per-failure screenshot + view-hierarchy dump under `test-results/failure-evidence/`. |

If the run's own workflow log ends with "No `$LOG` produced — Appium never started" or a crash
buffer, or the failure shape looks like a driver/session problem rather than an app assertion,
see `/android-ci-failure-patterns` (Failure Classes 1, 2, 4, 5 cover Fast Deployment aborts,
signature mismatches, the vacuous-green skip trap, and the `crc64…MainActivity` activity-wait
mismatch) before treating it as a test bug.

For a PR's own automatic run instead of a dispatched one:
```powershell
gh pr checks <pr-number> --watch
```

---

## Healing a failure

1. Start with the TRX/console failure message and the `FailureEvidence` screenshot + view
   hierarchy for that test (`E2E_ARTIFACT_DIR` locally, the `emulator-diagnostics` artifact in CI).
2. A locator timeout also inlines every automation id that *was* on screen at failure time
   (`FailureEvidence.DescribeVisibleIds`) — check that list before assuming anything.
3. Decide which of these it is:
   - **The page object's locator drifted** (renamed constant, wrong `By.Id`) — fix the page object
     in `tests/MauiApp.UITests/Pages/`. Test-project-only change.
   - **A `TestIds` AutomationId was dropped or never applied** to the XAML element after a
     restructure — this is a production fix (`src/Core` constant + the XAML's
     `AutomationId="{x:Static t:TestIds.X}"`); delegate to `implementer` via the main session,
     then re-run the affected page object's tests in the same PR.
   - **The test itself asserts the wrong thing** (API/behaviour changed intentionally) — fix the
     test, not production code.
4. Re-run the single failing test, then the full suite, to confirm the fix and check for
   regressions before recording the run link.

---

## Reporting back

When this skill is used to satisfy the gate for a PR, report:
- which option was used (A: local, or B: CI dispatch)
- the run link (CI) or a summary of local pass/fail/skip counts
- any healed test, with what changed and why
- confirmation that the run link is recorded in the PR before it merges
