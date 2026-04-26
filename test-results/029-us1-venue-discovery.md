# US1 Validation: Reliable Venue Discovery

Date: 2026-04-07
Feature: 029-ndi-server-compatibility

## Scope
- Validate mixed-server discovery behavior preserves usable sources.
- Validate non-compatible endpoints are not reported as fully successful.
- Validate source-list state receives compatibility outcomes for operator-facing diagnostics.

## Automated Validation Performed
- Data contract tests:
  - com.ndi.feature.ndibrowser.data.NdiDiscoveryRepositoryContractTest
- Classifier boundary tests:
  - com.ndi.feature.ndibrowser.data.DiscoveryCompatibilityClassifierTest
- Source-list propagation tests:
  - com.ndi.feature.ndibrowser.source_list.SourceListViewModelTest

## Command
```powershell
$env:JAVA_HOME='C:\Program Files\Android\Android Studio\jbr'
$env:ANDROID_HOME='C:\Users\Niels\AppData\Local\Android\Sdk'
$env:ANDROID_SDK_ROOT='C:\Users\Niels\AppData\Local\Android\Sdk'
.\gradlew.bat :feature:ndi-browser:data:testDebugUnitTest --tests "com.ndi.feature.ndibrowser.data.NdiDiscoveryRepositoryContractTest" --tests "com.ndi.feature.ndibrowser.data.DiscoveryCompatibilityClassifierTest" --console=plain
.\gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest --tests "com.ndi.feature.ndibrowser.source_list.SourceListViewModelTest" --console=plain
```

## Result
- PASS: targeted US1 unit tests completed successfully.
- Verified behavior:
  - Mixed endpoint reachability emits per-endpoint compatibility records.
  - Overall compatibility for mixed outcomes is limited (not fully compatible).
  - Source-list UI state receives compatibility snapshot and flags partial compatibility.
  - Partial compatibility telemetry event is emitted when mixed outcomes exist.

## Environment Notes
- Device/venue runtime validation remains environment-dependent.
- On-device venue endpoint verification should be executed when venue network/server access is available.
