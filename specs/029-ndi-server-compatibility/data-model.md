# Data Model: NDI Discovery Server Compatibility

## Entity: DiscoveryServerVersionTarget
- Purpose: Defines each discovery server version/environment included in validation.
- Fields:
  - targetId: string (stable identifier)
  - label: string (human-readable name)
  - versionString: string | null (reported version when available)
  - sourceType: enum (baseline, venue, additional-older)
  - endpointHost: string
  - endpointPort: int
  - obtainableForTest: boolean
  - notes: string
- Validation rules:
  - endpointHost must be non-empty.
  - endpointPort in [1..65535].
  - sourceType must be unique for baseline and venue.

## Entity: CompatibilityResult
- Purpose: Captures final compatibility classification per target.
- Fields:
  - targetId: string (FK -> DiscoveryServerVersionTarget.targetId)
  - status: enum (compatible, limited, incompatible, blocked)
  - temporaryUnknownObserved: boolean
  - discoveredSourceCount: int
  - streamStartAttempted: boolean
  - streamStartSucceeded: boolean
  - failureCategory: enum (none, compatibility, endpoint_unreachable, network, environment, unknown)
  - evidenceRef: string (path or artifact id)
  - recordedAtEpochMillis: long
- Validation rules:
  - status=limited requires discoveredSourceCount > 0 and streamStartSucceeded=false when attempted.
  - status=incompatible requires streamStartAttempted=true and streamStartSucceeded=false in a ready environment.
  - status=blocked requires failureCategory=environment or endpoint_unreachable plus unblock note.

## Entity: CompatibilityDiagnostic
- Purpose: Operator/maintainer-facing explanation mapped from CompatibilityResult.
- Fields:
  - targetId: string
  - category: enum (supported, discovery_only, unsupported, blocked, unknown_temporary)
  - message: string
  - recommendedNextStep: string
  - surfacedVia: enum (existing_overlay, settings_diagnostics, validation_artifact)
- Validation rules:
  - recommendedNextStep required for unsupported, blocked, and unknown_temporary.
  - category must be derivable from CompatibilityResult.

## Relationships
- DiscoveryServerVersionTarget 1 -> 1 CompatibilityResult (per validation cycle snapshot)
- CompatibilityResult 1 -> 1 CompatibilityDiagnostic

## State Transitions
- unknown_temporary -> compatible | limited | incompatible | blocked
- blocked -> compatible | limited | incompatible after unblocked rerun
- limited -> compatible if stream-start succeeds in subsequent validated run
