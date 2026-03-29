# Contract: Viewer Persistence and Source List Availability UI

## Scope

Defines externally observable behavior between user interactions and UI outcomes for source list and viewer restore flows.

## Source List Row Contract

Input signals per source row:
- sourceId
- previouslyConnected (boolean)
- isAvailable (boolean)

Required UI outputs:
- Previously Connected indicator shown iff `previouslyConnected == true`
- Unavailable indicator shown iff `isAvailable == false`
- View Stream action enabled iff `isAvailable == true`
- View Stream action disabled and non-navigating iff `isAvailable == false`

## Availability Debounce Contract

Input sequence (poll snapshots):
- source present => missCounter = 0, available = true
- source absent for one consecutive poll => missCounter = 1, still available
- source absent for two consecutive polls => missCounter >= 2, unavailable

Required behavior:
- Unavailable state transition occurs only at second consecutive miss.
- Availability state returns to available on next observed presence.

## Previously Connected Contract

Input condition:
- user selected source
- at least one frame was successfully rendered

Required behavior:
- Set `previouslyConnected = true` for that source.
- Persist marker across app restarts.
- Clear only via app data reset/uninstall.

## Viewer Restore Contract

Persisted input:
- lastViewedSourceId
- savedLastFrameImagePath (optional)
- current availability for lastViewedSourceId

Required behavior on app relaunch:
- Restore last viewed source context.
- Display saved frame preview when path is readable.
- If source unavailable, show unavailable state and do not auto-start playback.

## Error/Fallback Contract

- If saved image is unreadable or missing, show placeholder and keep restored source context.
- If storage write for preview fails, do not crash; preserve stream metadata path.

## Test Assertions

The following assertions are mandatory in e2e and/or unit tests:
- Disabled View Stream never triggers viewer navigation.
- Previously Connected badge appears only after successful frame render.
- Unavailable transition requires two consecutive misses.
- Last viewed context restores within defined success criteria bound.
