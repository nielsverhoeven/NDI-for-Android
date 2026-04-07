# 029 Compatibility Validation Scenarios

## Scenario S1: Baseline Latest Server
- Purpose: Confirm existing known-good behavior remains intact.
- Inputs: baseline-latest target endpoint, at least one available NDI source.
- Expected: discovery succeeds, stream starts, compatibility status = compatible.

## Scenario S2: Failing Venue Server
- Purpose: Reproduce venue issue and classify outcome.
- Inputs: venue-failing endpoint/version.
- Expected: produce deterministic compatibility status (limited, incompatible, or blocked) with actionable diagnostic note.

## Scenario S3: Additional Older Obtainable Server(s)
- Purpose: Expand support evidence to every obtainable older target.
- Inputs: one target per obtainable older version.
- Expected: each target gets explicit status (compatible/limited/incompatible/blocked) with evidence.

## Scenario S4: Mixed-Server Configuration
- Purpose: Ensure partial success is not misreported as full success.
- Inputs: at least one compatible and one non-compatible target configured together.
- Expected: compatible sources remain usable; non-compatible targets reported explicitly; overall full-success not emitted.

## Scenario S5: Unknown-to-Final Transition
- Purpose: Confirm temporary unknown transitions to final matrix state.
- Inputs: target with initially unavailable version metadata, then completed evidence run.
- Expected: temporary unknown replaced by compatible/limited/incompatible/blocked.
