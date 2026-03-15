# Phase 0 Research: NDI Source Discovery and Viewing

## Decision 1: Android toolchain baseline
- Decision: Standardize on Android Studio + JDK 17 + Android SDK cmdline tools for local and CI environments.
- Rationale: The current environment check shows no Android CLI tools available in PATH and missing JAVA_HOME/ANDROID_SDK_ROOT, so setup must be explicit and reproducible.
- Alternatives considered: Rely on preinstalled system tools (rejected because tool audit showed missing commands and env vars).

## Decision 2: NDI SDK integration strategy
- Decision: Integrate NDI 6 Android SDK through a dedicated sdk-bridge module with JNI/native library packaging per ABI.
- Rationale: Isolates native dependencies and keeps feature modules aligned with MVVM/repository boundaries.
- Alternatives considered: Direct native integration in feature module (rejected due to coupling and testability impact), dynamic loading from external storage (rejected for reliability/security concerns).

## Decision 3: Discovery lifecycle and battery behavior
- Decision: Run discovery auto-refresh every 5 seconds only while source list screen is foreground; provide manual refresh.
- Rationale: Matches clarified requirement and battery-conscious constitution constraints.
- Alternatives considered: Continuous background discovery (rejected for battery policy violation), manual refresh only (rejected for weaker UX).

## Decision 4: Playback interruption handling
- Decision: Auto-retry source reconnect for up to 15 seconds, then show retry/reselect actions.
- Rationale: Balances resilience and battery/network cost while avoiding indefinite loops.
- Alternatives considered: Infinite retries (rejected due to battery/network risk), no auto-retry (rejected due to poorer reliability).

## Decision 5: Source identity model
- Decision: Use stable endpoint identity as canonical source key; treat display name as user-facing label.
- Rationale: Prevents collisions when duplicate names exist and supports accurate persistence.
- Alternatives considered: Name-only identity (rejected due to ambiguity), list index identity (rejected due to instability between scans).

## Decision 6: Offline-first persistence mechanism
- Decision: Persist last selected source identity and recent discovery/session metadata in Room.
- Rationale: Satisfies constitution requirement for Room-based offline-first capability and supports app restart continuity.
- Alternatives considered: SharedPreferences/DataStore-only approach (rejected because constitution mandates Room for offline-first data needs).

## Decision 7: Testing approach under strict TDD
- Decision: Implement Red-Green-Refactor with JUnit unit tests for ViewModel/repository behavior and Espresso tests for list-to-viewer user flows.
- Rationale: Required by constitution and best aligns with feature risk areas (state transitions, interruption flows).
- Alternatives considered: Unit-only testing (rejected because UI navigation and interaction outcomes are core requirements).

## Decision 8: Permissions policy
- Decision: Do not request location permission for this feature.
- Rationale: Explicit user clarification and least-permission constitution policy.
- Alternatives considered: Conditional location prompt on failures (rejected as unnecessary and policy-hostile).

## Decision 9: Project modularization for this feature
- Decision: Use modules app, core, feature/ndi-browser, and ndi/sdk-bridge.
- Rationale: Enforces feature boundaries and isolates native SDK concerns while preserving single-activity navigation ownership in app module.
- Alternatives considered: Single app module architecture (rejected due to weaker separation and slower scaling for future features).

## Decision 10: Prerequisite verification gate
- Decision: Add explicit prerequisite verification steps before coding and in CI preflight.
- Rationale: The current machine check demonstrates unresolved prerequisites that would otherwise block implementation later.
- Alternatives considered: Deferring checks until build failures occur (rejected because it increases cycle time and creates avoidable instability).
