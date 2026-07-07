---
name: github-issue-subtasks-standardizer
description: Standardize issue subtask creation for this repository. Creates task issues, enforces parent-child relationships using GitHub sub-issues, and writes traceable links/checklists back to the parent issue before implementation starts.
allowed-tools: Bash
---

## Purpose

Create implementation-ready task breakdowns for a parent issue with explicit
GitHub parent-child links, so progress can be tracked reliably and consistently.

## Inputs

- Parent issue number (required)
- Owner/repo (infer from git remote when possible)
- Optional: requested number of subtasks or suggested task titles

## Step 1 - Validate prerequisites

1. Confirm GitHub auth:
```
gh auth status
```
2. Resolve repository slug from:
- `git remote get-url origin` (preferred)
- explicit input

## Step 2 - Read parent issue context

Fetch parent issue details:
```
gh issue view <parent> --repo <owner/repo> --json number,id,title,body,url,labels
```

Extract the scope and acceptance criteria to derive subtasks.

## Step 3 - Create standardized subtasks

Before creating any task, enforce idempotency:

1. Query existing open `task` issues for the same parent and T-ID/title pattern.
2. Reuse matching issues instead of creating duplicates.
3. Only create missing tasks.

Create 3-7 task issues with labels:
- `task`
- same type label as parent when useful (e.g., `bug`)

Each subtask body must include:
- `Parent: #<parent>`
- Goal
- Done When checklist

Example:
```
gh issue create --repo <owner/repo> \
  --title "[Task][#<parent>] <short task title>" \
  --body "Parent: #<parent>\n\n## Goal\n...\n\n## Done When\n- ..." \
  --label task
```

## Step 4 - Set true parent-child links (mandatory)

After creating task issues, link each child to the parent with GraphQL:

1. Get node IDs:
```
gh issue view <parent> --repo <owner/repo> --json id
gh issue view <child> --repo <owner/repo> --json id
```

2. Add sub-issue relation:
```
gh api graphql \
  -f query='mutation($parent:ID!,$childUrl:String!){addSubIssue(input:{issueId:$parent,subIssueUrl:$childUrl,replaceParent:true}){issue{id} subIssue{id}}}' \
  -f parent=<PARENT_NODE_ID> \
  -f childUrl=<CHILD_ISSUE_URL>
```

Repeat for every child.

## Step 5 - Verify hierarchy

Verify parent contains expected sub-issues:
```
gh api graphql \
  -f query='query($owner:String!,$repo:String!,$number:Int!){repository(owner:$owner,name:$repo){issue(number:$number){number subIssues(first:50){nodes{number title state url}}}}}' \
  -f owner=<owner> -f repo=<repo> -F number=<parent>
```

If any child is missing, re-run Step 4.

If duplicate children exist for the same T-ID, close duplicates and keep one canonical child issue before finishing.

## Step 6 - Update parent issue body

Ensure parent issue has a `## Task Breakdown` section listing child issues:
- [ ] #<child1> <title>
- [ ] #<child2> <title>
...

Keep `<!-- enriched-by-copilot -->` as the final line when present.

## Step 7 - Progress rules

- Do not start code implementation until Step 5 passes.
- Close child tasks as completed when done and leave completion comments with evidence.
- Keep the parent issue open until implementation PR exists.
- Never report "hierarchy synced" unless the parent `subIssues` query shows the exact expected child set.

## Quality rules

- Parent-child relation must be a real GitHub sub-issue link, not only plain text references.
- Child issue titles must be action-oriented and scoped to one deliverable.
- Every child must have verifiable `Done When` criteria.
- Parent issue must remain the single source of progress truth.
