# Test Results: Feature #113 (MAUI migration Stage 5) - 2026-06-05

## Stage Results
| Stage | Status | Command | Notes |
|---|---|---|---|
| Prerequisite gate | PASS | `dotnet --version` | SDK `10.0.300` |
| Prerequisite gate | PASS | `dotnet build -m:1 -v minimal` | Solution build succeeded |
| Stage 1 Build Validation (Debug) | PASS | `dotnet build --configuration Debug -m:1 -v minimal` | Build succeeded |
| Stage 1 Build Validation (Release) | BLOCKED (inconclusive run) | `dotnet build --configuration Release -m:1 -v minimal` | Build process was canceled by terminal session (`MSB5021`) before completion |
| Stage 2 Unit tests | PASS | `dotnet test tests/MauiApp.Tests/MauiApp.Tests.csproj --configuration Debug --logger "trx;LogFileName=unit-results.trx"` | 24 passed, 0 failed |
| Stage 3 Integration tests | SKIP (none discovered) | `dotnet test tests/MauiApp.Tests/MauiApp.Tests.csproj --configuration Debug --filter "Category=Integration" --logger "trx;LogFileName=integration-results.trx"` | 0 tests matched `Category=Integration` |
| Stage 4 UI tests | BLOCKED/SKIPPED | `dotnet test tests/MauiApp.UITests/MauiApp.UITests.csproj --configuration Debug --logger "trx;LogFileName=ui-results.trx"` | 8 skipped: `ANDROID_APK_PATH environment variable is not set — no emulator available.` |
| Stage 5 Device-facing evidence | PASS | `adb uninstall/install + monkey launch + pidof + crash log tail` | App installed and launched (`APP_PID` present), crash buffer tail empty |
| Stage 5 NDI E2E harness | BLOCKED | `pwsh -ExecutionPolicy Bypass -File .\testing\e2e\scripts\validate-command-contract.ps1`; `bash --version` | Script not present in repo path; shell harness requires bash, but bash/WSL unavailable in this environment |
| Stage 6 Release gate publish | FAIL | `dotnet publish src/MauiApp/NdiForAndroid.csproj --configuration Release --framework net10.0-android -m:1 -v minimal` | `XAGNM7009` internal Android native marshal generation error (`missing native code generation state for Arm64`) |

## Failures Found & Fixed
| Test | Failure | Root Cause | Fix | Verified |
|---|---|---|---|---|
| Release publish (`net10.0-android`) | `XAGNM7009` at `GenerateNativeMarshalMethodSources` | Likely Android SDK/.NET Android toolchain regression or target/ABI codegen state corruption during marshal generation | No test-side fix possible (production/build toolchain issue) | Reproduced once in clean publish command |
| UI test suite execution context | All 8 tests skipped | Required `ANDROID_APK_PATH` + emulator/Appium environment not configured for current run | No change made; needs environment provisioning | Skip reason reproduced in test output |

## Release Gate
| Check | Status |
|---|---|
| Debug build | PASS |
| Unit tests | PASS |
| Integration tests | SKIP (no tests discovered) |
| UI tests | BLOCKED/SKIPPED |
| NDI e2e | BLOCKED |
| Release build/publish | FAIL |
| Device install / launch smoke check | PASS |
