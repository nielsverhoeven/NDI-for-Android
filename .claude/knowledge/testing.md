<!-- Tester knowledge index. Created 2026-09-05 (issue #362). -->

# Testing knowledge (index)

**This file is deliberately thin.** The canonical, committed testing sources for this repo are:

| Source | Role |
|---|---|
| `CLAUDE.md` | Build & Test Commands (quick reference) + the UI e2e gate rules |
| `.claude/agents/tester.md` | Full stage-by-stage test procedure, including the e2e dispatch/watch/read commands |
| `.github/KNOWLEDGE-BASE.md` | "UI e2e tests (Appium)" + "Emulator Test Patterns" sections — env vars, TestIds convention, CI job graph |
| `.claude/skills/android-ui-tests/SKILL.md` | Step-by-step recipe: local run, CI dispatch, result reading, healing |
| `.claude/skills/android-ci-failure-patterns/SKILL.md` | Diagnosis reference for Android/Appium/emulator failure signatures |
| `.github/workflows/ndi-for-android-cicd.yml`, `emulator-tests.yml` | The actual gate + manual-dispatch workflows |

Do **not** duplicate those here — update them instead. This file only records the stage map, the
gate rule as a one-line reference, and lessons that do not yet belong in `docs/`.

## Test stages (as implemented)

| Stage | Command | Notes |
|---|---|---|
| 1 — Build | `dotnet build NdiForAndroid.sln` | Repo-root solution; run after every task. |
| 2 — Unit | `dotnet test tests/MauiApp.Tests` | References only `src/Core`, plain `net10.0` — no MAUI workload/Android SDK needed. Must pass before any PR merge. |
| 3 — Integration | `dotnet test --filter "Category=Integration"` | No dedicated integration test project exists yet; this filter runs whatever unit-test-project cases carry that trait. |
| 4 — UI e2e (Appium) | See `.claude/skills/android-ui-tests/SKILL.md` | `tests/MauiApp.UITests`. **Mandatory gate for any PR whose base is `main`** — see below. |
| 5 — NDI e2e (dual-emulator harness) | `tester.md` references `testing/e2e/scripts/run-dual-emulator-e2e.ps1` | **Not implemented** — no such script exists in `testing/e2e/` today (only `run-emulator-tests.sh`, which drives Stage 4). Treat `tester.md`'s Stage 5 as aspirational until this harness is built. |
| 6 — Release gate | `dotnet publish -c Release -f net10.0-android` | IL Linker trimming must succeed. |

## The e2e gate rule (one line)

Before opening or merging a PR whose base is `main`: `tests/MauiApp.UITests` must be green, either
via a dispatched `emulator-tests.yml` run (link recorded in the PR) or the PR's own `e2e-tests`
job. Never merge on a pending/failing check. A moved/renamed control's `TestIds` AutomationId
moves with it in the same PR, and the affected page object is re-run before merge.

## Lessons

### 2026-09-05 — the gate existed but nothing forced anyone to run it (issue #362)

The Appium suite was adapted three times across recent feature work (viewer restructuring, nav
changes, theming) without being executed once against those changes. Two structural reasons:

1. **Feature PRs targeted a non-`main` base** (e.g. an integration branch), and `e2e-tests` in
   `ndi-for-android-cicd.yml` only runs `if: github.ref == 'refs/heads/main' || github.base_ref ==
   'main'` — so the job silently never ran for that work, and nothing was red because nothing ran.
2. **PR #299 merged while its own "Run Emulator UI Tests" run was still pending**, treating an
   in-progress check as equivalent to a passing one. The run then failed after the merge, blocking
   the next release (#361). "Wait for every check" is now an explicit rule, not an assumption.

Separately, #342 moved the viewer's controls into new `ContentView`s during a restructure without
carrying their `TestIds` AutomationIds along; nothing caught it until #360, because the e2e suite
that would have caught it at compile-or-run time was in the state described above. The fix is
procedural, not code: `TestIds` moves with the control, in the same PR, every time.

None of this was a defect in the suite itself (`AppiumDriverFixture`'s `E2E_REQUIRE_DEVICE` /
vacuous-green protections, `A11Y_MAX_VIOLATIONS` ratchet, and `FailureEvidence` capture all
already existed and worked correctly once a run actually happened) — the gap was entirely in when
and whether a run happened at all. That is what CLAUDE.md's Workflow Reliability Rules, this
knowledge file, and `android-ui-tests`/`tester.md` now close.
