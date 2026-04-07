# Contract: NDI Discovery Compatibility Classification

## Purpose
Define the contract for classifying and reporting NDI Discovery Server compatibility outcomes across tested server targets.

## Inputs
- Target server endpoint and version metadata (if available)
- Discovery execution result for target
- Stream-start verification result for target
- Environment preflight status

## Output Schema

### CompatibilityResultRecord
- targetId: string
- versionString: string | null
- status: compatible | limited | incompatible | blocked
- temporaryUnknownObserved: boolean
- discoveredSourceCount: integer >= 0
- streamStartAttempted: boolean
- streamStartSucceeded: boolean
- failureCategory: none | compatibility | endpoint_unreachable | network | environment | unknown
- message: string
- recommendedNextStep: string | null
- evidenceRef: string
- recordedAtEpochMillis: long

## Classification Rules
1. compatible
- Discovery returns at least one usable source and stream start succeeds for the validated flow.

2. limited
- Discovery returns usable source(s), but support is discovery-only in validated behavior.

3. incompatible
- Stream start fails at least once in an otherwise ready environment for that target.

4. blocked
- Validation cannot complete due to environment or endpoint availability blockers.

5. temporary unknown
- May appear only before enough evidence is gathered; final recorded status must be one of compatible, limited, incompatible, blocked.

## Mixed-Server Reporting Rule
When multiple configured discovery servers are used in one run:
- Preserve sources from compatible targets.
- Emit non-compatible target records independently.
- Do not report an overall fully successful compatibility outcome if any target is incompatible or blocked.

## Backward Compatibility Expectations
- Existing diagnostics surfaces remain primary output channels.
- No dedicated compatibility UI is required by this contract.
- Existing automated tests remain regression protection and are modified only when directly impacted by requirement changes.
