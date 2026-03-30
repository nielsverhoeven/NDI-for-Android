# AGENTS.md

## Start Here
- Canonical module graph is in `settings.gradle.kts`: `:app`, `:core:model`, `:core:database`, `:core:testing`, `:feature:ndi-browser:{domain,data,presentation}`, `:ndi:sdk-bridge`.
- Existing AI guidance lives in `.github/agents/copilot-instructions.md` (there is no root `.github/copilot-instructions.md` in this repo).
- Behavior authority is spec-driven: `specs/001-scan-ndi-sources/contracts/ndi-feature-contract.md`, `specs/001-scan-ndi-sources/tasks.md`, `specs/002-stream-ndi-source/contracts/ndi-output-feature-contract.md`, and `specs/002-stream-ndi-source/tasks.md`.

## Architecture Rules (Project-Specific)
- Keep app composition in `app`: `app/src/main/java/com/ndi/app/di/AppGraph.kt` wires repositories and feature dependency providers.
- Use service-locator objects already in feature presentation (`SourceListDependencies`, `ViewerDependencies`, `OutputDependencies`) from `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListTelemetry.kt`, `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerTelemetry.kt`, and `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/output/OutputTelemetry.kt`.
- Domain contracts stay in `feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/NdiRepositories.kt`; implement them only in `feature/ndi-browser/data`.
- Room persistence is centralized in `core/database/src/main/java/com/ndi/core/database/NdiDatabase.kt`; do not add direct DB access from presentation.
- Native NDI integration is isolated to `ndi/sdk-bridge` (`ndi/sdk-bridge/src/main/java/com/ndi/sdkbridge/NdiNativeBridge.kt`, `ndi/sdk-bridge/src/main/cpp/CMakeLists.txt`).

## Data Flow and Navigation
- Follow `Fragment -> ViewModel -> Repository`: see `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/source_list/SourceListScreen.kt` and `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerScreen.kt`.
- Collect flows with lifecycle awareness (`repeatOnLifecycle`) and clear bindings in `onDestroyView` (same files above).
- Preserve foreground-only discovery refresh (`onStart`/`onStop` -> `startForegroundAutoRefresh`/`stopForegroundAutoRefresh`) in `SourceListScreen.kt` and `SourceListViewModel.kt`.
- Viewer routing uses deep links (`ndi://viewer/{sourceId}`) defined in `app/src/main/res/navigation/main_nav_graph.xml` and built by `app/src/main/java/com/ndi/app/navigation/NdiNavigation.kt`.
- Output routing uses deep links (`ndi://output/{sourceId}`) defined in `app/src/main/res/navigation/main_nav_graph.xml` and built by `app/src/main/java/com/ndi/app/navigation/NdiNavigation.kt`.
- No-autoplay continuity is intentional: selection is highlighted on relaunch, not auto-opened (`SourcePreselectionController.kt`, `MainActivity.kt`, `tasks.md` US2).

## Build, Validation, and CI Workflow
- Run prerequisite gate first: `scripts/verify-android-prereqs.ps1` (docs: `docs/android-prerequisites.md`).
- Validate wrapper/toolchain before major changes: `./gradlew --version` (or `./gradlew.bat --version` on Windows) per `docs/ndi-feature.md`.
- Keep release hardening enabled in `app/build.gradle.kts` (`isMinifyEnabled=true`, `isShrinkResources=true`) and guard with `verifyReleaseHardening`.
- CI runs on Windows and executes prereq checks with `-CiMode -AllowMissingNdiSdk`: `.github/workflows/android-ci.yml`.
- Dual-emulator validation harness is in `testing/e2e` (`testing/e2e/README.md`, `testing/e2e/scripts/run-dual-emulator-e2e.ps1`) and is the default e2e path for `specs/002-stream-ndi-source/tasks.md`.

## Housekeeping
- Use the `cleaner` agent (`.github/agents/cleaner.agent.md`) for dead code removal, unused resource pruning, redundant test deduplication, and dev-environment housekeeping. It collaborates with `reviewer` (sign-off), `tester` (regression gate), and `speckit.constitution` (principle updates) automatically.

## Agent Collaboration for NDI
- Use the `ndi.expert` agent (`.github/agents/ndi.expert.agent.md`) for NDI SDK integration decisions grounded in `https://docs.ndi.video/`.
- For feature execution, pair `ndi.expert` (NDI protocol/SDK correctness) with `android.app-builder` (Android implementation) under `speckit.implement` orchestration.

## Conventions That Prevent Regressions
- Keep telemetry emission patterns in place (`SourceListTelemetry.kt`, `ViewerTelemetry.kt`, `ViewerRecoveryTelemetry.kt`, `OutputTelemetry.kt`).
- Keep retry semantics bounded to 15 seconds (`ViewerViewModel.kt`, `NdiViewerRepositoryImpl.kt`, `ViewerReconnectCoordinator.kt`, `OutputControlViewModel.kt`, `NdiOutputRepositoryImpl.kt`, `OutputRecoveryCoordinator.kt`).
- Avoid editing legacy template path `app/src/main/java/com/example/ndi_for_android/*` unless explicitly migrating; active app path is `com.ndi.app` in `app/src/main/AndroidManifest.xml`.
- Respect toolchain baseline in `gradle/libs.versions.toml` and `gradle/wrapper/gradle-wrapper.properties` (AGP 9.0.0, Gradle 9.2.1, Kotlin 2.2.10, SDK 34 with JDK 21 toolchain and Java 17 bytecode target) unless doing an intentional coordinated upgrade.

## Workflow Reliability Rules
- Use local workspace files as the source of truth when present; do not fetch or rely on remote repository contents for files that already exist in the workspace.
- Before any bulk GitHub issue or PR automation, validate that the MCP server is running and authenticated for interacting with Github.
- For task-to-issue flows, validate any existing `[#NNN]` reference before reusing it; if the reference is invalid, stale, inaccessible, or does not resolve to the intended issue, replace it.
- Create issues conservatively in small batches and checkpoint the local identifier-to-issue mapping in the workspace before continuing.
- After task-to-issue operations, verify that the local `tasks.md` file was actually updated and that every task/user-story line contains exactly one valid issue token.
- Avoid repeated no-op or placeholder subagent calls; delegate once to the best-fit agent when delegation is useful, then execute the work directly.
- If remote repository contents do not yet include a local spec or `tasks.md` path, treat the local workspace file as authoritative for planning and task conversion.
