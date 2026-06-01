# Research: Persistent Source Cache

**Branch**: `030-persist-source-cache` | **Date**: 2026-04-19

## Decision 1: Persist cached discovered sources in Room and store preview images as file references

- Decision: Add a dedicated cached-source Room model for persisted discovery metadata, availability/validation state, endpoint details, preview-file path reference, and timestamps; keep preview image bytes in app-internal storage and persist only the resolved file path in Room.
- Rationale: The repo already uses Room for offline-first state and already stores preview-image file paths in continuity models. Persisting file references avoids oversized Room rows and keeps preview retention compatible with existing file-writing paths.
- Alternatives considered: Store preview bitmaps directly in Room as blobs; rejected because it increases migration, I/O, and database-size risk. Reuse only `last_viewed_context`; rejected because it represents one restored viewer context, not a durable multi-source cache.

## Decision 2: Model validation state separately from current availability

- Decision: Represent cached-source runtime status with an explicit validation-state dimension that can distinguish `NOT_YET_VALIDATED`, `VALIDATING`, `AVAILABLE`, and `UNAVAILABLE` even when a cached row already exists.
- Rationale: The feature requires cached rows to appear immediately while validation is still running and requires the View action to stay disabled during that window. A separate validation-state model is clearer than overloading a boolean `isAvailable` flag.
- Alternatives considered: Use only the existing availability history map; rejected because it is session-oriented and does not encode startup validation intent clearly enough for persisted rows. Treat cached rows as available until proven otherwise; rejected because it violates the disabled-during-validation requirement.

## Decision 3: Deduplicate cached sources by stable SDK/source ID when present, otherwise by normalized source endpoint

- Decision: Use `stableSourceId ?: normalizedEndpoint` as the canonical cache key, with display name retained only as presentation metadata. Keep a join between cached sources and discovery-server records so multiple discovery servers can point to one canonical source row.
- Rationale: This exactly matches the clarified requirement and prevents duplicate rows when the same source is announced by more than one discovery server or when display names collide.
- Alternatives considered: Deduplicate by display name; rejected because the spec forbids it. Deduplicate only by endpoint; rejected because a stable SDK/source ID should take precedence when present.

## Decision 4: Keep discovery-server behavior metadata-only and resolve stream startup from persisted source endpoints

- Decision: When one or more discovery servers are enabled, discovery continues to query only those servers for source metadata, but viewer/output startup resolves the actual source IP and port from the persisted cached-source endpoint rather than any discovery-server host.
- Rationale: This corrects the current conceptual bug identified in the spec: discovery servers are registries, not stream hosts. Persisting the announced source endpoint provides a stable handoff between discovery and playback/output flows.
- Alternatives considered: Re-query the discovery server during stream start; rejected because it repeats the current error path and adds extra runtime coupling. Launch directly against the configured server endpoint; rejected because it is semantically wrong for NDI stream startup.

## Decision 5: Surface developer inspection through the existing Settings developer diagnostics area

- Decision: Extend the existing developer diagnostics/settings flows with a read-only database inspection view or section that exposes cached-source rows, preview references, and discovery-server associations only when developer mode is enabled.
- Rationale: `SettingsViewModel`, `DiscoveryServerSettingsViewModel`, and `DeveloperDiagnosticsRepositoryImpl` already provide the appropriate developer-only surface. Reusing them preserves module boundaries and avoids introducing a separate debug-only activity.
- Alternatives considered: Add a standalone debug screen outside Settings; rejected because the spec explicitly requests the developer section of Settings. Allow in-app editing or deletion; rejected because the requirement is inspection-only.

## Decision 6: Merge persisted cache and live discovery in repository flows, not in fragments

- Decision: The discovery/data layer will load cached rows first, emit them immediately, kick off validation, then merge live discovery results back into the same canonical source set before presentation observes the updated state.
- Rationale: Repository-level merging preserves the constitution rule that UI stays thin, and it allows Home, Source List, and developer diagnostics to reuse one consistent cached-source model.
- Alternatives considered: Merge cache and live results in individual ViewModels; rejected because it duplicates policy across screens. Load cache only in Settings; rejected because Home/View need the same canonical state.

## Implementation Notes

- Room schema change is expected and should be handled as a normal migration from the current database version.
- Existing `LastViewedContextEntity`, `ConnectionHistoryStateEntity`, and `UserSelectionEntity` remain relevant and should be linked to, not replaced by, the new cached-source model.
- Preview retention should remain bounded and resilient: missing files must degrade gracefully to a placeholder state without deleting the cached row.