<!-- Last updated: 2026-06-09 -->

# Release Notes — Automatic Viewer Reconnection (Issue #233, PR #260)

## Summary

The viewer now recovers automatically when an active NDI stream drops unexpectedly,
mirroring the legacy Kotlin app's 15-second retry behaviour. You no longer have to
navigate back and re-select a source after a brief network interruption.

---

## What Changed

### Automatic reconnection on an unexpected drop

When a stream you are watching drops unexpectedly, the viewer now tries to reconnect
on its own for up to **15 seconds**. During this window it retries the connection
roughly **every 2 seconds**. The first attempt that reconnects successfully resumes
normal playback immediately.

### Countdown indicator

While reconnection is in progress, the viewer shows a counting-down status message:

> Reconnecting... {n}s remaining

The countdown starts at 15 and decreases each second. It resets only when a fresh
unexpected drop starts a new reconnection window — not on every retry attempt.

### Cancel an in-progress reconnection

A **Cancel** button is shown while reconnection is running. Tapping it stops the
retries immediately and returns the viewer to a stopped state.

### Reconnection failure and manual recovery

If the 15-second window expires without a successful reconnection, the viewer stops
and shows:

> Connection lost. Reconnection failed.

A **Reconnect** button is then shown. Tapping it restarts the viewer using the same
source you were last watching — no need to return to the source list.

### User-initiated Stop is unchanged

Stopping the viewer yourself never triggers reconnection. Auto-retry applies only to
unexpected drops, so an intentional Stop always ends playback cleanly.

---

## Notes for testers / developers

- The NDI bridge's connection-state signal (`INdiViewerBridge.GetConnectionState()`)
  is currently a **stub**: it reports `Connected` while a source is active and
  `Disconnected` otherwise. The real frame-arrival watchdog becomes an internal bridge
  concern once the libndi receive loop is wired (out of scope for this change).
- All retry/countdown timing is driven by an injected `TimeProvider`, so the behaviour
  is fully unit-testable without NDI hardware by advancing a fake clock.

---

## Technical details

- Feature spec: [`spec.md`](./spec.md) and [`plan.md`](./plan.md)
- Bridge contract, `IMainThreadDispatcher`, `TimeProvider` DI, and `ViewerViewModel`
  members are summarised in [`.github/KNOWLEDGE-BASE.md`](../../../.github/KNOWLEDGE-BASE.md).
- See [`docs/architecture.md`](../../architecture.md) for the module/threading model.
