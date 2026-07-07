---
name: android-ci-failure-patterns
description: Reference guide for Android-specific CI failure classes encountered in this repository. Covers Mono Fast Deployment aborts, APK signature mismatches on cached emulators, stale Release build state, and the correct remediation for each. Use when diagnosing emulator-tests workflow failures, adb install errors, or libmonodroid crashes in CI or on a physical device.
---

## Purpose

Provide a structured diagnosis and remediation reference for the Android-specific
failure classes that have occurred in this repo. Each entry contains:
- **Signature** — what the logs show
- **Root cause** — why it happens
- **Fix** — the minimal, correct remediation
- **Prevention** — how to stop it reoccurring

---

## Failure Class 1 — Mono Fast Deployment Abort (SIGABRT on startup)

### Signature
```
F/monodroid: No assemblies found in
  '/data/user/0/com.ndi.android/files/.__override__/arm64-v8a'
  or '<unavailable>'. Assuming this is part of Fast Deployment. Exiting...
F/libc: Fatal signal 6 (SIGABRT), code -1 (SI_QUEUE)
```
Stack frame: `xamarin::android::Helpers::abort_application` in `libmonodroid.so`

### Root Cause
A **Debug APK** was installed via `adb install` without a live IDE Fast Deployment
session. The Debug build expects Mono assemblies to be pushed separately into
`.__override__` by the IDE tooling. Without them the runtime aborts immediately.

### Fix
Build and install a **Release APK** instead:
```bash
dotnet publish src/MauiApp/NdiForAndroid.csproj \
  -f net10.0-android -c Release -o publish-output
adb install -r publish-output/com.ndi.android-Signed.apk
```

In CI (`emulator-tests.yml`) set the publish step to `-c Release` not `-c Debug`.

### Prevention
- CI must always publish with `-c Release` for any standalone APK install.
- Debug builds should only be installed via `dotnet run` / IDE hot-reload so the
  Fast Deployment push also fires.
- The emulator-tests workflow uses `dotnet publish` (not `dotnet build`), which
  sets `EmbedAssembliesIntoApk=true` in Release — that is the correct path.

---

## Failure Class 2 — APK Signature Mismatch on Cached Emulator

### Signature
```
adb: failed to install apk/com.ndi.android-Signed.apk:
  Failure [INSTALL_FAILED_UPDATE_INCOMPATIBLE: Existing package
  com.ndi.android signatures do not match newer version; ignoring!]
```

### Root Cause
The emulator (often restored from an AVD cache) already has `com.ndi.android`
installed with a **different signing key** — for example a previous Debug build
signed with the debug keystore while the new APK is Release-signed (or vice
versa). Android refuses to overwrite with `adb install -r` when certificates differ.

### Fix
Uninstall the existing package before installing the new one:
```bash
adb uninstall com.ndi.android 2>/dev/null || true
adb install "$APK_PATH"
```

In `testing/e2e/scripts/run-emulator-tests.sh` this is already applied:
```bash
adb uninstall com.ndi.android >/dev/null 2>&1 || true
adb install -r "$APK_PATH"
```

### Prevention
- Always call `adb uninstall <package>` before `adb install` in scripts that
  may run on a warmed (cached) emulator.
- The `|| true` guard is required so the uninstall does not fail the script when
  the package is not yet installed.
- Do not share AVD caches across PR branches that may have different signing
  configurations — or invalidate the AVD cache key when the signing config changes.

---

## Failure Class 3 — Stale Release Build State (XAGNM7009)

### Signature
```
error XAGNM7009: System.InvalidOperationException: Internal error:
  missing native code generation state for architecture 'Arm64'
  at Xamarin.Android.Tasks.GenerateNativeMarshalMethodSources.EnsureCodeGenState
```

### Root Cause
The `obj/Release/net10.0-android/` intermediate directory contains partial
artifacts from a previous interrupted or partially-completed Release build.
The AOT compiler state is inconsistent across architectures.

### Fix
Clean before rebuilding:
```powershell
dotnet clean src/MauiApp/NdiForAndroid.csproj -f net10.0-android -c Release
dotnet publish src/MauiApp/NdiForAndroid.csproj -f net10.0-android -c Release -o publish-output
```

### Prevention
- In CI, the workspace is always fresh (no `obj/` from a prior run), so this
  failure is local-only.
- Locally: run `dotnet clean` before any Release build if the previous Release
  build was interrupted (`Ctrl+C` or killed).
- Do not rely on incremental Release Android builds in local development.

---

## Emulator Install Checklist

Before any `adb install` step in a test or CI script, verify:

1. Emulator is booted: `adb shell getprop sys.boot_completed` returns `1`
2. Uninstall existing package: `adb uninstall com.ndi.android || true`
3. Install the **Release**-signed APK: `adb install "$APK_PATH"`
4. Verify install: `adb shell pm list packages | grep com.ndi.android`
5. Confirm no Fast Deployment abort: `adb logcat -b crash -d | grep monodroid`

---

## Quick Reference Table

| Error in logs | Class | Fix |
|---|---|---|
| `No assemblies found in .__override__` | Fast Deployment Abort | Use Release APK |
| `INSTALL_FAILED_UPDATE_INCOMPATIBLE` | Signature Mismatch | `adb uninstall` first |
| `XAGNM7009 missing native code generation state` | Stale Release Build | `dotnet clean` then rebuild |
| `INSTALL_FAILED_VERSION_DOWNGRADE` | Version downgrade | Increment `ApplicationVersion` |
| App exits silently, `adb shell pidof com.ndi.android` returns nothing | Check crash buffer | `adb logcat -b crash -d -v time` |
