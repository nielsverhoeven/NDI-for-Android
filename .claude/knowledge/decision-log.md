# Decision log

## 2026-09-12 — #316 deep-link / permission / lifecycle tests (CI x86_64 subset)

- **Substituted anchor state**: no real NDI source exists on the CI emulator, so "active receive"
  for the rotation/background/kill/interruption tests is the deep-linked viewer's permanent
  "Connecting..." hold state. Confirmed by code reading: `NdiViewerBridge.StartReceiver` degrades
  to `Disconnected` when `NdiRuntime.EnsureInitialized()` is false on x86_64, and
  `ViewerViewModel.CheckForUnexpectedDrop()` — the only path that would move a `Disconnected`
  state into the retry machinery — has no production caller. This state never times out or
  changes on its own, which is what makes it usable as a fixed point for interruption tests.
- **Permission collection uses its own Appium session + xUnit collection**
  (`PermissionAppiumDriverFixture`, `[Collection("PermissionSession")]`), gated by an
  assembly-level `[CollectionBehavior(DisableTestParallelization = true)]`, because
  `autoGrantPermissions` is a session-level capability, not a per-test one, and two live Appium
  sessions must never run concurrently against one physical emulator.
- **`TryRestart()` cannot be reused for a restart expected to show the permission dialog**: it
  waits for our own package to be foreground, but the dialog is a different package
  (`com.android.permissioncontroller`) in front of it. `NdiApp.TryRestartExpectingPermissionPrompt`
  accepts either as evidence the relaunch worked.
- **Permission state is reset via `pm revoke` before every permission test**, not just once,
  because collection run order is not guaranteed (only non-concurrency is), so either ordering
  could otherwise leak a grant from the main (auto-granting) collection into this one.
- **`CrashBufferGuard` marks a baseline length and never clears the device crash buffer.** The
  buffer is device-global; clearing it before every test would blame a routine system-app crash on
  the interruption test that happened to run next, and would erase the whole-run diagnostic
  `run-emulator-tests.sh` dumps at the end of a failed run. Each test's gate only fails when the
  new tail of the buffer contains our own package name.
- **MediaProjection consent and true audio-focus behaviour stay device-only** — no native NDI
  runtime on x86_64 means there is nothing downstream of "tapped Allow" to assert, and real
  audio-focus ducking needs a live audio pipeline this emulator profile (`-noaudio`) doesn't run.
- **New test classes run in the existing per-PR `e2e-tests` gate**, not behind a manual-only
  workflow or label — conditional on raising the `dotnet test` timeout from 20 to 35 minutes so
  the added test time cannot exceed it and exit with no TRX.
- **`OutputViewModel` re-stream mode is explicitly restored after the stream deep-link test.**
  Since `OutputViewModel` became a Singleton, `IsReStreamMode` persists for the process lifetime;
  left set, it would follow every later test in the shared collection onto the Stream tab.
- **Toast assertions match on a substring of the resolver's real error message, not just the
  Toast class**, to avoid a false positive against a stale Toast left over from an earlier test.
  Whether UiAutomator2 captures a Toast node at all on this AVD image is unverified until the
  first CI run — the fallback if it isn't captured is a logged observation, keeping
  `Assert.True(app.Home.IsVisible)` as the actual gate.
