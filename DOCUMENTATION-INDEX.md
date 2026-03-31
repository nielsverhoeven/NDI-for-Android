# Improved Test Workflow - Documentation Index

## Quick Navigation

### 🚀 Start Here (First Time)
1. **[OPERATING-INSTRUCTIONS-IMPROVED.md](.github/agents/OPERATING-INSTRUCTIONS-IMPROVED.md)** - Core principles and phase template
2. **[TEST-EXECUTION-CHECKLIST.md](TEST-EXECUTION-CHECKLIST.md)** - Before/during/after checklist
3. **[scripts/validate-tests.ps1](scripts/validate-tests.ps1)** - Run automated validation

### 📚 Detailed References
- **[test-execution-guidelines.md](.github/agents/test-execution-guidelines.md)** - Best practices, patterns, error handling
- **[test-workflow-instructions.md](.github/agents/test-workflow-instructions.md)** - Step-by-step phase details
- **[WORKFLOW-IMPROVEMENTS-SUMMARY.md](WORKFLOW-IMPROVEMENTS-SUMMARY.md)** - What was improved and why

### 🔍 Analysis & Learning
- **[TEST-WORKFLOW-ANALYSIS.md](TEST-WORKFLOW-ANALYSIS.md)** - What went wrong, lessons learned
- **[test-results/android-test-results.md](test-results/android-test-results.md)** - Current test status

---

## Document Purposes

### OPERATING-INSTRUCTIONS-IMPROVED.md
**Purpose**: Define core operating principles for agents
**Use When**: Before any test execution
**Length**: Quick read (~5 minutes)
**Key Sections**:
- Core principles (1 terminal, sequential phases)
- Phase-based execution template
- Pitfalls to avoid
- Test assertion best practices
- Reporting format

### test-execution-guidelines.md
**Purpose**: Detailed guidance for test validation
**Use When**: Debugging failures, understanding test patterns
**Length**: Reference (~10 minutes)
**Key Sections**:
- Minimize terminal sessions
- Sequential execution rules
- Test-specific behaviors (unit tests, emulators)
- Assertion patterns
- Error handling strategies

### test-workflow-instructions.md
**Purpose**: Complete step-by-step workflow reference
**Use When**: Manually executing phases
**Length**: Reference (~15 minutes)
**Key Sections**:
- 5 sequential phases (setup → E2E)
- Skip conditions for each phase
- Common issues table
- Success criteria

### validate-tests.ps1
**Purpose**: Automated single-command test validation
**Use When**: Just want tests validated
**Length**: 2-5 minutes execution
**Runs**: Phases 1-4 automatically

### WORKFLOW-IMPROVEMENTS-SUMMARY.md
**Purpose**: High-level overview of improvements
**Use When**: Onboarding, understanding changes
**Length**: Quick read (~5 minutes)
**Includes**: Problems found, solutions delivered, key improvements

### TEST-WORKFLOW-ANALYSIS.md
**Purpose**: Complete analysis of what went wrong
**Use When**: Understanding root causes, preventing recurrence
**Length**: Detailed read (~20 minutes)
**Sections**: Issues, impacts, learnings, validation checklist

### TEST-EXECUTION-CHECKLIST.md
**Purpose**: Interactive checklist for test runs
**Use When**: Actually running tests
**Length**: Used during execution
**Covers**: Before/during/after test phases

---

## Common Workflows

### Scenario 1: "Just validate unit tests"
```
1. Read: OPERATING-INSTRUCTIONS-IMPROVED.md (Phase 2 only)
2. Run: .\gradlew.bat test --no-daemon
3. Check: BUILD SUCCESSFUL
4. Done!
```
**Time**: 5 minutes

### Scenario 2: "Build and validate locally"
```
1. Run: .\scripts\validate-tests.ps1
2. Check output for all ✓ marks
3. Done!
```
**Time**: 3-5 minutes

### Scenario 3: "Debug failing test"
```
1. Read: test-execution-guidelines.md → Assertion section
2. Find test file
3. Read assertion comment
4. Compare input/transformation/output
5. Fix assertion or code
6. Re-run: .\gradlew.bat test --no-daemon
7. Done!
```
**Time**: 10-15 minutes

### Scenario 4: "Full E2E validation"
```
1. Read: OPERATING-INSTRUCTIONS-IMPROVED.md (all phases)
2. Read: TEST-EXECUTION-CHECKLIST.md (all phases)
3. Follow phases 1-5 sequentially
4. Done!
```
**Time**: 15-20 minutes

### Scenario 5: "Understand what went wrong before"
```
1. Read: TEST-WORKFLOW-ANALYSIS.md (complete)
2. Review: test-execution-guidelines.md (error handling)
3. Document lessons for team
```
**Time**: 20-30 minutes

---

## Key Improvements Over Previous Approach

| Aspect | Before | After |
|--------|--------|-------|
| Terminal Sessions | 13+ | 1-2 |
| Parallelization | Yes (causes issues) | No (sequential) |
| Rate Limiting | Frequent | Never |
| Time to Validation | 30+ minutes | 3-5 minutes |
| Success Clarity | Unclear | Clear |
| Error Recovery | Trial & error | Structured approach |
| Documentation | Missing | Comprehensive |
| Reusability | Low | High |

---

## For Different Audiences

### 👨‍💻 For Developers
- Start with: `OPERATING-INSTRUCTIONS-IMPROVED.md`
- Reference: `test-execution-guidelines.md` for patterns
- Use: `validate-tests.ps1` for quick checks

### 🤖 For AI Agents/Copilot
- Use: `OPERATING-INSTRUCTIONS-IMPROVED.md` as system prompt
- Follow: Phase-based template exactly
- Report: Using provided format
- Never: Violate "one terminal" rule

### 👥 For Team Leads
- Share: `WORKFLOW-IMPROVEMENTS-SUMMARY.md` 
- Reference: `TEST-WORKFLOW-ANALYSIS.md` when discussing failures
- Track: Success using provided checklist

### 📊 For DevOps/CI
- Implement: Phase structure in CI pipelines
- Use: `test-workflow-instructions.md` for CI step definitions
- Monitor: Following success criteria from guidelines

---

## File Locations Quick Reference

```
C:\gitrepos\NDI-for-Android\
├── .github\agents\
│   ├── OPERATING-INSTRUCTIONS-IMPROVED.md    ← START HERE
│   ├── test-execution-guidelines.md
│   └── test-workflow-instructions.md
├── scripts\
│   └── validate-tests.ps1                    ← RUN THIS
├── WORKFLOW-IMPROVEMENTS-SUMMARY.md
├── TEST-WORKFLOW-ANALYSIS.md
├── TEST-EXECUTION-CHECKLIST.md              ← USE WHILE TESTING
└── test-results\
    └── android-test-results.md
```

---

## When to Update These Documents

### Add to test-execution-guidelines.md
- New test assertion patterns discovered
- New error scenarios encountered
- Better error recovery strategies

### Add to test-workflow-instructions.md
- New phases required
- New skip conditions
- New common issues

### Update OPERATING-INSTRUCTIONS-IMPROVED.md
- Core principles change (rare)
- New critical rules discovered
- Simplifications to workflow

### Update TEST-EXECUTION-CHECKLIST.md
- New pre-test requirements
- New success indicators
- New troubleshooting steps

---

## Contact/Questions

If you find something unclear:
1. Check if there's a relevant document above
2. Read TEST-WORKFLOW-ANALYSIS.md for context
3. Reference test-execution-guidelines.md for patterns
4. Ask team with document reference

---

## Feature Specifications Index

### 001 – Scan NDI Sources
- Spec: `specs/001-scan-ndi-sources/spec.md`
- Tasks: `specs/001-scan-ndi-sources/tasks.md`
- Contract: `specs/001-scan-ndi-sources/contracts/ndi-feature-contract.md`

### 002 – Stream NDI Source
- Spec: `specs/002-stream-ndi-source/spec.md`
- Tasks: `specs/002-stream-ndi-source/tasks.md`
- Contract: `specs/002-stream-ndi-source/contracts/ndi-output-feature-contract.md`

### 005 – Background Stream Persistence
- Spec: `specs/005-background-stream-persistence/spec.md`
- Plan: `specs/005-background-stream-persistence/plan.md`
- Tasks: `specs/005-background-stream-persistence/tasks.md` (all tasks complete)
- Quickstart: `specs/005-background-stream-persistence/quickstart.md`
- Contract: `specs/005-background-stream-persistence/contracts/ndi-background-stream-persistence-contract.md`
- Validation: `specs/005-background-stream-persistence/validation/` (US1, US2, US3 evidence)

**Summary**: Keeps NDI output streaming active when the broadcaster navigates to Home or another app.
Dual-emulator e2e validates six-step Chrome/nos.nl cross-app propagation with visual similarity proof.

### 023 – Per-Source Frame Retention
- Spec: `specs/023-per-source-frame-retention/spec.md`
- Plan: `specs/023-per-source-frame-retention/plan.md`
- Tasks: `specs/023-per-source-frame-retention/tasks.md`
- Research: `specs/023-per-source-frame-retention/research.md`
- Data model: `specs/023-per-source-frame-retention/data-model.md`
- Quickstart: `specs/023-per-source-frame-retention/quickstart.md`
- Contract: `specs/023-per-source-frame-retention/contracts/per-source-frame-repository.md`

**Summary**: Replaces the single-slot source-list preview with session-scoped per-source thumbnail retention, backed by a 10-entry LRU cache while preserving the existing relaunch continuity path.

---

**Version**: 1.1  
**Last Updated**: 2026-03-20  
**Status**: Ready for use ✓


