---
description: '>-'
Convert existing tasks into actionable, dependency-ordered GitHub issues for: ''
the feature with proper hierarchy and labeling. Retrieves available labels first,: ''
checks existing issue references in tasks.md, then updates/creates issues with: ''
proper labels and parent-child relationships.: ''
tools: ['github/github-mcp-server/issue_write', 'github/github-mcp-server/sub_issue_write', 'github/github-mcp-server/issue_read', 'open_file', 'list_dir', 'read_file', 'file_search']
handoffs:
  - label: Regenerate Tasks
    agent: speckit.tasks
    prompt: '>-'
    Regenerate tasks.md for the feature — the existing task list may be stale: ''
    or missing entries that need GitHub issues.: ''
    send: false
---
## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Core Principle: Issue Hierarchy

**User Story Issues (parent level)**:
- One issue per user story (US1, US2, US3, etc.)
- Label: `user-story`
- Contains: Multiple task issues as sub-issues
- Title format: `[Story] US#: {description}`

**Task Issues (child level)**:
- One issue per task (T001, T002, etc.)
- Labels: `task` + story identifier (e.g., `us1`, `us2`)
- Parent: Exactly one user story issue
- Title format: `[Task] T###: {description}`

**Constraint**: Every task MUST have a parent user story.

## Workflow Outline

### Phase 0: Retrieve Available Labels from Repository

1. **Fetch all labels from GitHub repository**:
   - Use GitHub API to list all labels in the target repository
   - Build a map of available labels: `label_name -> label_id`
   - Store canonical label names that exist (e.g., `user-story`, `task`, `us1`, `us2`, `us3`, `spec-001`, `spec-002`, `setup`, `foundational`, etc.)
   
2. **Map task phases and stories to compatible labels**:
   - Phase names: `setup`, `foundational`, `polish` (or variants if different labels exist in repo)
   - Story identifiers: `us1`, `us2`, `us3` (or variants)
   - Feature spec markers: `spec-001`, `spec-002` (or variants)
   - **Skip labels that do not exist in the repository** (do not create new labels)

3. **Report label compatibility**:
   - List all available labels with their canonical names
   - Indicate which task/phase/story labels will be used
   - Warn if critical labels like `user-story` or `task` are missing

### Phase 1: Parse tasks.md and Detect Existing Issues

1. **Read the local `tasks.md` file(s)**:
   - All specs may have separate tasks.md files (e.g., `specs/001/tasks.md`, `specs/002/tasks.md`)
   - Parse every checklist line matching format: `- [X or space] T### [optional tokens] Description`

2. **Extract and map task metadata**:
   - **Task ID**: T###
   - **Completion state**: `[X]` (done) or `[ ]` (pending)
   - **Story label**: Extract from `[US#]` marker if present
   - **Existing issue reference**: Scan line for `[#NNN]` token
     - If `[#NNN]` found: Store as **existing issue** (do NOT create duplicate)
     - If no `[#NNN]` found: Mark as **needs issue**
   - **Full description**: Everything after ID and markers
   - **Parallel flag**: Check for `[P]` marker

3. **Validate task-to-story mapping**:
   - **ERROR**: Any task without `[US#]` marker must fail with clear error message
   - Message: "Task T### has no parent story marker. Add `[US#]` to the line."
   - Do **NOT** proceed until all tasks have parent stories

### Phase 2: Check GitHub for Existing Issues by Reference

1. **For each task with existing `[#NNN]` reference in tasks.md**:
   - Retrieve the issue from GitHub API (get full details)
   - Verify issue exists and is accessible
   - **If issue not found**: Warn user and ask whether to:
     - Remove the stale `[#NNN]` reference and create a new issue
     - Manually provide correct issue number
     - Skip this task
   - **If issue found**: Continue to Phase 3 (update with proper labels and parent)

2. **Build mapping of existing references**:
   - Store: `T001 -> #42`, `T002 -> #43`, etc.
   - Separate: Issues to create vs. issues to update

### Phase 3: Create Missing Issues or Update Existing Ones

1. **For tasks WITHOUT existing `[#NNN]` reference**:
   - **Check if issue already exists in GitHub** by searching for task title (prevent accidental duplicates)
   - If found: Use that issue number and proceed to Phase 4 (update labels/parent)
   - If not found: **Create new issue** with:
     - **Title**: `[Task] T###: {task description}` (no internal markers)
     - **Body**: Task ID, phase, story parent, file references, spec path
     - **Labels**: `task` + compatible story label (`us1`, `us2`, etc.) + phase label (if exists)
     - **Do NOT create as sub-issue** — regular issue only
   - Capture returned issue number

2. **For tasks WITH existing `[#NNN]` reference**:
   - **Update existing issue** with:
     - Verify title matches expected format or update if needed
     - Add missing labels: `task` + story label + phase label (if compatible labels exist)
     - Ensure body has proper structure
   - Do NOT change issue state unless explicitly requested

### Phase 4: Create/Link User Story Parent Issues

1. **Extract user story definitions from tasks.md**:
   - Look for lines with `## Phase N: User Story #` or similar section headers
   - Or infer from collected tasks: Map all unique `[US#]` values found
   - Each story should have: ID (US#), description, list of task IDs

2. **For each user story**:
   - Check if issue already exists in GitHub (search by `[Story] US#:` title pattern)
   - **If found**: Verify it has `user-story` label and has the right description
   - **If not found**: Create new issue with:
     - **Title**: `[Story] US#: {user story description}`
     - **Body**: User story description, feature spec path, related task IDs summary
     - **Labels**: `user-story` + compatible feature label (e.g., `spec-001`)
   - Store mapping: `US1 -> #52`, `US2 -> #53`, etc.

### Phase 5: Link Tasks as Sub-Issues to Parent Stories

1. **For each task issue (created or existing)**:
   - Determine parent story from task-to-story mapping: `T001 -> US1`
   - Look up parent story issue number: `US1 -> #52`
   - Result: Link task issue `#4` as sub-issue of story issue `#52`

2. **Use GitHub API to establish sub-issue relationship**:
   - Check if relationship already exists (prevent duplicates)
   - Create relationship if missing
   - Verify both issues exist before linking

3. **Verify no orphaned tasks**:
   - Every task issue MUST have exactly one parent user story issue
   - Fail with clear error if any task has no parent

### Phase 6: Update Local tasks.md with Issue Tokens

1. **Read the local `tasks.md` file(s)** again (may have changed during processing)

2. **For each checklist line**:
   - Determine task/story ID from line
   - Look up corresponding GitHub issue number from collected mappings
   - If no `[#NNN]` token exists: **Insert** `[#NNN]` after the ID token
   - If `[#NNN]` token exists: **Verify** it matches the mapping (idempotent)

3. **Format rules for token insertion**:
   - ✅ CORRECT: `- [ ] T001 [#42] [P] [US1] Description...`
   - ✅ CORRECT: `- [X] US1 [#52] Discover Available NDI Sources`
   - ❌ WRONG: `- [ ] T001 [US1] [#42] Description...` (token after markers)
   - ❌ WRONG: `- [ ] T001 [#42] [#43]...` (duplicate tokens)
   - Insert immediately after the identifier (T### or US#), before any other markers

4. **Write updated file(s) back to disk atomically**:
   - Rewrite full file contents, not line-by-line patches
   - Preserve all formatting, comments, and structure
   - Verify no data loss

### Phase 7: Verify and Report

1. **Verify issue creation and updates**:
   - Count user story issues (created/linked/updated)
   - Count task issues (created/linked/updated)
   - Confirm no duplicates or orphans

2. **Verify hierarchy and labels**:
   - Every task issue has exactly one parent story issue ✓
   - Every task issue has `task` label ✓
   - Every task issue has story-specific label (`us1`, `us2`, etc.) ✓
   - Every story issue has `user-story` label ✓
   - All label names are compatible with repository ✓
   - All sub-issue relationships are established in GitHub ✓

3. **Verify local file update**:
   - All user story lines have `[#NN]` tokens ✓
   - All task lines have `[#NN]` tokens ✓
   - All tokens appear immediately after identifiers ✓
   - No duplicate tokens on any line ✓
   - File is properly formatted and readable ✓

4. **Generate summary report**:
   - Table of user stories: ID, issue number, title, status
   - Table of tasks: ID, issue number, title, parent story, status
   - Sample updated lines (first 5 and last 5 with new tokens)
   - Verification status: ✅ All verified or ⚠️ With warnings/errors
   - Links to a few issues in GitHub UI for spot-checking

## Decision Flow: Single Source of Truth

- **ONLY** this agent (`speckit.taskstoissues`) handles GitHub issue operations
- **Local `tasks.md` is authoritative** for task definitions and parent story mapping
- Do **NOT** contact other agents (speckit.tasks, speckit.analyze, etc.) for issue information
- If uncertainty arises:
  - Offer 2-5 candidate options to the user (issue number, title, reason)
  - Wait for user selection before proceeding
  - Do **NOT** guess or make assumptions

## Error Handling

If issue creation or sub-issue linking fails:
- Report the exact GitHub API error
- Do **NOT** retry with a different agent or workaround
- Ask user whether to:
  - Retry the failed operation
  - Skip issue linking for this pass
  - Provide manual issue number mappings

## Implementation Steps (Detailed)

### Step 0: Retrieve Repository Labels (FIRST)

Before any issue creation or update, **fetch all available labels from the target repository**:

1. **Get repository info**:
   - Run `git config --get remote.origin.url` to get remote URL
   - Extract owner and repo name from GitHub URL
   - Verify it's a valid GitHub repository

2. **Fetch all labels using GitHub API**:
   - Use `github-mcp-server` tool to list all labels in the repository
   - Store each label's name, description, and color
   - Build a canonical map: `label_name -> exists: true`

3. **Determine compatible label names**:
   - Check if `user-story` label exists (or variant like `user-story-1`)
   - Check if `task` label exists
   - Check if `us1`, `us2`, `us3` labels exist (or variants)
   - Check if spec labels exist: `spec-001`, `spec-002`, etc.
   - Check if phase labels exist: `setup`, `foundational`, `polish`, etc.
   - Record which labels exist and which will be skipped

4. **Report label findings**:
   - Print all available labels found in repository
   - Print mapping of intended task/story/phase labels to actual labels available
   - Note any critical labels that are missing

### Step 1: Parse tasks.md and Detect Existing Issues

1. **Locate and read all `tasks.md` files**:
   - Search for files matching pattern: `specs/*/tasks.md`
   - Read full contents of each file

2. **Parse every line matching task format**:
   ```text
   - [ ] T### [optional: #NNN] [optional: [P]] [optional: [US#]] Description text
   - [X] T### [optional: #NNN] [optional: [P]] [optional: [US#]] Description text
   ```

   For each line, extract and store:
   - **Task ID**: `T###` (e.g., `T001`)
   - **Completion**: `[ ]` (pending) or `[X]` (done)
   - **Existing issue**: Look for `[#NNN]` pattern — if found, this is the **existing issue number**
   - **Parallel**: Check for `[P]` marker
   - **Story parent**: Extract `[US#]` marker if present
   - **Description**: Full text after all markers (e.g., "Implement NdiSourceRepository...")

3. **Validate all tasks have parent stories**:
   - For every task found, verify `[US#]` marker is present
   - **If missing**: Report error: "T### has no parent story marker [US#]"
   - **Do NOT proceed** until all tasks have parent stories assigned

4. **Build in-memory maps**:
   - `task_id -> {completion, existing_issue, story_parent, description}`
   - `story_id -> {list_of_task_ids}`

### Step 2: Check GitHub for Existing Issues

1. **For each task with existing `[#NNN]` reference**:
   - Use GitHub API to retrieve the issue by number
   - **If issue not found**:
     - Warn user: "Issue #NNN referenced in task T### not found in GitHub"
     - Ask user: Skip, remove reference, or provide correct number?
     - Wait for response before continuing
   - **If issue found**:
     - Store: `T### -> {issue_number: NNN, found: true}`
     - Continue to next task

2. **For each task WITHOUT existing `[#NNN]` reference**:
   - Search GitHub for existing issue by title pattern: `[Task] T###:`
   - **If found** (issue exists but not linked in tasks.md):
     - Ask user: "Found existing issue #NN with matching title. Link it? [Y/N]"
     - If Y: Store issue number and update tasks.md
     - If N: Proceed to create new issue
   - **If not found**: Proceed to create new issue in next step

### Step 3: Create New Issues and Update Existing Issues

1. **Create missing task issues**:
   - For each task marked "needs_creation":
     - Use `issue_write` tool with method `create`
     - **Title**: `[Task] T###: {description}` (no internal markers)
     - **Body**: Include Task ID, spec path, file references, parent story ID
     - **Labels**: `task` + `us#` label (only if available from Step 0) + phase label (if available)
     - Capture returned issue number and store: `T### -> {issue_number: returned_number}`

2. **Update existing task issues**:
   - For each task with existing issue number (from tasks.md or GitHub search):
     - Use `issue_write` tool with method `update`
     - Add missing labels: `task` + `us#` + phase (use only compatible labels from Step 0)
     - Optionally update body if structure is missing
     - Do NOT change state or assignees unless explicitly requested

3. **Create/update user story parent issues**:
   - Collect all unique story IDs from parsed tasks
   - For each story ID (US1, US2, etc.):
     - Search GitHub for existing issue by title: `[Story] US#:`
     - **If found**: Get issue number, add `user-story` label (if available)
     - **If not found**: Create new issue:
       - **Title**: `[Story] US#: {story description from tasks.md}`
       - **Body**: Story description, feature spec path, summary of task IDs
       - **Labels**: `user-story` (if available) + feature spec label (if available)
       - Store: `US# -> {issue_number: returned_number}`

### Step 4: Link Tasks as Sub-Issues to Parent Stories

1. **For each task issue** (created or existing):
   - Determine parent story: `T### -> US# -> {issue_number: N}`
   - Use `sub_issue_write` tool with method `add`:
     - Parent issue: story issue number (N)
     - Child issue: task issue number (already collected)
   - **Before creating relationship**: Check if it already exists
     - Retrieve parent issue, inspect sub-issues list
     - Skip if relationship already exists

2. **Verify no orphaned tasks**:
   - Count all task issues: X
   - Count all task issues with parent relationships: Y
   - **If X ≠ Y**: Fail with error message listing orphaned tasks
   - **If X = Y**: Continue

### Step 5: Update Local tasks.md with Issue Tokens

1. **Read tasks.md file(s)** again (fresh read, may have changed):
   - Preserve original formatting
   - Detect encoding

2. **For each line in tasks.md**:
   - Identify if it's a task line or story line
   - Extract the ID (T### or US#)
   - Look up corresponding issue number from collected mappings
   - **If no `[#NNN]` token exists on line**:
     - Insert `[#NNN]` immediately after the ID token
     - Example: `- [ ] T001 [#42] [P] [US1] Description...`
   - **If `[#NNN]` token exists**:
     - Verify it matches the mapping (idempotent)
     - If mismatch: Report warning and ask user

3. **Write updated file(s) to disk**:
   - Atomic write: Rewrite full file contents
   - Preserve all comments, headers, and formatting
   - Verify no data loss by comparing line count

### Step 6: Final Verification and Report

1. **Count and verify**:
   - User stories created: X
   - User stories updated: Y
   - Task issues created: A
   - Task issues updated: B
   - Total sub-issue links established: C
   - Tasks without parent: 0 (or fail if > 0)

2. **Verify GitHub state**:
   - Sample 3 user story issues: Verify `user-story` label ✓
   - Sample 3 task issues: Verify `task` + `us#` label ✓
   - Sample 3 sub-issue relationships: Verify they appear in GitHub UI ✓

3. **Verify local file state**:
   - Count lines with `[#NNN]` tokens: Should match total user stories + tasks
   - Check for duplicate tokens on any line: Should be 0
   - Verify formatting integrity

4. **Generate and display summary report**:
   ```
   ✅ PHASE 0: Label Compatibility
   Available labels: user-story, task, us1, us2, us3, spec-001, spec-002, setup, foundational
   Compatible task labels: task, us1, us2, us3, spec-001, setup
   
   ✅ PHASE 1-2: Parsed and Detected
   Tasks parsed: 65 (spec-001) + 67 (spec-002) = 132 total
   Tasks with existing [#NNN]: 12
   Tasks needing new issues: 120
   
   ✅ PHASE 3: Issues Created/Updated
   New issues created: 120
   Existing issues updated: 12
   Total: 132 task issues ready
   
   ✅ PHASE 4: Sub-Issue Links
   Sub-issue relationships created: 132
   Orphaned tasks: 0
   
   ✅ PHASE 5: Local File Updated
   - specs/001-scan-ndi-sources/tasks.md: Updated with [#NNN] tokens
   - specs/002-stream-ndi-source/tasks.md: Updated with [#NNN] tokens
   
   Sample updated lines:
   - [X] T001 [#65] [P] Align prerequisite package checks...
   - [X] T002 [#66] Update local setup guidance...
   ...
   - [ ] T005 [#69] Record release-readiness validation matrix...
   
   ✅ Verification: All verified
   → Spot-check issue #65: https://github.com/owner/repo/issues/65
   → Spot-check issue #66: https://github.com/owner/repo/issues/66
   ```

1. **Run `.specify/scripts/bash/check-prerequisites.sh --json --require-tasks --include-tasks`** from repo root and parse FEATURE_DIR and AVAILABLE_DOCS list. All paths must be absolute. For single quotes in args like "I'm Groot", use escape syntax: e.g 'I'\''m Groot' (or double-quote if possible: "I'm Groot").

2. **From the executed script, extract the path to `tasks.md` and read its full contents**. Parse every task line that matches the checklist format:

   ```text
   - [ ] T### [optional markers] Description
   - [X] T### [optional markers] Description   ← already completed tasks
   ```

   Build an in-memory list of tasks, each capturing:
   - **Task ID** (e.g. `T001`)
   - **Completion state** (`[ ]` or `[X]`)
   - **Existing issue reference** — scan for a `[#NNN]` token already present in the line; if found, this task already has an issue and must be **skipped** (do not create a duplicate)
   - **Full description text** (everything after the ID and markers)
   - **Phase and story labels** (e.g. `[P]`, `[US1]`)

3. **Get the Git remote by running**:

   ```bash
   git config --get remote.origin.url
   ```

   > [!CAUTION]
   > ONLY PROCEED TO NEXT STEPS IF THE REMOTE IS A GITHUB URL

4. **For each task that does `not` already have a `[#NNN]` issue reference, use the GitHub MCP server to create a new issue in the repository matching the Git remote**.

   Each issue must include:
   - **Title**: the task description (without internal markers like `[P]`, `[US1]`)
   - **Body**: structured content linking back to the feature — include the Task ID, phase, story label (if any), and a reference to the feature spec file path from FEATURE_DIR
   - **Labels**: map phase/story markers to labels where the repository has matching labels (e.g. `user-story-1`, `setup`, `polish`); skip labelling silently if labels don't exist

   Capture the **GitHub issue number** returned for each created issue.

   > [!CAUTION]
   > UNDER NO CIRCUMSTANCES EVER CREATE ISSUES IN REPOSITORIES THAT DO NOT MATCH THE REMOTE URL

5. **Write issue IDs back into tasks.md** — this is the critical collaboration step that connects GitHub issues to the feature specification.

   For each task where an issue was just created (or already existed and was parsed in step 2), update the corresponding line in tasks.md by inserting `[#NNN]` immediately after the Task ID token:

   **Before**:
   ```text
   - [ ] T004 [P] [US1] Implement NdiSourceRepository in feature/ndi-browser/data/...
   ```

   **After**:
   ```text
   - [ ] T004 [#42] [P] [US1] Implement NdiSourceRepository in feature/ndi-browser/data/...
   ```

   Rules for the writeback:
   - Insert `[#NNN]` between the Task ID and the next token (or description if no other tokens follow).
   - Do not alter the checkbox state, Task ID, or any other part of the line.
   - Do not add a second `[#NNN]` if one is already present (idempotent).
   - Write the updated tasks.md back to disk atomically — rewrite the full file, not line-by-line patches.

6. **Make sure all created issues are properly linked to the feature specification and any relevant design artifacts**.

7. **Output a summary table**:

   | Task ID | Issue | Title (truncated) | Status |
   |---------|-------|-------------------|--------|
   | T001    | #12   | Create project… | ✅ Created |
   | T002    | #13   | Implement…       | ✅ Created |
   | T005    | #9    | (pre-existing)   | ⏭ Skipped |

   Followed by the path to the updated tasks.md and confirmation that all issue IDs are now embedded in it.