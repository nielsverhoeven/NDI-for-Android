# Data Model: Fluent Button Radius Alignment

**Branch**: `033-fluent-button-radius` | **Date**: 2026-04-27

## Core Entities

### Button Surface Variant

Represents a user-visible app button instance included in this feature scope.

| Field | Type | Description |
|---|---|---|
| `flowId` | `String` | One of: Home, SourceList, Viewer, Output, Settings |
| `role` | `String` | Button role (primary, secondary, tonal, outlined) |
| `state` | `String` | Visual state (default, pressed, focused, disabled) |
| `cornerProfileId` | `String` | Identifier for the canonical less-rounded profile |
| `themeMode` | `String` | light or dark |
| `layoutMode` | `String` | compact or wide |

Validation rules:

1. Every in-scope button variant must map to the same `cornerProfileId`.
2. Visual states must not introduce alternate legacy corner geometry.

### Corner Profile Specification

Defines the canonical button shape profile expected in this feature.

| Field | Type | Description |
|---|---|---|
| `profileId` | `String` | Unique id for the selected corner profile |
| `radiusDp` | `Int` | Corner radius value used for all in-scope button variants |
| `fluentAlignmentNote` | `String` | Human-readable design rationale |
| `appliesToRoles` | `List<String>` | Button roles covered by this profile |

Validation rules:

1. Exactly one profile is active for in-scope flow buttons and its `radiusDp` value is 8.
2. Any role excluded from `appliesToRoles` must be out-of-scope or justified in evidence.

### Visual Compliance Evidence Record

Captures validation evidence for this feature’s quality gates.

| Field | Type | Description |
|---|---|---|
| `artifactPath` | `String` | Path under `test-results/` |
| `flowId` | `String` | Flow validated |
| `gate` | `String` | Validation gate type (playwright-flow, regression, preflight, release-hardening) |
| `status` | `Pass/CodeFailure/BlockedEnvironment` | Final classification |
| `details` | `List<String>` | Evidence notes, command references, blocker remediation |

Validation rules:

1. Each in-scope flow requires at least one evidence record.
2. Blocked records must include explicit remediation command(s).

## Relationships

```text
Corner Profile Specification 1 --- * Button Surface Variant
Button Surface Variant 1 --- * Visual Compliance Evidence Record
```

## State/Outcome Transitions

```text
Visual Compliance Evidence Record:
  Pending -> Pass
  Pending -> CodeFailure
  Pending -> BlockedEnvironment
```

## Persistence Notes

1. No Room/entity schema changes are required.
2. Data model serves planning/validation artifacts only.
3. Evidence is persisted as markdown artifacts under `test-results/`.
