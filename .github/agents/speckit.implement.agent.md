---
description: Execute the implementation plan by processing and executing all tasks defined in tasks.md
---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Pre-Execution Checks

**Check for extension hooks (before implementation)**:
- Check if `.specify/extensions.yml` exists in the project root.
- If it exists, read it and look for entries under the `hooks.before_implement` key
- If the YAML cannot be parsed or is invalid, skip hook checking silently and continue normally
- Filter to only hooks where `enabled: true`
- For each remaining hook, do **not** attempt to interpret or evaluate hook `condition` expressions:
  - If the hook has no `condition` field, or it is null/empty, treat the hook as executable
  - If the hook defines a non-empty `condition`, skip the hook and leave condition evaluation to the HookExecutor implementation
- For each executable hook, output the following based on its `optional` flag:
  - **Optional hook** (`optional: true`):
    ```
    ## Extension Hooks

    **Optional Pre-Hook**: {extension}
    Command: `/{command}`
    Description: {description}

    Prompt: {prompt}
    To execute: `/{command}`
    ```
  - **Mandatory hook** (`optional: false`):
    ```
    ## Extension Hooks

    **Automatic Pre-Hook**: {extension}
    Executing: `/{command}`
    EXECUTE_COMMAND: {command}
    
    Wait for the result of the hook command before proceeding to the Outline.
    ```
- If no hooks are registered or `.specify/extensions.yml` does not exist, skip silently

## Outline

1. Run `.specify/scripts/bash/check-prerequisites.sh --json --require-tasks --include-tasks` from repo root and parse FEATURE_DIR and AVAILABLE_DOCS list. All paths must be absolute. For single quotes in args like "I'm Groot", use escape syntax: e.g 'I'\''m Groot' (or double-quote if possible: "I'm Groot").

2. **Check checklists status** (if FEATURE_DIR/checklists/ exists):
   - Scan all checklist files in the checklists/ directory
   - For each checklist, count:
     - Total items: All lines matching `- [ ]` or `- [X]` or `- [x]`
     - Completed items: Lines matching `- [X]` or `- [x]`
     - Incomplete items: Lines matching `- [ ]`
   - Create a status table:

     ```text
     | Checklist | Total | Completed | Incomplete | Status |
     |-----------|-------|-----------|------------|--------|
     | ux.md     | 12    | 12        | 0          | ✓ PASS |
     | test.md   | 8     | 5         | 3          | ✗ FAIL |
     | security.md | 6   | 6         | 0          | ✓ PASS |
     ```

   - Calculate overall status:
     - **PASS**: All checklists have 0 incomplete items
     - **FAIL**: One or more checklists have incomplete items

   - **If any checklist is incomplete**:
     - Display the table with incomplete item counts
     - **STOP** and ask: "Some checklists are incomplete. Do you want to proceed with implementation anyway? (yes/no)"
     - Wait for user response before continuing
     - If user says "no" or "wait" or "stop", halt execution
     - If user says "yes" or "proceed" or "continue", proceed to step 3

   - **If all checklists are complete**:
     - Display the table showing all checklists passed
     - Automatically proceed to step 3

3. Load and analyze the implementation context:
   - **REQUIRED**: Read tasks.md for the complete task list and execution plan
   - **REQUIRED**: Read plan.md for tech stack, architecture, and file structure
   - **IF EXISTS**: Read data-model.md for entities and relationships
   - **IF EXISTS**: Read contracts/ for API specifications and test requirements
   - **IF EXISTS**: Read research.md for technical decisions and constraints
   - **IF EXISTS**: Read quickstart.md for integration scenarios

4. **Project Setup Verification**:
   - **REQUIRED**: Create/verify ignore files based on actual project setup:

   **Detection & Creation Logic**:
   - Check if the following command succeeds to determine if the repository is a git repo (create/verify .gitignore if so):

     ```sh
     git rev-parse --git-dir 2>/dev/null
     ```

   - Check if Dockerfile* exists or Docker in plan.md → create/verify .dockerignore
   - Check if .eslintrc* exists → create/verify .eslintignore
   - Check if eslint.config.* exists → ensure the config's `ignores` entries cover required patterns
   - Check if .prettierrc* exists → create/verify .prettierignore
   - Check if .npmrc or package.json exists → create/verify .npmignore (if publishing)
   - Check if terraform files (*.tf) exist → create/verify .terraformignore
   - Check if .helmignore needed (helm charts present) → create/verify .helmignore

   **If ignore file already exists**: Verify it contains essential patterns, append missing critical patterns only
   **If ignore file missing**: Create with full pattern set for detected technology

   **Common Patterns by Technology** (from plan.md tech stack):
   - **Node.js/JavaScript/TypeScript**: `node_modules/`, `dist/`, `build/`, `*.log`, `.env*`
   - **Python**: `__pycache__/`, `*.pyc`, `.venv/`, `venv/`, `dist/`, `*.egg-info/`
   - **Java**: `target/`, `*.class`, `*.jar`, `.gradle/`, `build/`
   - **C#/.NET**: `bin/`, `obj/`, `*.user`, `*.suo`, `packages/`
   - **Go**: `*.exe`, `*.test`, `vendor/`, `*.out`
   - **Ruby**: `.bundle/`, `log/`, `tmp/`, `*.gem`, `vendor/bundle/`
   - **PHP**: `vendor/`, `*.log`, `*.cache`, `*.env`
   - **Rust**: `target/`, `debug/`, `release/`, `*.rs.bk`, `*.rlib`, `*.prof*`, `.idea/`, `*.log`, `.env*`
   - **Kotlin**: `build/`, `out/`, `.gradle/`, `.idea/`, `*.class`, `*.jar`, `*.iml`, `*.log`, `.env*`
   - **C++**: `build/`, `bin/`, `obj/`, `out/`, `*.o`, `*.so`, `*.a`, `*.exe`, `*.dll`, `.idea/`, `*.log`, `.env*`
   - **C**: `build/`, `bin/`, `obj/`, `out/`, `*.o`, `*.a`, `*.so`, `*.exe`, `*.dll`, `autom4te.cache/`, `config.status`, `config.log`, `.idea/`, `*.log`, `.env*`
   - **Swift**: `.build/`, `DerivedData/`, `*.swiftpm/`, `Packages/`
   - **R**: `.Rproj.user/`, `.Rhistory`, `.RData`, `.Ruserdata`, `*.Rproj`, `packrat/`, `renv/`
   - **Universal**: `.DS_Store`, `Thumbs.db`, `*.tmp`, `*.swp`, `.vscode/`, `.idea/`

   **Tool-Specific Patterns**:
   - **Docker**: `node_modules/`, `.git/`, `Dockerfile*`, `.dockerignore`, `*.log*`, `.env*`, `coverage/`
   - **ESLint**: `node_modules/`, `dist/`, `build/`, `coverage/`, `*.min.js`
   - **Prettier**: `node_modules/`, `dist/`, `build/`, `coverage/`, `package-lock.json`, `yarn.lock`, `pnpm-lock.yaml`
   - **Terraform**: `.terraform/`, `*.tfstate*`, `*.tfvars`, `.terraform.lock.hcl`
   - **Kubernetes/k8s**: `*.secret.yaml`, `secrets/`, `.kube/`, `kubeconfig*`, `*.key`, `*.crt`

5. Parse tasks.md structure and extract:
   - **Task phases**: Setup, Tests, Core, Integration, Polish
   - **Task dependencies**: Sequential vs parallel execution rules
   - **Task details**: ID, description, file paths, parallel markers [P]
   - **Execution flow**: Order and dependency requirements
   - **Issue references per user story**: For each `[USN]` phase, scan all task lines in that phase for `[#NNN]` tokens and build a deduplicated list of GitHub issue numbers associated with that story. Store this as the story's **issue list** — it will be used in the commit message after the story is implemented and tested. If no `[#NNN]` tokens are present in tasks.md, the commit is still created but without issue-closing keywords.

6. Execute implementation following the task plan, **delegating to specialized agents** based on task type:

   - **Phase-by-phase execution**: Complete each phase before moving to the next
   - **Respect dependencies**: Run sequential tasks in order, parallel tasks [P] can run together
   - **Follow TDD approach**: Execute test tasks before their corresponding implementation tasks
   - **File-based coordination**: Tasks affecting the same files must run sequentially
   - **Validation checkpoints**: Verify each phase completion before proceeding

   **Agent Delegation Rules (Android project)**:

   | Task Type | Delegate To | When |
   |-----------|-------------|------|
  | NDI SDK integration contracts, bridge behavior, and protocol correctness | `ndi.expert` (with `android.app-builder`) | Any task that changes NDI behavior, SDK usage, source discovery/streaming semantics, or `ndi/sdk-bridge` contracts |
   | Core Android implementation (models, repositories, ViewModels, DI wiring, architecture) | `android.app-builder` | Any task touching `core/*`, `feature/*/data`, `feature/*/domain`, `ndi/sdk-bridge`, or `app/di` |
   | UI screens, layouts, Compose, navigation, accessibility | `frontend-dev` | Any task touching `feature/*/presentation`, layouts, navigation graphs, or screen-level UX |
   | Test writing/validation, build verification, instrumentation, e2e | `tester` | After every implementation phase and before marking a phase complete |
   | Architecture review, quality gates, security/design gaps | `reviewer` | After core implementation phases and before final completion sign-off |
   | Documentation (guides, README, module reference, feature docs) | `documenter` | After all user stories are committed and final gates pass |

   **Collaboration Workflow Per Phase**:

  1. **Implement** — Delegate NDI SDK integration requirements to `ndi.expert`, then execute code tasks with `android.app-builder` (core/data/domain) and `frontend-dev` (UI/presentation) as appropriate for the tasks in that phase.
   2. **Review** — After implementation tasks in a phase are done, invoke `reviewer` to assess architecture compliance, module boundary correctness, and quality gaps.
   3. **Test** — Invoke `tester` to run the applicable test stages (unit, instrumentation, e2e) and validate the phase output. Do not advance to the next phase until the tester signals a pass.
  4. **Fix loop** — If `reviewer` or `tester` surface issues, route NDI-specific findings through `ndi.expert` and hand implementation fixes back to `android.app-builder` or `frontend-dev`, then re-run review and test.
   5. **Commit** — Once the phase passes review and test, create a git commit for the completed user story. See **User Story Commit Rules** below.

   **User Story Commit Rules**:

   After `tester` and `reviewer` both pass for a `[USN]` phase, stage all files changed during that phase and create a commit using this exact format:

   ```
   feat(usN): <concise description of what the user story delivers>

   <optional 1–3 sentence summary of the key changes made in this story>

   <for each issue number in the story's issue list, one line per issue:>
   Closes #NNN

   Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
   ```

   Rules:
   - The subject line uses conventional commit format: `feat(usN):` where `N` is the user story number (e.g. `feat(us1):`, `feat(us2):`).
   - Include one `Closes #NNN` line per issue number collected from the story's issue list in step 5. This links the commit to its GitHub issue and closes it on merge.
   - If the story's issue list is empty (no `[#NNN]` tokens were found in tasks.md), omit the `Closes` lines entirely — do not fabricate issue numbers.
   - Always include the `Co-authored-by` trailer as the last line.
   - Do **not** create a commit for Setup or Polish phases — only for named user story phases (`[USN]`).
   - Do **not** commit if the tester or reviewer gate has not yet passed for that phase.

7. Implementation execution rules:
   - **Setup first**: Initialize project structure, dependencies, configuration — handle directly or via `android.app-builder`
   - **Tests before code**: Write contract/entity/integration tests first — delegate to `tester` for test scaffolding guidance
  - **Core development**: Implement models, services, repositories, ViewModels — delegate to `android.app-builder` and include `ndi.expert` whenever NDI SDK behavior is involved
   - **UI development**: Implement screens, navigation, accessibility — delegate to `frontend-dev`
  - **Integration work**: Database connections, DI wiring, NDI bridge integration — delegate NDI-specific integration details to `ndi.expert` and implementation to `android.app-builder`
   - **Polish and validation**: Final test run, performance checks, release hardening — delegate to `tester`, then `reviewer` for final sign-off

8. Progress tracking and error handling:
   - Report progress after each completed task
   - Halt execution if any non-parallel task fails
   - For parallel tasks [P], continue with successful tasks, report failed ones
   - Provide clear error messages with context for debugging
   - Suggest next steps if implementation cannot proceed
   - **IMPORTANT** For completed tasks, make sure to mark the task off as [X] in the tasks file.

9. Completion validation:
   - Verify all required tasks are completed
   - Check that implemented features match the original specification
   - Validate that tests pass and coverage meets requirements
   - Confirm the implementation follows the technical plan
   - Report final status with summary of completed work

   **Final Collaboration Gates (Android project)**:

   **Gate A — Final Test Run (delegate to `tester`)**:
   - Invoke `tester` to run all applicable stages (unit, instrumentation, e2e dual-emulator, release hardening).
   - Do not declare completion until `tester` confirms all release gates pass.
   - Route any failures back to `android.app-builder` or `frontend-dev` for fixes, then re-run `tester`.

   **Gate B — Architecture and Quality Sign-Off (delegate to `reviewer`)**:
   - Invoke `reviewer` to validate the completed implementation against architecture rules, module boundaries, lifecycle correctness, telemetry/retry semantics, and release hardening.
   - Address all critical/high findings; document any accepted lower-priority findings in the final report.

   **Gate C — Documentation (delegate to `documenter`)**:
   - Invoke `documenter` to produce or update the full Android documentation set: README, architecture guide, module reference, feature guides, NDI integration guide, testing guide, and developer setup guide.
   - `documenter` will read the live codebase directly and may invoke `android.app-builder` and `frontend-dev` handoffs to fill any gaps.
   - Do not declare full completion until `documenter` confirms all in-scope documentation is written and accurate.

   **Gate D — Final Summary**:
   - Report completed tasks, agent collaborations performed, test results, reviewer dispositions, and documentation produced.
   - Document any remaining open risks or follow-up recommendations.

Note: This command assumes a complete task breakdown exists in tasks.md. If tasks are incomplete or missing, suggest running `/speckit.tasks` first to regenerate the task list.

10. **Check for extension hooks**: After completion validation, check if `.specify/extensions.yml` exists in the project root.
    - If it exists, read it and look for entries under the `hooks.after_implement` key
    - If the YAML cannot be parsed or is invalid, skip hook checking silently and continue normally
    - Filter to only hooks where `enabled: true`
    - For each remaining hook, do **not** attempt to interpret or evaluate hook `condition` expressions:
      - If the hook has no `condition` field, or it is null/empty, treat the hook as executable
      - If the hook defines a non-empty `condition`, skip the hook and leave condition evaluation to the HookExecutor implementation
    - For each executable hook, output the following based on its `optional` flag:
      - **Optional hook** (`optional: true`):
        ```
        ## Extension Hooks

        **Optional Hook**: {extension}
        Command: `/{command}`
        Description: {description}

        Prompt: {prompt}
        To execute: `/{command}`
        ```
      - **Mandatory hook** (`optional: false`):
        ```
        ## Extension Hooks

        **Automatic Hook**: {extension}
        Executing: `/{command}`
        EXECUTE_COMMAND: {command}
        ```
    - If no hooks are registered or `.specify/extensions.yml` does not exist, skip silently
