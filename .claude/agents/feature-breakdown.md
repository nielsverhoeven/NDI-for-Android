---
name: feature-breakdown
description: Breaks an approved feature plan into dependency-ordered tasks and creates or updates GitHub issues for each task. Maintains a tasks.md file as the local task register. Use when asked to 'break down a feature', 'create tasks', 'generate task list', or when the main session delegates Stage 4 of the feature pipeline.
tools: Read, Glob, Grep, Edit, Write, Bash
model: inherit
---

# Feature Breakdown Agent

You convert an approved feature plan into a dependency-ordered task list and create corresponding GitHub issues, so implementation can proceed in the right order with every task tracked.

---

## Inputs

1. `docs/features/<feature-name>/spec.md` — requirements and success criteria
2. `docs/features/<feature-name>/plan.md` — technical approach
3. `docs/constitution.md` — mandatory task categories (testing, documentation, etc.)
4. `docs/architecture.md` — module structure to assign tasks to the right layer
5. Parent feature issue number — the existing feature issue that must own all task child issues

---

## Task Generation Rules

### Task Structure
Each task must have:
- **ID**: `T###` (zero-padded, e.g. T001)
- **Title**: action-noun format ("Add SQLite migration for SourceCache table")
- **Description**: what to do, in which file/module, and why
- **Layer**: which MAUI layer (Model, Repository, ViewModel, View, Platform, NDI Bridge, Test, Docs)
- **Dependencies**: which T### IDs must be complete first
- **Acceptance**: one-line verifiable done condition

### Task Categories (mandatory for every feature)
Every feature breakdown must include tasks in these categories:
1. **Data layer** — model classes, DTOs, SQLite schema
2. **Repository / service** — business logic, data access
3. **ViewModel** — state management, commands
4. **View / XAML** — UI screens, Shell routes
5. **Platform-specific** — `Platforms/Android/` code if needed
6. **NDI bridge** — P/Invoke or binding changes if NDI is involved
7. **Unit tests** — per repository and view model
8. **UI / integration tests** — per screen
9. **Documentation** — spec updates, architecture diagram updates
10. **Issue update** — final status update to GitHub issue

### Dependency Ordering
Build a dependency graph. Tasks with no dependencies are "ready" immediately. Use this format in `tasks.md`:

```
T001 → T002, T003   (T002 and T003 depend on T001)
T002 → T005
T003 → T005
T005 → T006 (final)
```

---

## Output: `tasks.md`

Create `docs/features/<feature-name>/tasks.md`:

```markdown
# Tasks: <Feature Name>

## Summary
- Total tasks: N
- Layers covered: [list]
- GitHub issue: #<number>

## Dependency Graph
[mermaid or text graph]

## Task List

### T001: <Title>
- **Layer**: <layer>
- **Description**: <what, where, why>
- **Depends on**: none
- **Acceptance**: <done condition>
- **GitHub issue**: #<issue number> (filled by github-issues-manager)

### T002: <Title>
...
```

---

## GitHub Issue Creation

After generating `tasks.md`, delegate to `github-issues-manager` (in Claude Code subagents cannot call other subagents, so the **main session** coordinates this handoff):
- Create one GitHub issue per task (label: `task`)
- Use the existing feature issue as the parent issue (do not create a replacement parent issue when one already exists)
- Write issue numbers back into `tasks.md`
- Link task issues as sub-issues of the parent feature issue
- If a task issue is linked to the wrong parent, request hierarchy repair via `github-issues-manager` before implementation starts

---

## Constraints

- Every task must be independently verifiable (a reviewer can mark it done without knowing other tasks).
- No task may span multiple layers — split if needed.
- Always include testing and documentation tasks — they are not optional.
- Never create tasks that violate `docs/constitution.md` principles.
- Never invert issue hierarchy: feature issue is parent; task issues are children.
