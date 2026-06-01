# Battery and Lifecycle Continuity Report

**Feature**: Background Stream Persistence (005)  
**Date**: 2026-03-20  
**Scope**: Lifecycle-bound execution impact for continuous NDI stream output.

---

## Justification of Continuity Approach

### No New Background Workers or Jobs

This feature does **not** introduce:
- A new `WorkManager` job or `JobScheduler` entry.
- A background `Service` running independently of the foreground lifecycle.
- A fire-and-forget coroutine scope that outlives the component lifecycle.

The NDI output stream is maintained by the native NDI SDK bridge (`ndi/sdk-bridge`) which runs on the existing `Service` connection already present in the app. The only change is that **explicit navigation away from the output screen does not call `stopOutput()`**, leaving the pre-existing active stream session untouched.

### Lifecycle-Bound Continuation

The stream session is bound to:
1. An explicit user action to start output (already required before this feature).
2. An explicit user action to stop output, or a terminal interruption event from the NDI SDK.

Background transitions do **not** extend any coroutine lifecycle scope beyond what was already initialized by the explicit start. The coroutine scope in `OutputControlViewModel` uses the ViewModel scope, which itself is cleared when the Activity is destroyed (not when the Fragment is non-visible).

### Energy Impact Analysis

| Scenario | Before Feature | After Feature | Delta |
|----------|---------------|---------------|-------|
| App in foreground, stream active | NDI encode + network | NDI encode + network | No change |
| App in background (Home), stream active | Stream stopped (no encode) | NDI encode + network | Higher while backgrounded |
| App killed / Activity destroyed | Stream stopped | Stream stopped | No change |

The energy cost increase is **intentional and user-explicit**: the user starts output, then deliberately switches apps while keeping the stream alive. This is the core value of the feature — continuity for active sessions.

### Measurable Bounds

- Continuing stream in background costs the same power as a foreground stream.
- There is no unbounded polling loop or additional sensor/network operation beyond the NDI session itself.
- Stream is always stopped when the Activity is fully destroyed or when the user explicitly taps Stop Output.

### Lifecycle Safety

- `OutputControlViewModel` lifecycle: scoped to the ViewModel host (Activity). No leaks.
- `StreamContinuityRepositoryImpl`: uses `MutableStateFlow`; no threads or coroutines created beyond what ViewModel scope manages.
- No wakelock, no foreground service promotion, no silent background keep-alive.

---

## Conclusion

The feature is battery-conscious by design. Continuity is strictly lifecycle-bound:  
the stream persists only while the app's ViewModel remains in scope (Activity not destroyed)  
and only when the user has explicitly started it.  
No autonomous energy consumption is introduced beyond the explicit user-controlled output session.
