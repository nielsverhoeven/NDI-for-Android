---
name: github-actions-manager
description: >
  Manage GitHub Actions workflows for the current repository: list all workflows,
  inspect their definitions, enable or disable them, and check their overall health.
  Use when asked to 'list workflows', 'inspect a workflow', 'enable/disable a workflow',
  'check workflow health', or 'what actions are configured'.
allowed-tools: shell
---

## Purpose

Provide a complete picture of all GitHub Actions workflows in the repository —
what they do, how they are configured, and whether they are healthy — so other
agents can make informed decisions about CI status and remediation.

---

## Step 1 — Locate the GitHub CLI

Check in order:
```
gh                                        # if on PATH
& "C:\Program Files\GitHub CLI\gh.exe"   # Windows default
/usr/local/bin/gh                         # macOS / Linux
```

Verify authentication:
```
gh auth status
```

Resolve `owner/repo` from `git remote get-url origin`.

---

## Step 2 — List All Workflows

```
gh workflow list --repo <owner/repo> --all
```

Present as a table:

| Name | State | ID | File |
|---|---|---|---|

Mark disabled workflows clearly. Note any workflows that are `active` but have
not had a run in more than 30 days (potentially stale).

---

## Step 3 — Inspect a Workflow Definition

When a specific workflow needs deeper inspection:

1. **Read the workflow YAML** from `.github/workflows/<file>.yml` in the local
   workspace. This is always available and does not require an API call.

2. **Check for common issues** in the YAML:
   - `timeout-minutes` missing on long-running jobs (default is 6 hours — a
     silent hang risk)
   - Actions pinned to a major version tag rather than a SHA (security risk)
   - Deprecated Node.js versions (`node-version: '16'` or `'18'`; Node 20
     deprecated from GitHub runners June 2026)
   - `actions/checkout`, `actions/setup-java`, `actions/setup-node`,
     `actions/upload-artifact` — check if they need upgrading
   - Jobs with no `needs:` dependency that should be gated
   - Missing `if: always()` on artifact upload steps (artifacts lost on failure)
   - Secrets referenced but not documented in the workflow comment
   - Long-running e2e or emulator jobs without an explicit `timeout-minutes`

3. **Report findings** as a checklist:
   ```
   ✅ timeout-minutes set on all jobs
   ⚠️  actions/checkout@v4 uses Node 20 (deprecated June 2026)
   ❌ e2e job has no timeout — will run for up to 6h if hung
   ```

---

## Step 4 — Enable / Disable a Workflow

Enable:
```
gh workflow enable <workflow-id-or-name> --repo <owner/repo>
```

Disable:
```
gh workflow disable <workflow-id-or-name> --repo <owner/repo>
```

Confirm the change:
```
gh workflow list --repo <owner/repo> --all
```

---

## Step 5 — Workflow Health Summary

Produce a health summary table across all workflows:

| Workflow | State | Last Run | Last Status | Avg Duration | Issues Found |
|---|---|---|---|---|---|

For each workflow:
- Fetch the most recent run: `gh run list --workflow <id> --limit 1`
- Flag any workflow whose last run failed, timed out, or is older than 14 days

---

## Quality Rules

- Never modify workflow YAML files directly — report findings and let the
  appropriate agent (or the user) make changes.
- Always check the local `.github/workflows/` directory first for YAML content
  before making API calls.
- When reporting deprecated action versions, always include the specific
  deprecation date and the recommended upgrade path.
- Flag `timeout-minutes` omissions as a **critical** finding — the 6-hour
  default is the most common cause of wasted CI minutes.
