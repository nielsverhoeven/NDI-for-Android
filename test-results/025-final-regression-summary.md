
Running 20 tests using 6 workers

  Γ£ô   1 [android-primary] ΓÇ║ tests\024-core-navigation-smoke.spec.ts:3:5 ΓÇ║ US1 navigation smoke baseline (13ms)
  Γ£ô   2 [android-primary] ΓÇ║ tests\024-developer-mode-rebuild.spec.ts:4:5 ΓÇ║ US3 developer mode baseline contract (23ms)
  Γ£ô   3 [android-primary] ΓÇ║ tests\024-core-settings-smoke.spec.ts:3:5 ΓÇ║ US1 settings smoke baseline (13ms)
  Γ£ô   4 [android-primary] ΓÇ║ tests\024-navigation-menu-rebuild.spec.ts:4:5 ΓÇ║ US2 navigation menu baseline contract (27ms)
  Γ£ô   5 [android-primary] ΓÇ║ tests\024-settings-menu-rebuild.spec.ts:4:5 ΓÇ║ US2 settings menu baseline contract (26ms)
  Γ£ô   6 [android-primary] ΓÇ║ tests\025-appearance-settings-rebuild.spec.ts:11:5 ΓÇ║ US1 light mode save uses hybrid validation (28ms)
  Γ£ô   7 [android-primary] ΓÇ║ tests\025-appearance-settings-rebuild.spec.ts:25:5 ΓÇ║ US1 dark mode save uses hybrid validation (24ms)
  Γ£ô   8 [android-primary] ΓÇ║ tests\025-appearance-settings-rebuild.spec.ts:36:5 ΓÇ║ US1 system mode save path is callable (23ms)
  Γ£ô   9 [android-primary] ΓÇ║ tests\025-appearance-settings-rebuild.spec.ts:43:5 ΓÇ║ US2 theme editor entry scenario contract (17ms)
  Γ£ô  10 [android-primary] ΓÇ║ tests\025-appearance-settings-rebuild.spec.ts:48:5 ΓÇ║ US3 system default follow-system toggle scenario contract (12ms)
  Γ£ô  11 [android-secondary] ΓÇ║ tests\024-core-navigation-smoke.spec.ts:3:5 ΓÇ║ US1 navigation smoke baseline (17ms)
  Γ£ô  12 [android-secondary] ΓÇ║ tests\024-settings-menu-rebuild.spec.ts:4:5 ΓÇ║ US2 settings menu baseline contract (29ms)
  Γ£ô  13 [android-secondary] ΓÇ║ tests\024-core-settings-smoke.spec.ts:3:5 ΓÇ║ US1 settings smoke baseline (18ms)
  Γ£ô  14 [android-secondary] ΓÇ║ tests\024-navigation-menu-rebuild.spec.ts:4:5 ΓÇ║ US2 navigation menu baseline contract (24ms)
  Γ£ô  15 [android-secondary] ΓÇ║ tests\024-developer-mode-rebuild.spec.ts:4:5 ΓÇ║ US3 developer mode baseline contract (25ms)
  Γ£ô  16 [android-secondary] ΓÇ║ tests\025-appearance-settings-rebuild.spec.ts:11:5 ΓÇ║ US1 light mode save uses hybrid validation (26ms)
  Γ£ô  17 [android-secondary] ΓÇ║ tests\025-appearance-settings-rebuild.spec.ts:25:5 ΓÇ║ US1 dark mode save uses hybrid validation (17ms)
  Γ£ô  18 [android-secondary] ΓÇ║ tests\025-appearance-settings-rebuild.spec.ts:36:5 ΓÇ║ US1 system mode save path is callable (26ms)
  Γ£ô  19 [android-secondary] ΓÇ║ tests\025-appearance-settings-rebuild.spec.ts:43:5 ΓÇ║ US2 theme editor entry scenario contract (11ms)
  Γ£ô  20 [android-secondary] ΓÇ║ tests\025-appearance-settings-rebuild.spec.ts:48:5 ΓÇ║ US3 system default follow-system toggle scenario contract (12ms)

  20 passed (7.8s)

## Release Hardening
- Command: ./gradlew.bat :app:verifyReleaseHardening
- Result: PASS
- Evidence file: test-results/025-release-hardening.md
