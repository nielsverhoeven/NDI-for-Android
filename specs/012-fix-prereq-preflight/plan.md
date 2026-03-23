# Implementation Plan: Fix Prereq Preflight

**Branch**: `012-fix-prereq-preflight` | **Date**: March 23, 2026 | **Spec**: [specs/012-fix-prereq-preflight/spec.md](specs/012-fix-prereq-preflight/spec.md)
**Input**: Feature specification from `/specs/012-fix-prereq-preflight/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Modify the Android prerequisites verification script to automatically install missing Android SDK packages before validation, ensuring CI builds on pull requests to main succeed without manual intervention.

## Technical Context

**Language/Version**: PowerShell (Windows scripting)  
**Primary Dependencies**: Android SDK command-line tools (sdkmanager, avdmanager, emulator), JDK 21 (handled by GitHub Actions setup-java)  
**Storage**: N/A (no persistent data)  
**Testing**: PowerShell script unit testing, CI integration testing, manual verification on clean environments  
**Target Platform**: Windows (GitHub Actions windows-latest runners)  
**Project Type**: CI infrastructure automation script  
**Performance Goals**: Complete prerequisite installation within 5 minutes  
**Constraints**: Must work in headless CI environment, no user interaction, respect existing CI setup (JDK already configured)  
**Scale/Scope**: Single script modification with CI workflow integration

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [ ] MVVM-only presentation logic enforced (no UI/business logic leakage) - N/A (no UI changes)
- [ ] Single-activity navigation compliance maintained - N/A (no app navigation)
- [ ] Repository-mediated data access preserved - N/A (no data access)
- [ ] TDD evidence planned (Red-Green-Refactor with failing-test-first path) - N/A (infrastructure script)
- [ ] Unit test scope defined using JUnit - N/A (PowerShell script)
- [ ] Playwright e2e scope defined for end-to-end flows - N/A (no UI)
- [ ] For visual UI additions/changes: emulator Playwright e2e tests are explicitly planned - N/A
- [ ] For visual UI additions/changes: existing Playwright e2e regression run is explicitly planned - N/A
- [ ] Material 3 compliance verification planned for UI changes - N/A
- [ ] Battery/background execution impact evaluated - N/A
- [ ] Offline-first and Room persistence constraints respected (if applicable) - N/A
- [ ] Least-permission/security implications documented - N/A (CI environment)
- [ ] Feature-module boundary compliance documented - N/A
- [ ] Release hardening validation planned (R8/ProGuard + shrink resources) - N/A

## Project Structure

### Documentation (this feature)

```text
specs/012-fix-prereq-preflight/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
scripts/
└── verify-android-prereqs.ps1  # Modified to install missing prerequisites

.github/
└── workflows/
    └── android-ci.yml          # Updated preflight job if needed
```

**Structure Decision**: Modifying existing CI infrastructure files rather than creating new modules, as this is an optimization of existing build pipeline components.

## Complexity Tracking

None - this is a straightforward enhancement to existing CI scripts without introducing new architectural complexity.

## Phase 0: Research & Technical Feasibility

### Current State Analysis
- Review existing `verify-android-prereqs.ps1` script functionality
- Analyze CI workflow `android-ci.yml` preflight job
- Identify which prerequisites are commonly missing in CI
- Research Android SDK installation methods for CI environments
- Document current failure patterns and root causes

### Implementation Options Research
- Option 1: Modify PowerShell script to call sdkmanager for missing packages
- Option 2: Add GitHub Actions steps to pre-install Android SDK components
- Option 3: Use pre-built Android SDK images/caches
- Option 4: Hybrid approach: script detects and installs, with CI caching

### Risk Assessment
- Network dependency for downloads
- Disk space requirements
- Installation time impact on CI
- Compatibility with existing CI setup
- Error handling for installation failures

### Output: research.md

## Phase 1: Design & Architecture

### Data Model
- Prerequisite state tracking (installed vs missing)
- Installation result logging
- Error state management

### Architecture Design
- Extend existing verification script with installation functions
- Maintain backward compatibility for local development
- Add installation retry logic
- Implement progress reporting for CI

### Interface Contracts
- Installation function signatures
- Error handling contracts
- Logging interface

### Testing Strategy
- Unit tests for installation functions
- Integration tests in CI environment
- Manual testing on clean Windows environments

### Output: data-model.md, quickstart.md, contracts/

## Phase 2: Implementation Planning

### Task Decomposition
- Modify verification script to install missing packages
- Update CI workflow if additional steps needed
- Add error handling and logging
- Test implementation thoroughly

### Success Metrics
- 100% CI pass rate for preflight
- Installation time < 5 minutes
- Clear error messages on failures
- No regression in local development workflow

### Output: tasks.md (generated by /speckit.tasks)</content>
<parameter name="filePath">c:\github\NDI-for-Android\specs\012-fix-prereq-preflight\plan.md