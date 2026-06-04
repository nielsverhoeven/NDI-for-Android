# Regression Evidence: Android APK Startup Abort Fix (#153)

## Summary

Issue #153 tracked an app startup abort that occurred immediately after launching
an installed APK on Android. The app died before any UI appeared.

This document records the root cause, fixes applied, and verification evidence.

---

## Root Cause

**Failure class**: Mono Fast Deployment startup abort.

When a Debug APK is installed via `adb install` (without an active IDE Fast
Deployment session), the Mono runtime looks for push-deployed assemblies under
the per-app override path:

```
/data/user/0/com.ndi.android/files/.__override__/arm64-v8a
```

When those assemblies are absent (as they will be on any plain `adb install`
outside the IDE), `libmonodroid.so` immediately aborts the process:

```
F/monodroid: No assemblies found in '.../__override__/arm64-v8a' or '<unavailable>'.
             Assuming this is part of Fast Deployment. Exiting...
F/libc:      Fatal signal 6 (SIGABRT)
```

---

## Fixes Applied

### T001 — NdiForAndroid.csproj packaging normalization
**Commit**: `f11d81c` on branch `156-task153-normalize-android-apk-packaging-to-avoid-fast-deployment-abort-path`

Added `Debug`-configuration Android properties to embed assemblies into the APK
rather than relying on Fast Deployment:

```xml
<PropertyGroup Condition="'$(TargetFramework)' == 'net10.0-android' and '$(Configuration)' == 'Debug'">
  <EmbedAssembliesIntoApk>true</EmbedAssembliesIntoApk>
  <AndroidUseSharedRuntime>false</AndroidUseSharedRuntime>
</PropertyGroup>
```

**Effect**: A Debug APK installed via `adb install` now embeds all Mono assemblies
inside the APK itself. The Fast Deployment override path is not consulted at startup.

### T002 — Local validation script
Added `testing/scripts/validate-apk-startup.sh` — a reproducible validation
that uninstalls any prior installation, installs the APK, launches the app, and
confirms the crash buffer is clean.

### T003 — CI emulator install hardening
Already merged to `main`:
- `testing/e2e/scripts/run-emulator-tests.sh`: `adb uninstall com.ndi.android` before `adb install`.
- `.github/workflows/emulator-tests.yml`: `dotnet publish -c Release` (Release APK embeds assemblies by default).

**Effect**: CI cached-emulator installs no longer fail with
`INSTALL_FAILED_UPDATE_INCOMPATIBLE` due to stale Debug-signed package state.

### T004 — Startup smoke tests
Added `tests/MauiApp.UITests/StartupSmokeTests.cs` with two tests:
- `AppStartup_DoesNotAbort_DriverSessionEstablished` — Appium session creation proves app survived startup.
- `AppStartup_RendersUiWithin15Seconds` — asserts at least one visible UI element within 15 seconds.

---

## Verification Evidence

### Release APK publish (post-fix)
```
dotnet publish src/MauiApp/NdiForAndroid.csproj -f net10.0-android -c Release -o stage1-release-publish -m:1 -v minimal

NdiForAndroid.Core net10.0 succeeded
NdiForAndroid net10.0-android succeeded with 3 warning(s)
Build succeeded with 3 warning(s) in 261.8s
```
Exit code: **0**

### Debug APK obj/Debug/net10.0-android/build.props after T001
```
embedassembliesintoapk=true   ← previously false
androidusesharedruntime=false
```

---

## Troubleshooting Reference

| Symptom | Likely cause | Fix |
|---|---|---|
| `No assemblies found in .__override__/arm64-v8a`; SIGABRT | Debug APK installed without Fast Deployment session | Rebuild with `EmbedAssembliesIntoApk=true` or use Release APK |
| `INSTALL_FAILED_UPDATE_INCOMPATIBLE` | Cached emulator/device has different-signed package | `adb uninstall com.ndi.android` then reinstall |
| `XAGNM7009 missing native code generation state` | Stale Release build intermediates | `dotnet clean -c Release` then rebuild |
| App exits silently; `pidof com.ndi.android` returns nothing | Check crash buffer | `adb logcat -b crash -d -v time` |

See also: `.github/skills/android-ci-failure-patterns/SKILL.md`
