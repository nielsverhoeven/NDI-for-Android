# Feature: NDI Integration Rework — mDNS Fallback, Discovery Server Sourcing, and View/Stream Screen Separation

<!-- Issue: #213 -->
<!-- Created: 2026-06-07 -->

## Overview

The app's NDI discovery mechanism currently requires a manually configured Discovery Server endpoint and returns no sources when none is set. This does not match the NDI protocol convention, which specifies mDNS (Bonjour / multicast DNS) as the default peer-to-peer discovery channel and Discovery Servers as an opt-in enterprise mechanism. In addition, the Stream and View screens conflate two distinct user intents — viewing incoming NDI streams and originating outgoing NDI output — making both screens confusing and harder to test independently. This feature corrects both problems: it introduces automatic mDNS fallback discovery, makes Discovery Server mode mutually exclusive with mDNS (avoiding double-subscription), persists discovered sources to SQLite, and cleanly separates the View screen (incoming stream discovery and playback) from the Stream screen (outgoing device output only).

---

## User Stories

- As a user on a flat LAN with no server configuration, I want the app to automatically discover NDI sources via mDNS so that I can start viewing without manual setup.
- As a user who has configured a Discovery Server, I want the app to switch to server-based discovery automatically and stop multicast traffic so that my network policies are respected.
- As a user, I want enabling or disabling a Discovery Server in Settings to take effect immediately, without restarting the app, so that I can react to network changes on the fly.
- As a user browsing the View screen, I want to see the list of discoverable NDI sources and tap one to view it, so that the View screen is my single entry point for incoming streams.
- As a user on the Stream screen, I want to see only the controls for sending NDI output from my device, so that the screen's purpose is unambiguous.
- As a developer, I want all discovered source records stored in the local database so that I can inspect the last-known source registry even when the network is unavailable.

---

## Functional Requirements

1. **mDNS default discovery**: When the `DiscoveryServers` list in settings is empty, or when all configured servers have `Enabled = false`, the app shall discover NDI sources using mDNS (Bonjour/multicast DNS) on the local network.
2. **Continuous mDNS refresh**: mDNS discovery shall run as a continuous or periodically-polled background operation while the View screen is active; discovered sources shall appear without requiring a manual refresh gesture.
3. **MulticastLock acquisition**: Before receiving mDNS multicast packets on Android, the app shall acquire `WifiManager.MulticastLock`; the lock shall be released when mDNS discovery is stopped or the app is backgrounded.
4. **Discovery Server mode activation**: When one or more `DiscoveryServerPreference` entries are present with `Enabled = true`, the app shall switch to Discovery Server mode: mDNS shall be stopped and the app shall query each enabled Discovery Server for its registered source list.
5. **Mutual exclusivity**: mDNS discovery and Discovery Server polling shall never run simultaneously. The orchestration layer enforces exactly one active discovery mode at all times.
6. **Multi-server support**: All enabled Discovery Servers shall be queried, respecting `Order` for deduplication priority when the same source name appears across servers.
7. **Source persistence**: Every source record discovered by either mechanism (name, host, port) shall be upserted to the SQLite `sources` table with a `DiscoveryMode` tag indicating how it was found.
8. **Stale source expiry**: Sources discovered via Discovery Server that no longer appear in a poll result shall have their `IsAvailable` flag set to `false` in SQLite (soft-delete); they shall not appear in the active source list but shall be retained for history.
9. **Hot mode-switching**: Changing Discovery Server configuration in Settings and applying it shall trigger an immediate, in-process switch between mDNS and Discovery Server mode without requiring an app restart.
10. **Legacy field deprecation**: The legacy `DiscoveryHost`/`DiscoveryPort` single-server fields on `NdiSettingsSnapshot` shall be superseded by the `DiscoveryServers` list. The orchestration layer shall ignore the legacy fields; they remain in the DB schema for backward compatibility only.
11. **View screen — source discovery and playback**: The View screen shall display the list of currently discovered NDI sources (populated by either discovery mode), indicate the active discovery mode (mDNS or Discovery Server), and allow the user to tap a source to navigate to the viewer player.
12. **Stream screen — outgoing output only**: The Stream screen shall contain only controls for originating an NDI output stream from the device (screen share, camera selection, stream name input, start/stop). It shall not display any NDI source list or discovery results.
13. **Discovery mode indicator**: The View screen shall display a non-blocking indicator showing which discovery mode is active (e.g., "mDNS" or "Discovery Server: host:port").
14. **No regression on Settings**: Existing Discovery Server add/edit/delete/enable/disable flows and their persistence via `ISettingsRepository` must not regress.
15. **Settings Apply semantics preserved**: The existing `IDiscoverySettingsOrchestrator.ApplyAsync` call path shall remain the trigger for committing settings changes to the bridge layer.

---

## Non-Functional Requirements

- **Performance**: mDNS source enumeration shall produce initial results within 3 seconds on a cooperative LAN under normal conditions.
- **Reliability**: A Discovery Server that is unreachable shall not prevent the app from falling back gracefully and showing cached sources; no unhandled exceptions shall surface to the user.
- **Thread safety**: All NDI native callbacks and mode-switch transitions shall be marshaled to the UI thread via `MainThread.BeginInvokeOnMainThread` per the constitution's NDI bridge threading rule.
- **Battery / network efficiency**: Discovery Server polling interval shall be configurable and default to no less than 10 seconds to avoid excessive battery drain.
- **Accessibility**: The discovery mode indicator on the View screen shall be readable by Android accessibility services (content description set).
- **Android API compatibility**: MulticastLock usage and mDNS socket operations shall function on Android 8.0 (API 26) through Android 15 (API 35).

---

## Success Criteria

1. With zero Discovery Servers configured: the View screen populates NDI sources via mDNS within 3 seconds of screen activation; no Discovery Server connections are attempted.
2. With one or more enabled Discovery Servers: the View screen populates sources from those servers; no mDNS multicast traffic is produced (verifiable via `tcpdump` or equivalent on the test network).
3. Toggling a Discovery Server from enabled to disabled (and applying) switches discovery mode within 5 seconds without an app restart.
4. Every source that appears on the View screen has a corresponding row in the SQLite `sources` table with the correct `DiscoveryMode` value.
5. The Stream screen contains no NDI source list; it contains a stream-name input, output controls, and no references to `ISourceRepository` or discovery logic.
6. `dotnet test` passes (all non-NDI unit tests) with no regressions in Settings, Sources, Viewer, or Output test suites.
7. The existing Settings persistence and Discovery Server CRUD flows pass all existing automated tests.

---

## Out of Scope

- Custom mDNS service-type configuration (app always uses the standard `_ndi._tcp.local.` service type).
- Push-notification event streaming from the NDI Discovery Server (polling only in this iteration).
- MDNS over Wi-Fi Direct or hotspot interfaces.
- UI redesign of the Settings screen beyond adding/changing a discovery mode indicator.
- Viewer player changes (frame rendering, codec selection).
- NDI output protocol changes (screen-share/camera pipeline).

---

## Assumptions

- `CHANGE_WIFI_MULTICAST_STATE` is already declared in `AndroidManifest.xml` and no additional manifest change is required for MulticastLock.
- The NDI SDK (`libndi.so`) exposes a mDNS-based `NDIlib_find_create_v3` / `NDIlib_find_get_current_sources` API path that the mDNS bridge can call via P/Invoke; if not, an OS-level `NsdManager` (Android Java API) bridge will be used instead.
- Discovery Server protocol provides a source list with `{name, ip, port}` tuples accessible via the existing TCP reachability pattern already used in `NdiDiscoveryBridge`.
- The `IDiscoverySettingsOrchestrator` interface is the correct extension point for triggering mode switches; no new app-lifecycle hooks are needed.
- MulticastLock lifecycle is tied to the View screen becoming active/inactive (not the entire app lifecycle).

---

## Open Questions

None. All decisions are derivable from the issue body, the codebase, and the constitution.
