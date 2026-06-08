<!-- Last updated: 2026-06-08 -->

# Release Notes — NDI Integration Rework (Issue #213, PR #229)

## Summary

This release reworks how the app discovers NDI sources and clarifies the purpose of the View and Stream screens.

---

## What Changed

### Automatic mDNS discovery (zero-config)

The app now discovers NDI sources on the local network automatically via mDNS (Bonjour / multicast DNS) without any manual configuration. When no Discovery Server is configured in Settings, the View screen populates from mDNS.

On Android, the required `WifiManager.MulticastLock` is acquired automatically before each discovery poll and released when the mode switches or discovery stops. The `CHANGE_WIFI_MULTICAST_STATE` permission (already declared in the manifest) covers this; no new runtime permissions are required.

### Discovery Server mode (enterprise networks)

When one or more Discovery Servers are enabled in Settings → Discovery, the app switches to Discovery Server mode. In this mode:

- Each enabled server is checked for TCP reachability before querying.
- Results from all reachable servers are merged and deduplicated.
- mDNS multicast traffic stops entirely while this mode is active, respecting network policies that restrict multicast.
- Switching back to mDNS (by disabling all servers in Settings) takes effect immediately on next apply — no app restart required.

### View screen — source discovery and selection

The View tab now hosts the source list. It shows all currently discovered NDI sources (from whichever discovery mode is active), displays the active discovery mode label ("mDNS" or "Discovery Server"), and lets you tap any source to open the viewer.

### Stream screen — outgoing output only

The Stream screen now focuses exclusively on originating an NDI output stream from your device. It contains a stream name field and start/stop controls. It no longer shows any NDI source list or discovery results.

### Source persistence

Every source discovered by either mechanism is persisted to the local SQLite database (`sources` table). Sources that were found via a Discovery Server but no longer appear in a subsequent poll are marked unavailable (soft-deleted) rather than deleted, so historical records are preserved.

---

## Migration Notes

### Database schema (automatic — no action required)

A `DiscoveryMode` column (`TEXT NOT NULL DEFAULT 'Mdns'`) has been added to the `sources` table. The app applies this migration automatically via `ALTER TABLE` on first launch after update. Existing rows are assigned `'Mdns'` as the default. No data is lost and no manual action is required.

### Settings (no action required)

Existing Discovery Server configuration in Settings is preserved. The new orchestration layer reads the same `DiscoveryServers` list and applies mode switching on startup. Legacy `DiscoveryHost`/`DiscoveryPort` fields remain in the database schema for backward compatibility but are no longer used by the discovery logic.

---

## Technical details

See [`docs/architecture.md`](../../architecture.md) for the updated bridge layer, navigation routes, and data layer schema notes.
