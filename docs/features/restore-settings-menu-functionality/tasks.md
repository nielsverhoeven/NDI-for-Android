# Tasks: Restore Missing and Broken Settings Menu Functionality

## Summary
- Total tasks: 10
- Layers covered: Data, Repository, ViewModel, View, Platform, NDI Bridge, Test, Docs, Issue Management
- GitHub issue: #142
- Scope correction: 2026-06-06 screenshot parity re-baseline

## Dependency Graph
T001 -> T002
T002 -> T003, T004, T005, T006, T007
T003 -> T008
T004 -> T008
T005 -> T008
T006 -> T008
T007 -> T008
T008 -> T009
T009 -> T010

## Task List

### T001: Define settings baseline data contracts and persistence defaults
- Layer: Data
- Description: Build screenshot-driven baseline inventory and map each visible section/control to domain models and persistence fields, including pending staged values for Apply workflow.
- Depends on: none
- Acceptance: Each screenshot-visible control is mapped to a typed field with deterministic defaults and compatibility behavior.
- GitHub issue: #201

### T002: Implement repository compatibility and persistence hardening for settings
- Layer: Repository
- Description: Implement repository load/save for full section set with explicit staged-to-persisted Apply semantics and malformed-data fallback behavior.
- Depends on: T001
- Acceptance: Repository supports atomic apply of staged changes and safe legacy fallback without runtime exceptions.
- GitHub issue: #202

### T003: Implement General section and global Apply workflow behavior
- Layer: ViewModel
- Description: Add General detail panel state and guidance text plus global Apply command behavior (dirty tracking, disabled/enabled transitions, apply completion feedback).
- Depends on: T002
- Acceptance: Apply remains disabled when unchanged and enables only when valid staged changes exist.
- GitHub issue: #203

### T004: Implement Appearance section parity controls
- Layer: View + ViewModel
- Description: Implement Appearance detail pane with theme radio options (`Light`, `Dark`, `System default`) and accent color options (`Blue`, `Teal`, `Green`, `Orange`, `Red`, `Pink`) with staged selection behavior.
- Depends on: T002
- Acceptance: Appearance panel matches screenshot options and selections persist only via Apply.
- GitHub issue: #204

### T005: Implement Discovery section parity controls and list actions
- Layer: View + ViewModel + Service
- Description: Implement Discovery detail pane with host input, port input (default 5959 hint), Add Server action, server rows with reorder affordance, enable toggle, edit, and delete actions.
- Depends on: T002
- Acceptance: Discovery panel interaction model matches screenshot and persists through Apply.
- GitHub issue: #205

### T006: Implement Developer tools section parity and cached registry rendering
- Layer: View + ViewModel + Platform/Service
- Description: Implement Developer Settings detail pane with Developer Mode toggle and Cached Source Registry list entries showing source name, endpoint, state, key, and last-seen timestamp.
- Depends on: T002
- Acceptance: Developer tools pane mirrors screenshot structure and data fields without leaking native types.
- GitHub issue: #206

### T007: Implement About section parity content
- Layer: View + Service
- Description: Implement About detail pane with `App version` display in version/build format consistent with screenshot behavior.
- Depends on: T002
- Acceptance: About pane shows app version string in `x.y.z (build)` style.
- GitHub issue: #207

### T008: Rebuild settings shell composition to exact two-pane sectioned menu
- Layer: View + Navigation
- Description: Compose the complete sectioned settings menu layout (left category cards and right detail pane) and wire category selection to corresponding detail panel content from T003-T007.
- Depends on: T003, T004, T005, T006, T007
- Acceptance: Device UI visibly resembles issue screenshots and category switching updates right pane content correctly.
- GitHub issue: #208

### T009: Add screenshot-parity tests and device verification checklist
- Layer: Test
- Description: Add or update tests for category visibility, right-pane content parity, Apply enablement behavior, and persisted settings across restart; capture side-by-side screenshot checklist evidence.
- Depends on: T008
- Acceptance: Test and manual evidence confirms screenshot-level parity and persistence behavior.
- GitHub issue: #209

### T010: Update issue and closure workflow with corrected parity status
- Layer: Issue Management
- Description: Reopen and sync task issue states to reflect corrected screenshot parity scope, then post implementation evidence and remaining gaps to parent issue #142.
- Depends on: T009
- Acceptance: Parent issue reflects corrected status and only closes when screenshot-parity checklist passes.
- GitHub issue: #210

## Stage 5 Execution Status (Rebased 2026-06-06)

- Previous completion claims are superseded by screenshot-parity re-baseline.
- Task status must be re-evaluated against updated acceptance criteria in T001-T010.

### Current run status (feature/142-recreate-settings-menu)

- [x] T001 / #201: Data contracts and defaults updated for screenshot parity baseline.
- [x] T002 / #202: Repository and persistence compatibility path updated (including additive defaults).
- [x] T003 / #203: Global staged Apply behavior implemented (dirty tracking and validation-gated enablement).
- [x] T004 / #204: Appearance section parity implemented (theme radios plus accent color set).
- [x] T005 / #205: Discovery parity implemented (host or port, add/update, reorder, toggle, edit, delete).
- [x] T006 / #206: Developer tools parity implemented (Developer Mode and cached source registry fields).
- [x] T007 / #207: About parity implemented (version plus build display).
- [x] T008 / #208: Two-pane category menu composition implemented.
- [x] T009 / #209: Settings parity and Apply behavior unit tests updated and passing.
- [ ] T010 / #210: GitHub status sync is pending manual comment or close actions from this run.
