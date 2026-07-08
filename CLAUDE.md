# CLAUDE.md

Guidance for Claude Code when working in this repository.

> **Start here**: Read [`.github/KNOWLEDGE-BASE.md`](.github/KNOWLEDGE-BASE.md) for the full
> authoritative reference (tech stack, key file paths, code patterns, feature history, agent
> efficiency rules). Read it *instead of* re-reading `docs/constitution.md` + `docs/architecture.md`
> for routine implementation tasks. This file is the quick-start summary + how the agent system
> maps to Claude Code.

This app is a **.NET MAUI (C#) application** integrating the **NDI SDK** for video source
discovery, receiving, sending, and output on Android. It is **NOT** a Kotlin / Android Studio
project. The NDI bridge is a **real P/Invoke integration** against the bundled NDI SDK 6.3.1 —
not a stub.

## Tech Stack
- **.NET MAUI** targeting `net10.0-android` — C# 12, nullable enabled, implicit usings.
  Android `minSdk 26`, `targetSdk 35`. `Core` project is plain `net10.0` (MAUI-free, unit-testable).
- **UI**: MAUI Shell + XAML; **SkiaSharp** (`SkiaSharp.Views.Maui.Controls`) for the NDI video surface
- **MVVM**: CommunityToolkit.Mvvm (`ObservableObject`, `[RelayCommand]`, `[ObservableProperty]`)
- **DI**: `Microsoft.Extensions.DependencyInjection` via `src/MauiApp/MauiProgram.cs` — no service locator
- **Persistence**: SQLite-net-pcl (async API only) via `src/MauiApp/Data/NdiDatabase.cs`
- **NDI**: real P/Invoke against bundled `libndi.so` (NDI SDK 6.3.1, `arm64-v8a` + `armeabi-v7a`
  only). Soft-disabled on x86/x86_64 — no native lib there, so `NdiRuntime.EnsureInitialized()`
  returns `false` and NDI features disable without crashing (important for emulators).
- **Tests**: xUnit 2.x + Moq (+ `Microsoft.Extensions.TimeProvider.Testing`) — `tests/MauiApp.Tests`;
  Appium UI smoke tests — `tests/MauiApp.UITests`
- **CI**: GitHub Actions — see `.github/workflows/` (`ndi-for-android-cicd.yml`,
  `emulator-tests.yml`, `codeql.yml`, `copilot-setup-steps.yml`)

## Build & Test Commands
```powershell
dotnet build NdiForAndroid.sln        # solution is at the REPO ROOT (not under src/); run after every task
dotnet test tests/MauiApp.Tests       # non-NDI unit tests — must pass before PR merge
```

## Project Structure (feature-based / vertical slices)
```
NdiForAndroid.sln        <- solution at repo root (Core + MauiApp + Tests + UITests)
src/
  Core/                  <- Domain contracts: interfaces, models, ViewModels (net10.0, MAUI-free)
    Features/{Feature}/
      Models/            <- records/DTOs
      Repositories/      <- interfaces only
      Services/          <- interfaces (+ platform-agnostic service impls)
      ViewModels/        <- CommunityToolkit MVVM view models
    NdiBridge/           <- bridge interfaces + plain-C# models (INdiBridges.cs, NdiBridgeModels.cs, QualityProfile.cs)
    Services/            <- cross-cutting contracts (IMainThreadDispatcher, ICaptureSources, lifecycle, etc.)
  MauiApp/               <- MAUI app: composition + concrete implementations
    Features/{Feature}/
      Views/             <- XAML + minimal code-behind
      Repositories/      <- concrete SQLite implementations
      Services/          <- concrete service implementations
    NdiBridge/           <- real P/Invoke bridge (the ONLY layer doing native NDI interop)
      Interop/           <- NdiNativeMethods.cs + NdiNativeStructs.cs ([DllImport("ndi")])
    Data/                <- SQLite context (NdiDatabase.cs)
    Platforms/Android/   <- Android-only lifecycle, permissions, capture, MediaProjection, NSD
      libs/<abi>/        <- bundled libndi.so (arm64-v8a, armeabi-v7a)
tests/
  MauiApp.Tests/         <- xUnit unit tests (no native NDI — mock the bridge)
  MauiApp.UITests/       <- Appium UI smoke tests
docs/
  constitution.md        <- authoritative tech/architecture decisions
  architecture.md        <- module map, dependency + threading rules (canonical diagrams)
  ndi-sdk-coverage.md    <- per-capability NDI SDK coverage matrix
  features/{feature}/    <- per-feature spec/plan/tasks/release-notes
.github/
  KNOWLEDGE-BASE.md      <- consolidated agent reference (read this first!)
```

Features currently in the app: **Home**, **Sources** (View tab — discovery + tap-to-view),
**Output** (Stream tab — outgoing NDI send), **Viewer** (receive + render), **Settings**,
**DiagOverlay** (diagnostic log), **ConnectionHistory**, **DeepLinking**, **Navigation**
(responsive size-class-aware shell).

## Architecture Rules (must not violate)
1. **No direct DB access from ViewModels** — always go through a repository interface.
2. **No NDI SDK types cross the bridge boundary** — the bridge returns plain C# records/enums only.
   All `[DllImport("ndi")]` lives in `src/MauiApp/NdiBridge/Interop/`.
3. **No business logic in Views** — Views are XAML + bindings; keep code-behind minimal.
4. **NDI threading**: bridge events (`ConnectionStateChanged`, `TallyEchoChanged`,
   `OutputStatusChanged`) are raised on pump/background threads. Subscribers marshal to the UI
   thread via `IMainThreadDispatcher` (Core) / `MainThread.BeginInvokeOnMainThread` (MauiApp).
   ViewModels stay in Core (MAUI-free) and unit-testable — timing uses injected `TimeProvider`.
5. **Android APIs** are isolated in `Platforms/Android/` behind Core interfaces; non-Android
   targets get `Noop*` implementations so the Core builds/tests without a device.
6. **Every captured NDI frame MUST be freed** (`recv_free_*`) — a leak is native OOM in seconds.
   Pump threads swallow exceptions (an uncaught background-thread crash kills the process).

## Code Style
- C# 12, nullable reference types enabled — no `!` suppressions without a comment
- XAML for all UI — no code-behind logic
- All NDI native calls isolated to the NDI bridge layer; no NDI SDK types leak into ViewModels or Views
- No direct database access from ViewModels — go through repositories
- Conventional commits: `feat(<layer>): description` with `Task: T###` and `Issue: #N` trailers.
  Use `Closes #N` to auto-link/close the issue (`Refs #N` for partial work).

## Android Gotchas (do not regress)
- **Manifest**: this project does **not** use MAUI SingleProject, so
  `src/MauiApp/NdiForAndroid.csproj` **must** keep
  `<AndroidManifest>Platforms\Android\AndroidManifest.xml</AndroidManifest>` — without it the
  APK silently ships with only `INTERNET` and multicast discovery/capture break.
- **Native libs**: `libndi.so` is packaged by the default `AndroidNativeLibrary` globs. Do **not**
  add an explicit `<AndroidNativeLibrary Include>` (double-add → XA4301).
- **Theming**: always use `DynamicResource` (never `StaticResource`/hardcoded hex) so runtime
  theme changes apply. See the MAUI Theming Rules section in `KNOWLEDGE-BASE.md`.
- **CI/emulator install**: always install `com.ndi.android-Signed.apk` (the unsigned variant fails
  with `INSTALL_PARSE_FAILED_NO_CERTIFICATES`).

---

## Agent System (how Copilot's agents map to Claude Code)

This repo was originally configured for GitHub Copilot's custom-agent system (see `AGENTS.md` and
`.github/agents/`). It has been translated to Claude Code:

- **Specialist agents** live in [`.claude/agents/`](.claude/agents/) as **subagents**. Invoke one
  with the `Task`/Agent tool (e.g. ask the `maui-expert` for an API answer, the `implementer` to
  write code, the `tester` to run test stages).
- **Skills** live in [`.claude/skills/`](.claude/skills/). Invoke with the Skill tool / `/skill-name`.
- **Slash commands** live in [`.claude/commands/`](.claude/commands/) — currently `/feature`, which
  encodes the full feature pipeline.
- The **orchestrator** is not a subagent — **you (the main session) play the orchestrator.** Drive
  the feature pipeline yourself and delegate each stage to the right specialist subagent.

> **Key difference from Copilot**: Claude Code subagents are stateless and **cannot call other
> subagents or hand off to each other**. The old `handoffs:` graph is replaced by *you* coordinating
> from the main loop — run one specialist, read its result, then run the next.

### Specialist subagents
| Subagent | Role |
|---|---|
| `architect` | Owns `docs/architecture.md` + `docs/constitution.md`; validates plans |
| `maui-expert` | Authoritative .NET MAUI guidance from official docs (read/web only) |
| `ndi-expert` | Authoritative NDI SDK guidance from https://docs.ndi.video/ (read/web only) |
| `implementer` | Writes the .NET MAUI code for each task |
| `tester` | Runs all test stages, generates/heals tests, reports results |
| `documenter` | Updates project documentation to match implemented features |
| `feature-clarifier` | Resolves spec ambiguities via targeted questions |
| `feature-planner` | Turns an enriched issue into a spec + technical plan |
| `feature-breakdown` | Breaks the plan into dependency-ordered task issues |
| `github-issues-manager` | Full GitHub issue lifecycle (create/enrich/link/branch/close) |
| `github-action-manager` | Inspects CI runs, classifies failures, recommends fixes |

### Skills (`.claude/skills/`)
| Skill | Purpose |
|---|---|
| `android-build-install-run` | Build the APK, install on device/emulator, launch, capture startup evidence |
| `android-ci-failure-patterns` | Diagnose/fix Android emulator CI failures |
| `github-actions-manager` | List workflows, inspect YAML health |
| `github-action-runs-manager` | Fetch run status, retrieve failure logs, classify causes |
| `github-issue-enrichment` | Enrich a GitHub issue with a full technical brief |
| `github-issue-subtasks-standardizer` | Standardize + link parent/child sub-issues |

---

## Feature Development Pipeline (you drive this — see `/feature`)

GitHub is the single source of truth. All feature work begins from a GitHub issue.
Full stage-by-stage detail (entry conditions, exit gates) lives in [`AGENTS.md`](AGENTS.md).

```
[new issue]      → github-issues-manager (create) → github-issues-manager (enrich) → APPROVAL GATE
[implement N]    → github-issues-manager (branch) → feature-planner → architect (validate)
                 → feature-breakdown (tasks) → implementer  (↕ maui-expert / ndi-expert)
                 → github-action-manager (CI) → tester → documenter → github-issues-manager (close)
[enrich/plan/clarify only] → run that single stage; never create a branch
```

## Workflow Reliability Rules (non-negotiable)
- Read [`.github/KNOWLEDGE-BASE.md`](.github/KNOWLEDGE-BASE.md) before starting feature work.
- Every issue must have a **linked branch before implementation starts** — never commit to `main`.
- Branch naming: feature → `feature/<issue>-<slug>`, bug → `bugfix/<issue>-<slug>`
  (slug = lowercase, hyphens, max 5 words from the title, articles stripped). Create via
  `gh issue develop` so it links in the issue's Development sidebar.
- Validate `gh auth status` before any GitHub write operation.
- Run `dotnet build NdiForAndroid.sln` after every implementation task.
- Use the `android-build-install-run` skill whenever acceptance depends on observable device
  behavior, before declaring Android UI work verified.
- Never skip a test stage — fix or explicitly document blockers.
- Open a PR for completed work before closing the issue; include the PR link in the closure note.
- Parent/child issue hierarchy is mandatory (feature = parent, tasks = children) via real GitHub
  sub-issue relations, not checklists alone.

## Note on local-AI / Ollama steps
The original `implementer` agent referenced `.github/scripts/ollama-task.ps1` to offload boilerplate
to a local model and reduce Copilot credits. That credit-reduction concern does not apply to Claude
Code — generate code directly. The script is still present if you want to use it, but it is optional.
