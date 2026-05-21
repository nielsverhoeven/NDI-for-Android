---
name: github-issue-enrichment
description: >
  Fetch open GitHub issues for the current repository, analyze one or more issues
  against the actual codebase, and rewrite the issue body with structured technical
  detail so it can serve as direct planning input for a future feature or migration.
  Use when asked to "enrich", "detail", "analyse" or "update" a GitHub issue,
  or when asked to "fetch open issues" and then improve their content.
allowed-tools: shell
---

## Purpose

Transform a minimal or vague GitHub issue into a fully-specified technical brief
by combining the issue's intent with a codebase analysis. The enriched issue
should be ready to feed directly into `speckit.specify`, `speckit.plan`, or any
planning/implementation workflow.

---

## Step 1 — Locate the GitHub CLI

The `gh` binary may not be on `PATH`. Check in order:

```
gh
"C:\Program Files\GitHub CLI\gh.exe"   # Windows default install
/usr/local/bin/gh                       # macOS/Linux Homebrew
```

Use whichever resolves. If none is found, tell the user to install and
authenticate GitHub CLI (`gh auth login`) before continuing.

Confirm authentication with:
```
gh auth status
```

---

## Step 2 — Identify the repository

Determine the `owner/repo` slug. Prefer, in order:
1. The current git remote: `git remote get-url origin`
2. The `GH_REPO` environment variable
3. Explicit user input

---

## Step 3 — List open issues

```
gh issue list --repo <owner/repo> --state open --limit 100
```

Present the results as a compact table (number, title, labels, last updated).
If the user has already identified a specific issue number, skip straight to Step 4.

> **Already-enriched filter:** Before processing any issue, fetch its body and
> check whether it contains the marker `<!-- enriched-by-copilot -->`.
> If the marker is present, skip that issue and inform the user it has already
> been enriched. Never re-enrich an issue that carries this marker unless the
> user explicitly instructs you to with `--force` or equivalent wording.

---

## Step 4 — Fetch the full issue

```
gh issue view <number> --repo <owner/repo>
```

Extract:
- Title
- Current body / description
- Labels and assignees
- Linked PRs or references

---

## Step 5 — Analyse the codebase

Read the repository structure and relevant source files to ground the enriched
issue in reality. Always perform at minimum:

1. **Module graph** — read `settings.gradle.kts`, `package.json`, `*.sln`, or
   whichever build manifest reveals the module/project structure.
2. **Tech stack** — identify languages, frameworks, persistence layer, build
   toolchain, and CI configuration (`.github/workflows/`).
3. **Feature surface** — read the feature directories / key entry-point files
   that are most relevant to the issue's subject area.
4. **Native / interop layer** — if the issue touches FFI, JNI, P/Invoke, or
   native SDKs, read those bridge files too.
5. **Existing specs** — check for a `specs/` directory and read any contracts
   or task files related to the issue topic.

Aim to understand: what exists today, how it is structured, and what would need
to change to fulfil the issue's intent.

---

## Step 6 — Write the enriched issue body

Compose a new issue body using the following sections (omit any that are
genuinely not applicable):

```markdown
## Overview
One-paragraph plain-English summary of what this issue requests and why.

## Current Architecture
Table or bullet list: layer → technology, covering the parts of the codebase
that are relevant to this issue. Include module names, key files, and the
existing feature inventory that must be preserved or replaced.

## Target / Desired State
Describe the end state in concrete terms: what technologies, patterns, or
structures should exist after the issue is resolved.

## Key Challenges & Decisions Required
Numbered list of the hardest technical problems, unknowns, or explicit
decisions that must be made before or during implementation. Flag the
highest-risk items clearly.

## Proposed Approach / Phases
Ordered phases or steps, each with a one-line goal and the main tasks within it.

## Out of Scope
Anything explicitly excluded from this issue.

## Acceptance Criteria
Checkbox list of verifiable, testable conditions that define "done".

## References
Links to relevant documentation, SDKs, prior specs, or related issues/PRs.
```

Keep the tone factual and precise. Avoid vague language ("improve", "better").
Every claim should trace back to something observed in the codebase or the
original issue description.

---

## Step 7 — Update the issue

Append the following HTML comment **as the very last line** of the enriched body.
It is invisible in GitHub's rendered Markdown but detectable by future scans:

```
<!-- enriched-by-copilot -->
```

Then update the issue:

```
gh issue edit <number> --repo <owner/repo> \
  --title "<improved title if needed>" \
  --body "<enriched body ending with the marker above>"
```

Use a here-doc or temp file for long bodies to avoid shell escaping problems:

```powershell
# PowerShell example
$body = @'
... enriched content ...

<!-- enriched-by-copilot -->
'@
gh issue edit <number> --repo <owner/repo> --body $body
```

After updating, confirm with:
```
gh issue view <number> --repo <owner/repo>
```
and report the issue URL to the user.

---

## Quality rules

- Never invent technical details that are not observable in the codebase or
  the original issue text.
- If the NDI SDK, a proprietary library, or a missing dependency makes a claim
  unverifiable, flag it explicitly in the "Key Challenges" section.
- Acceptance criteria must be independently verifiable (a reviewer can tick
  them without knowing the implementation).
- The enriched issue must be complete enough that a separate agent — given only
  the issue body and the repository — can begin planning without further
  clarification.
