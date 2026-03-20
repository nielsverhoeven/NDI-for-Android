# Phase 0 Research: Settings Menu and Developer Diagnostics

## Decision 1: Persist settings in existing repository + Room-backed path

- Decision: Store discovery server configuration and developer-mode toggle through existing repository-mediated persistence, implemented in the data layer with `core/database` ownership.
- Rationale: This matches constitution rules for repository-mediated data access, offline-first behavior, and centralized Room persistence.
- Alternatives considered: SharedPreferences/DataStore directly from UI (rejected: presentation-layer persistence access and architectural inconsistency), in-memory only settings (rejected: fails persistence requirements).

## Decision 2: Discovery server value format is hostname, IPv4, or bracketed IPv6 with optional port

- Decision: Accept `hostname`, `IPv4`, or bracketed `IPv6` with optional `:port` in range `1-65535`; trim surrounding whitespace before validation; use default NDI discovery port when port is omitted.
- Rationale: Captures both simple and advanced user needs while keeping input UX concise.
- Alternatives considered: Host-only format (rejected: insufficient for custom-port environments), mandatory port (rejected: unnecessary friction for common setup), split host/port fields (rejected: heavier UX with limited additional value), unbracketed IPv6 with port (rejected: ambiguous delimiter parsing).

## Decision 3: Apply discovery server changes immediately

- Decision: On save, apply the new discovery endpoint immediately, even if active stream sessions must be interrupted.
- Rationale: Aligns with clarified behavior and keeps runtime behavior deterministic.
- Alternatives considered: Apply on next refresh (rejected by clarification), require app restart (rejected as poor UX), user prompt immediate/deferred (rejected to avoid branching complexity in this feature).

## Decision 4: Unreachable discovery server fallback behavior

- Decision: If configured discovery server is unreachable, automatically fall back to default multicast/local discovery, show a visible warning, and retain configured value.
- Rationale: Preserves service continuity while signaling degraded path.
- Alternatives considered: Hard fail without fallback (rejected: availability risk), silent fallback (rejected: poor observability), disable discovery until manual action (rejected: high user friction).

## Decision 5: Settings screen integration via existing single-activity nav graph

- Decision: Add settings destination to current nav graph and entry point from existing main flow UI.
- Rationale: Maintains single-activity navigation principle and route consistency.
- Alternatives considered: Separate activity for settings (rejected: architecture drift), modal-only settings without route (rejected: poor deep-link/back-stack consistency).

## Decision 6: Developer overlay remains visible in idle mode

- Decision: With Developer Mode enabled and no active stream, display an explicit idle state (for example, "No active stream") while continuing to show recent logs.
- Rationale: Prevents false perception that diagnostics are broken and supports troubleshooting before stream start.
- Alternatives considered: Hide overlay while idle (rejected: ambiguous state), blank overlay fields (rejected: low clarity), no logs until stream starts (rejected: reduced diagnostic value).

## Decision 7: Redact sensitive values before overlay display

- Decision: Redact secrets/tokens/credentials from overlay log text before rendering.
- Rationale: Maintains developer observability without exposing sensitive runtime values in UI.
- Alternatives considered: Raw logs (rejected: security/privacy risk), event-only summaries (rejected: may remove too much debugging context), debug-only raw logs (rejected: branch-specific behavior complexity).

## Decision 8: Overlay computation remains lifecycle-bound and lightweight

- Decision: Compute overlay state in ViewModel scope and collect UI state with lifecycle awareness on each screen.
- Rationale: Aligns with existing `repeatOnLifecycle` pattern and avoids unnecessary background work.
- Alternatives considered: Background service for overlay refresh (rejected: battery/complexity), screen-local duplicate logic (rejected: inconsistency risk).

## Decision 9: Logging model is recent-entry buffer

- Decision: Surface a bounded recent-entry list (minimum 5 entries) as the canonical overlay log payload.
- Rationale: Satisfies feature requirement while keeping render cost predictable.
- Alternatives considered: Full unbounded log stream (rejected: memory/render overhead), single latest event only (rejected: insufficient troubleshooting context).

## Decision 10: Test strategy enforces red-green-refactor across unit and e2e

- Decision: Add failing tests first for settings persistence/validation, immediate-apply interruption behavior, fallback warning behavior, overlay idle visibility, and redaction.
- Rationale: Required by constitution TDD gate and prevents regression in navigation + streaming flows.
- Alternatives considered: implementation-first then tests (rejected by constitution), unit-only coverage (rejected: misses integrated behavior across screens).

## Clarification Resolution Status

All clarified decisions from `spec.md` are reflected in this research output.
No `NEEDS CLARIFICATION` items remain.
