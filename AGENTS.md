# AGENTS.md

## Start Here
- Constitution lives at `docs/constitution.md` — read it before any feature work. If it does not exist, invoke the `architect` agent to create it.
- Architecture lives at `docs/architecture.md` — maintained by `architect`.
- GitHub is the single source of truth for features and tasks. All feature work begins from a GitHub issue.
- This app is a **.NET MAUI (C#) application** integrating the **NDI SDK** for video source discovery, streaming, and output on Android.

## Agent Network

The agent network is organized into three rings:

### Orchestration
- **`orchestrator`** — main entry point for all feature development. Drives the full pipeline from GitHub issue to shipped, tested, documented code. Co-owns `docs/constitution.md` with `architect`.

### Domain Experts (consulted, never primary entry points)
- **`architect`** — owns the constitution and architecture documentation. Validates all feature plans. Consults `maui.expert` and `ndi.expert` for technology decisions.
- **`maui.expert`** — authoritative .NET MAUI knowledge. Answers implementation questions from official docs (https://learn.microsoft.com/en-us/dotnet/maui/, https://github.com/dotnet/maui).
- **`ndi.expert`** — authoritative NDI SDK knowledge. Answers integration questions from official docs (https://docs.ndi.video/).

### Feature Pipeline (invoked by orchestrator in order)
1. **`github.issues-manager`** — enriches and updates GitHub issues. Single agent authorized to write issue content.
2. **`feature.clarifier`** — resolves spec ambiguities through targeted questions. Writes answers back to the GitHub issue.
3. **`feature.planner`** — translates an enriched issue into a feature spec and technical plan.
4. **`feature.breakdown`** — breaks the plan into dependency-ordered tasks and creates GitHub issues for each.
5. **`implementer`** — writes the .NET MAUI code for each task. Consults `maui.expert` and `ndi.expert`.
6. **`github.action-manager`** — validates CI after implementation. Classifies failures and delegates fixes to `implementer`, `tester`, or `orchestrator`.
7. **`tester`** — runs all test stages, plans and generates new tests, heals broken tests.
8. **`documenter`** — updates project documentation to reflect implemented features.

## Feature Development Flow

```
User → orchestrator
  [if new issue] → github.issues-manager (Stage -2: create issue with title + label)
                 → github.issues-manager (Stage 0: enrich new issue)
                 → [APPROVAL GATE] user confirms before implementation starts
  [if existing issue + implement] → github.issues-manager (Stage -1: create branch)
  [if enrich/plan/clarify only]  → github.issues-manager / feature.clarifier / feature.planner
  → feature.planner (create spec + plan)
  → architect (validate plan against constitution)
  → feature.breakdown (tasks → GitHub issues)
  → implementer (write code on issue branch)
    ↕ maui.expert (MAUI API guidance)
    ↕ ndi.expert (NDI SDK guidance)
  → github.action-manager (validate CI on PR/branch)
    ↕ implementer / tester (fix failures if any)
  → tester (run all stages)
  → documenter (update docs)
  → github.issues-manager (close issue with summary)
```

> **Trigger rules**: Pipeline entry depends on user intent. New issue creation always goes through Stage -2 → Stage 0 → Approval Gate before any branch or implementation work begins. Existing issue implementation starts at Stage -1. Enrichment, planning, and clarification are standalone operations that never create branches.

## Reusable Skills

Skills live in `.github/skills/<skill-name>/SKILL.md` and are invoked with `/skill-name` from any agent.

| Skill | Purpose |
|---|---|
| `github-issue-enrichment` | Enrich a GitHub issue with full technical brief; marks issue to prevent re-processing |
| `github-actions-manager` | List workflows, inspect YAML health, check for timeouts and deprecated actions |
| `github-action-runs-manager` | Fetch run status by PR/branch/commit, retrieve failure logs, classify root causes |

## Constitution

- Location: `docs/constitution.md`
- Owned by: `architect` (amendments) + `orchestrator` (enforcement)
- Contains: technology stack, architecture principles, NDI bridge pattern, testing standards, development agreements
- Every agent must read `docs/constitution.md` before starting work on any feature.
- Constitution amendments require `architect` review and version increment.

## Technology Stack (summary — see constitution for details)
- **Platform**: .NET MAUI targeting `net9.0-android`
- **Language**: C#
- **UI**: MAUI Shell + XAML
- **MVVM**: CommunityToolkit.Mvvm
- **DI**: Microsoft.Extensions.DependencyInjection via MauiProgram.cs
- **Persistence**: SQLite-net or EF Core + SQLite
- **NDI bridge**: P/Invoke against `libndi.so` or Android Binding Library (decision in constitution)
- **Build**: dotnet CLI
- **Tests**: xUnit, dotnet test

## GitHub Issue Conventions
- All features start from a GitHub issue enriched by `github.issues-manager`.
- Issues can originate from the user (existing issue number) or be created fresh by `orchestrator` → `github.issues-manager` (Operation 7) from a natural-language description.
- Enriched issues contain the marker `<!-- enriched-by-copilot -->` as their last line.
- Before implementation starts on any issue, break work into explicit task-level child issues (or a clearly enumerated task checklist if child issues are not practical) and link them to the parent issue.
- **Approval is required** before implementation starts on a newly created issue — `orchestrator` presents the enriched issue and waits for explicit user confirmation.
- Task-level GitHub issues are created by `feature.breakdown` with label `task`.
- Feature-level GitHub issues carry label `feature`; bug issues carry label `bug`.
- Parent/child hierarchy is mandatory: the feature issue is the parent, and all task issues are child issues linked to that parent.
- Do not create a duplicate parent issue during breakdown when a feature parent issue already exists.
- Any detected hierarchy mismatch must be repaired by `github.issues-manager` before implementation continues.
- Only `github.issues-manager` writes back to GitHub issues on behalf of other agents.
- Do not close an issue until a pull request exists for the implemented changes and the issue references that PR.

## Branch Naming Convention

Every issue gets a dedicated branch created by `github.issues-manager` (Operation 6) before any work begins.

| Issue type | Branch pattern | Example |
|---|---|---|
| Feature (default) | `feature/<issue>-<slug>` | `feature/115-ndi-source-discovery` |
| Bug (`bug` label) | `bugfix/<issue>-<slug>` | `bugfix/116-crash-on-resume` |

**Slug rules**: lowercase, hyphens, max 5 words from the issue title, articles stripped.

Branches are created via `gh issue develop` so they appear linked in the GitHub issue's "Development" sidebar. All implementation commits go on this branch — never directly on `main`.

## Workflow Reliability Rules
- Always read `docs/constitution.md` before starting any feature work.
- GitHub is the single source of truth — all features must have a GitHub issue.
- Every issue must have a linked branch before implementation starts (Stage -1).
- Always run task breakdown before coding: create child task issues (preferred) or an explicit task checklist in the issue, and track progress against it.
- Validate `gh auth status` before any GitHub write operation.
- Run `dotnet build` after every implementation task — do not accumulate build failures.
- Never skip a test stage — fix or explicitly document blockers.
- Open a PR for completed issue work before closing the issue, and include the PR link in the closure note.
- Constitution violations must be escalated to `orchestrator`; never proceed silently.
