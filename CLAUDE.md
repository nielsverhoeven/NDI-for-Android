# CLAUDE.md

Guidance for Claude Code when working in this repository.

> **Start here**: Read [`.github/KNOWLEDGE-BASE.md`](.github/KNOWLEDGE-BASE.md) for the full
> authoritative reference (tech stack, key file paths, code patterns, agent efficiency rules).
> Read it *instead of* re-reading `docs/constitution.md` + `docs/architecture.md` for routine
> implementation tasks. This file is the quick-start summary + how the agent system maps to Claude Code.

This app is a **.NET MAUI (C#) application** integrating the **NDI SDK** for video source
discovery, streaming, and output on Android. It is **NOT** a Kotlin / Android Studio project.

## Tech Stack
- **.NET MAUI** targeting `net10.0-android` — C# 12, nullable enabled
- **MVVM**: CommunityToolkit.Mvvm (`ObservableObject`, `[RelayCommand]`, `[ObservableProperty]`)
- **DI**: `Microsoft.Extensions.DependencyInjection` via `src/MauiApp/MauiProgram.cs` — no service locator
- **Persistence**: SQLite-net-pcl (async API only)
- **NDI**: P/Invoke against `libndi.so` (arm64-v8a + armeabi-v7a)
- **Tests**: xUnit 2.x + Moq — `dotnet test tests/MauiApp.Tests`
- **CI**: GitHub Actions — see `.github/workflows/`

## Build & Test Commands
```powershell
dotnet build src/NdiForAndroid.sln    # verify after every task — do not accumulate build failures
dotnet test tests/MauiApp.Tests       # must pass before PR merge
```

## Project Structure
```
src/
  Core/        <- Domain contracts: interfaces, models, ViewModels
  MauiApp/     <- MAUI app: views, implementations, DI, Android platform
tests/
  MauiApp.Tests/     <- xUnit unit tests (no native NDI)
  MauiApp.UITests/   <- Appium UI smoke tests
docs/
  constitution.md    <- authoritative tech/architecture decisions
  architecture.md    <- module map and dependency rules
.github/
  KNOWLEDGE-BASE.md  <- consolidated agent reference (read this first!)
```

## Code Style
- C# 12, nullable reference types enabled — no `!` suppressions without a comment
- XAML for all UI — no code-behind logic
- All NDI native calls isolated to the NDI bridge layer; no NDI SDK types leak into ViewModels or Views
- No direct database access from ViewModels — go through repositories
- Conventional commits: `feat(<layer>): description` with `Task: T###` and `Issue: #N` trailers.
  Use `Closes #N` to auto-link/close the issue (`Refs #N` for partial work).

---

## Agent System (how Copilot's agents map to Claude Code)

This repo was originally configured for GitHub Copilot's custom-agent system. It has been
translated to Claude Code:

- **Specialist agents** live in [`.claude/agents/`](.claude/agents/) as **subagents**. Invoke one
  with the `Task`/Agent tool (e.g. ask the `maui-expert` for an API answer, the `implementer` to
  write code, the `tester` to run test stages).
- **Skills** live in [`.claude/skills/`](.claude/skills/). Invoke with the Skill tool / `/skill-name`.
- The **orchestrator** is not a subagent — **you (the main session) play the orchestrator.** Drive
  the feature pipeline yourself and delegate each stage to the right specialist subagent. The
  `/feature` slash command encodes the full pipeline.

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
- Run `dotnet build` after every implementation task.
- Use the `android-build-install-run` skill whenever acceptance depends on observable device
  behavior, before declaring Android UI work verified.
- Never skip a test stage — fix or explicitly document blockers.
- Open a PR for completed work before closing the issue; include the PR link in the closure note.
- Parent/child issue hierarchy is mandatory (feature = parent, tasks = children) via real GitHub
  sub-issue relations (`addSubIssue`), not checklists alone.

## Note on local-AI / Ollama steps
The original `implementer` agent referenced `.github/scripts/ollama-task.ps1` to offload boilerplate
to a local model and reduce Copilot credits. That credit-reduction concern does not apply to Claude
Code — generate code directly. The script is still present if you want to use it, but it is optional.
