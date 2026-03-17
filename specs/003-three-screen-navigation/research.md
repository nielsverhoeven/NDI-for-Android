# Phase 0 Research: Three-Screen NDI Navigation

## Decision 1: Use an adaptive top-level navigation shell (bottom bar on phones, rail on tablets)

- Decision: Implement one top-level navigation shell that renders Material 3
  bottom navigation for compact layouts and Material 3 navigation rail for
  expanded/tablet layouts.
- Rationale: This directly satisfies FR-002a and keeps a single destination
  model across form factors while matching Material 3 guidance.
- Alternatives considered: Bottom navigation on all layouts (rejected for poor
  tablet ergonomics), drawer-only navigation (rejected because requirement
  explicitly calls for bottom nav/rail behavior).

## Decision 2: Add a dedicated Home destination as the top-level launcher entry

- Decision: Introduce a Home top-level destination in `app` navigation
  composition and make it the default for launcher-icon app launch.
- Rationale: FR-004 and User Story 2 require Home as the orientation/dashboard
  entry point with explicit actions to Stream and View.
- Alternatives considered: Keep Source List as start destination (rejected
  because it violates explicit Home-default requirement), route launcher to last
  destination always (rejected because launcher and Recents behavior differ).

## Decision 3: Keep Stream and View as top-level destinations while preserving their existing feature flows

- Decision: Treat Stream and View as top-level navigation targets that host the
  existing output and source-list/viewer flows instead of redefining repository
  behavior.
- Rationale: This preserves specs 001 and 002 behavior authority and minimizes
  regression risk by reusing existing feature contracts.
- Alternatives considered: Merge Stream and View into one destination (rejected
  because it breaks feature focus and user stories), rewrite repositories
  specifically for top-level navigation (rejected as unnecessary churn).

## Decision 4: Enforce deterministic top-level navigation without duplicate stacking

- Decision: Use top-level navigation actions/options that restore existing
  destination state and prevent duplicate stack instances during repeated taps.
- Rationale: FR-008 requires deterministic outcomes and no duplicate top-level
  destination stacking.
- Alternatives considered: Plain navigate calls without stack control (rejected
  due to duplicate destination risk), manual fragment transactions (rejected by
  single-activity Navigation Component constitution gate).

## Decision 5: Preserve deep links and integrate them into top-level shell state

- Decision: Keep existing deep links (`ndi://viewer/{sourceId}` and
  `ndi://output/{sourceId}`) and ensure top-level navigation still exposes Home,
  Stream, and View when entered via deep link.
- Rationale: FR-009 requires backwards-compatible deep-link entry while keeping
  all three top-level destinations mutually reachable.
- Alternatives considered: Remove deep links in favor of top-level-only routes
  (rejected because it breaks existing contracts), add separate deep-link-only
  activity (rejected by single-activity architecture).

## Decision 6: Process-death restore policy distinguishes launcher launch from Recents restore

- Decision: On launcher launch, force Home as initial top-level destination; on
  Recents/task restore, reopen the last active top-level destination.
- Rationale: Clarified requirement FR-004 and FR-004a requires split behavior by
  launch context.
- Alternatives considered: Always open Home (rejected because it violates
  Recents restore requirement), always reopen last destination (rejected because
  it violates launcher-default Home requirement).

## Decision 7: Preserve continuity semantics exactly as specified for Stream and View

- Decision: Leaving Stream keeps output active until explicit stop; leaving View
  stops playback immediately and preserves selected source without autoplay on return
  or relaunch.
- Rationale: FR-010a through FR-010d and existing specs define these as explicit
  continuity guarantees.
- Alternatives considered: Stop Stream on every navigation away (rejected as
  behavior regression), autoplay View on return when source is selected
  (rejected by no-autoplay continuity contract).

## Decision 8: Add top-level navigation telemetry with non-sensitive payloads

- Decision: Emit non-sensitive telemetry for destination changes and failed
  navigation attempts in addition to existing feature telemetry streams.
- Rationale: FR-011 requires top-level navigation telemetry while CR-004
  constrains data sensitivity.
- Alternatives considered: No navigation telemetry (rejected because FR-011),
  include full source/session payloads (rejected as unnecessary and potentially
  sensitive).

## Decision 9: Test strategy remains strict TDD with layered validation

- Decision: Start with failing unit tests for navigation state selection,
  launch-context routing, and continuity policies, then add failing UI flow tests
  for Home/Stream/View transitions on phone and tablet layouts.
- Rationale: Constitution principle IV and CR-002 require test-first development
  and automated coverage for top-level transitions.
- Alternatives considered: Manual QA-only validation (rejected for regression
  risk), E2E-only testing without unit coverage (rejected for slower feedback
  and poor failure localization).

## Decision 10: Keep persistence local and minimal for continuity metadata

- Decision: Store only non-sensitive continuity fields needed for top-level
  restore and dashboard context through repository-mediated local persistence.
- Rationale: CR-004 requires local-only non-sensitive storage and avoids any new
  permission/data-collection surface.
- Alternatives considered: In-memory-only continuity (rejected because process
  death behavior requires durable state), cloud-backed continuity sync (rejected
  as out of scope and unnecessary).

## Clarification Resolution Status

All Technical Context items are resolved; there are no remaining
`NEEDS CLARIFICATION` placeholders for this feature plan.

