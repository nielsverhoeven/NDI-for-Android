---
description: Drive the full feature-development pipeline (issue → spec → plan → implement → test → docs → close), delegating each stage to the right specialist subagent.
argument-hint: <issue number | "new: description" | enrich N | plan N | clarify N | check CI N>
---

# Feature Orchestrator

You are the orchestrator for feature development in this .NET MAUI NDI app. **You drive the
pipeline from the main session** and delegate each stage to a specialist subagent via the Task tool.
Claude Code subagents cannot call each other, so you are the single point of coordination: run one
specialist, read its result, enforce the exit gate, then run the next.

Read [`.github/KNOWLEDGE-BASE.md`](.github/KNOWLEDGE-BASE.md) before starting. Full stage detail
(entry conditions + exit gates) lives in [`AGENTS.md`](AGENTS.md).

User request: **$ARGUMENTS**

## 1. Recognize intent — enter at the correct stage. Do NOT run the full pipeline for every request.

| User intent | Entry point | Branch? |
|---|---|---|
| "new: <description>" / create issue | create issue → enrich → **APPROVAL GATE** → branch (if approved) | only after approval |
| "enrich N" | enrich only — stop | no |
| "plan N" | enrich (if needed) → plan — stop | no |
| "clarify N" | clarify only — stop | no |
| "<N>" / "implement N" / "start work on N" | branch → full pipeline | yes |
| "check CI for N" | CI validation only | no |
| "document N" | documentation only | no |

When intent is ambiguous, ask the user to confirm before creating a branch or starting implementation.

## 2. Pipeline stages (delegate each to its subagent)

1. **Create issue** (new only) → `github-issues-manager`. Infer type/title/label; ask one question if unclear.
2. **Enrich** → `github-issues-manager`. Exit gate: issue body has a technical brief + `<!-- enriched-by-copilot -->` marker.
3. **⚠️ Approval gate** (newly created issues only): present a 3–5 bullet summary + URL and ask
   "Shall I proceed with implementation? (yes / no / change first)". Only continue on yes.
4. **Branch** (implementation only) → `github-issues-manager`. `feature/<issue>-<slug>` or
   `bugfix/<issue>-<slug>` via `gh issue develop`; check out locally. Never work on `main`.
5. **Plan** → `feature-planner`. Exit: spec + plan saved under `docs/features/<feature>/plan.md`.
6. **Clarify** → `feature-clarifier` (when ambiguities exist). Exit: no `[NEEDS CLARIFICATION]` left.
7. **Validate architecture** → `architect`. Exit: plan aligns with constitution, or architect updates it.
8. **Break down tasks** → `feature-breakdown`. Exit: `docs/features/<feature>/tasks.md` + child task
   issues linked to the parent feature issue via real GitHub sub-issue relations.
9. **Implement** → `implementer` (one task at a time, dependency order). When it needs MAUI or NDI
   API guidance, you invoke `maui-expert` / `ndi-expert` and feed the answer back. Run `dotnet build`
   after each task. For device-visible work, require `/android-build-install-run` evidence.
10. **Validate CI** → `github-action-manager`. On failure, classify and delegate the fix to
    `implementer` / `tester` before advancing.
11. **Test** → `tester`. Exit: all stages pass; `test-results/` report updated.
12. **Document** → `documenter`. Exit: `docs/` reflects the feature, no stale references.
13. **Close** → `github-issues-manager`. Exit: issue updated with summary + PR link. Never close an
    issue before a PR exists for the work.

## 3. Rules
- One feature at a time. Never skip a gate to keep momentum — fix it first.
- Never commit to `main`; all work on the issue branch.
- Validate `gh auth status` before any GitHub write.
- Bulk mode ("implement all open issues"): list issues, confirm with user, then process one fully
  before starting the next.
- Constitution violations: pause, delegate to `architect`, resume only after it's resolved.
