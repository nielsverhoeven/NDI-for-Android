# Research: Theme Editor Settings

## Decision 1: Theme mode representation and app-wide application

- Decision: Store theme mode as an enum-like persisted value (`LIGHT`, `DARK`, `SYSTEM`) in settings and apply at app host level so all screens reflect the mode consistently.
- Rationale: Existing architecture centralizes persisted settings and uses a single activity host; applying mode at host level avoids per-screen divergence.
- Alternatives considered:
  - Per-fragment mode toggles: rejected because it creates inconsistent visual state and extra wiring.
  - Device-only/system-only behavior: rejected because explicit Light/Dark override is a user requirement.

## Decision 2: Accent palette model

- Decision: Provide a fixed curated accessible palette of 6-8 accent choices represented by stable IDs (not free-form color input).
- Rationale: Matches clarified requirement, keeps UX predictable, and simplifies persistence + testing.
- Alternatives considered:
  - Arbitrary color picker: rejected due to complexity and accessibility consistency risk.
  - Single accent only: rejected because it does not satisfy requested customization value.

## Decision 3: Persistence strategy

- Decision: Extend existing `NdiSettingsSnapshot` and settings storage model to persist `themeMode` and `accentColorId` alongside current settings fields.
- Rationale: Existing repository/data path already handles settings reads/writes and avoids creating duplicate preference stores.
- Alternatives considered:
  - Separate SharedPreferences store for theme only: rejected because it would split settings source-of-truth.
  - Non-persistent in-memory settings: rejected because relaunch persistence is required.

## Decision 4: Existing system mode compatibility

- Decision: Keep current system-follow behavior as the default compatibility path and ensure selecting `SYSTEM` maps to current behavior.
- Rationale: Requirement explicitly states current system setting behavior already works and must be preserved.
- Alternatives considered:
  - Re-implement full theme resolution stack: rejected because it risks regressions without added value.

## Decision 5: Test and validation strategy

- Decision: Use failing-first JUnit tests for viewmodel/repository persistence logic, instrumentation checks for settings UI state, and Playwright emulator e2e for visible theme/accent flows; run full existing Playwright regression.
- Rationale: Satisfies constitution TDD and visual-change obligations.
- Alternatives considered:
  - Unit tests only: rejected because visible theme transitions and UI selection states require e2e verification.
  - Playwright only: rejected because model persistence and state transformations need fast unit coverage.

## Decision 6: Feature-module boundary strategy

- Decision: Implement theme editor as a dedicated feature module family (`feature/theme-editor/domain`, `feature/theme-editor/data`, `feature/theme-editor/presentation`) and integrate from existing settings navigation.
- Rationale: Satisfies Constitution Principle IX by introducing explicit module boundaries for new feature work while preserving existing app composition and repository wiring patterns.
- Alternatives considered:
  - Keep all implementation only inside existing `feature/ndi-browser` modules: rejected because it does not provide explicit new feature-module boundaries.
  - Implement in `app` module directly: rejected because it violates feature-first modularization and weakens testable boundaries.
