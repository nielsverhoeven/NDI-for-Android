# Data Model: Fluent + Electron UX Redesign

**Branch**: `032-fluent-electron-redesign` | **Date**: 2026-04-27

## Core Entities

### DesignLanguageTokenSet

Canonical visual token definitions for Fluent + Electron consistency.

| Field | Type | Description |
|---|---|---|
| `version` | `String` | Token set version identifier used in artifacts. |
| `typographyRoles` | `Map<String, String>` | Role-to-style mapping for titles/body/labels/captions. |
| `spacingScale` | `Map<String, Int>` | Named spacing values for layout rhythm. |
| `colorRoles` | `Map<String, String>` | Semantic color roles for surfaces, text, states. |
| `elevationRoles` | `Map<String, String>` | Elevation/shadow hierarchy roles. |
| `interactionStates` | `Map<String, String>` | Hover/press/focus/disabled treatment rules. |
| `mappingNotes` | `List<String>` | Explicit rationale when retaining Material defaults. |

Validation rules:

1. Token roles must be reused across all in-scope redesigned surfaces (FR-001, FR-002).
2. Any retained Material default requires explicit mapping rationale (FR-018).

### RedesignedScreenContract

Defines required visual/interaction behavior for one in-scope screen.

| Field | Type | Description |
|---|---|---|
| `screenId` | `String` | Unique screen identifier (SourceList, Viewer, Output, Settings, NavShell). |
| `flowId` | `String` | End-to-end flow that the screen belongs to. |
| `statePatterns` | `List<String>` | Required loading/success/empty/error patterns. |
| `primaryActions` | `List<String>` | Actions that must remain reachable. |
| `adaptiveRules` | `List<String>` | Phone/tablet/orientation/text-scale adaptation rules. |
| `a11yRules` | `List<String>` | Readability, focusability, and contrast expectations. |
| `legacyMixAllowed` | `Boolean` | Must be false for shipped in-scope flows. |

Validation rules:

1. In shipped flows, `legacyMixAllowed` is always false (FR-019).
2. State patterns and primary actions must be complete for all in-scope screens (FR-001a, FR-006).

### FlowRedesignSlice

Represents a phased rollout unit delivered as a complete user flow.

| Field | Type | Description |
|---|---|---|
| `sliceId` | `String` | Unique phase identifier. |
| `includedFlows` | `List<String>` | Complete flows included in this phase. |
| `includedScreens` | `List<String>` | In-scope screens covered by this phase. |
| `entryCriteria` | `List<String>` | Preconditions for implementation start. |
| `exitCriteria` | `List<String>` | Required tests/evidence before merge. |
| `status` | `Planned/InProgress/Complete` | Delivery state. |

Validation rules:

1. Each slice must include complete user flows, not partial flow fragments (FR-019).
2. Exit criteria must include Playwright coverage and compliance evidence (FR-008, FR-010a).

### VisualComplianceEvidenceRecord

Evidence artifact metadata for per-screen Fluent + Electron conformance.

| Field | Type | Description |
|---|---|---|
| `artifactPath` | `String` | Path under `test-results/` for this evidence file. |
| `screenId` | `String` | Referenced redesigned screen. |
| `checklistItems` | `List<String>` | Per-screen compliance checklist outcomes. |
| `testEvidenceLinks` | `List<String>` | Traceable links/paths to Playwright/log outputs. |
| `classification` | `Pass/CodeFailure/BlockedEnvironment` | Final validation status. |
| `updatedAtEpochMillis` | `Long` | Last update timestamp. |

Validation rules:

1. One or more records per in-scope screen are required before merge (FR-010, SC-001).
2. Blocked items must include exact blocker and remediation notes (FR-012).

### AdaptiveViewContext

Execution context used for adaptive and accessibility validation.

| Field | Type | Description |
|---|---|---|
| `profileId` | `String` | Device profile identifier used in validation. |
| `formFactor` | `Phone/Tablet` | Target form factor. |
| `orientation` | `Portrait/Landscape` | Runtime orientation. |
| `fontScale` | `Float` | Accessibility text scale used in test. |
| `runtimeFixture` | `String` | NDI fixture/environment descriptor. |

Validation rules:

1. Validation must include at least one phone and one tablet context (FR-015).
2. Increased text scale contexts must be included in accessibility checks (FR-016).

## Relationships

```text
DesignLanguageTokenSet 1 --- * RedesignedScreenContract
FlowRedesignSlice 1 --- * RedesignedScreenContract
RedesignedScreenContract 1 --- * VisualComplianceEvidenceRecord
AdaptiveViewContext * --- * VisualComplianceEvidenceRecord
```

## State Transitions

```text
FlowRedesignSlice: Planned -> InProgress -> Complete
  Complete requires:
    - Playwright coverage pass for changed flows
    - Existing Playwright regression pass
    - VisualComplianceEvidenceRecord pass for included screens
```

## Persistence and Reporting Notes

1. No new Room schema is required for this redesign model; entities are planning/validation contracts.
2. Evidence records are stored as markdown artifacts under `test-results/`.
3. Behavior regressions and environment blockers are reported separately in validation outputs.
