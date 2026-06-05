---
name: android-build-install-run
description: >
  Build, install, launch, and smoke-check the latest Android app build on a
  connected device or emulator for this repository. Use when validating device-
  visible behavior, reproducing acceptance criteria on hardware, or preparing a
  clean APK install before manual or automated Android verification.
---

## Purpose

Provide one canonical, repeatable procedure for producing the latest APK,
installing it on a connected Android target, launching the app, and checking for
startup failures that are common in this repository.

Use this skill when:
- a task's acceptance criteria depend on behavior visible on a device or emulator
- the user asks to install or launch the latest app build
- an implementation change touches navigation, UI flow, lifecycle, permissions,
  or native NDI integration that cannot be fully validated by unit tests alone
- the tester needs a clean, reproducible deployment path before UI or e2e checks

---

## Preconditions

Before starting:
1. Work from the current issue branch.
2. Confirm a device or emulator is connected:
   ```powershell
   adb devices
   ```
3. Confirm the target finished booting:
   ```powershell
   adb shell getprop sys.boot_completed
   ```
   Expected output: `1`

If no device is connected or boot is incomplete, stop and report that the device
validation gate is blocked.

---

## Standard Flow

### 1. Build the latest APK

Use the project file directly so the output always reflects the current branch:

```powershell
dotnet build src/MauiApp/NdiForAndroid.csproj -f net10.0-android -c Debug -m:1 -v minimal
```

Use this when you need the latest local changes installed quickly on a connected
device for manual validation.

If a standalone APK is required instead of the default build output, publish a
Release package:

```powershell
dotnet publish src/MauiApp/NdiForAndroid.csproj -f net10.0-android -c Release -o publish-output -m:1 -v minimal
```

### 2. Resolve the APK path

Preferred install targets, in order:
1. `src/MauiApp/bin/Debug/net10.0-android/com.ndi.android.apk`
2. `publish-output/com.ndi.android-Signed.apk`

If neither exists after the build step, stop and fix the build output issue
before continuing.

### 3. Uninstall any existing package

Always remove the installed package before reinstalling:

```powershell
adb uninstall com.ndi.android
```

Ignore failure when the package is not installed.

### 4. Install the latest APK

```powershell
adb install <apk-path>
```

Expected output contains `Success`.

If install fails with `INSTALL_FAILED_UPDATE_INCOMPATIBLE`, retry after ensuring
the uninstall step completed. If the app aborts immediately with Mono fast
deployment symptoms, see `/android-ci-failure-patterns`.

### 5. Launch the app

```powershell
adb shell monkey -p com.ndi.android -c android.intent.category.LAUNCHER 1
```

Then confirm the process is running:

```powershell
adb shell pidof com.ndi.android
```

### 6. Check for startup crashes

If the app does not stay alive or behavior is suspicious, inspect the crash
buffer instead of general logcat:

```powershell
adb logcat -b crash -d -v time
```

Key failure signatures for this repo:
- `No assemblies found in .__override__` -> wrong APK variant / fast deployment issue
- `INSTALL_FAILED_UPDATE_INCOMPATIBLE` -> uninstall/reinstall mismatch
- native abort / signal traces -> capture and report exact lines

---

## Validation Output

When using this skill, report these facts back to the calling workflow:
- build command used
- APK path installed
- install result
- launch result
- running PID, if available
- whether crash buffer was clean or what exact failure signature appeared
- whether the observed device behavior matches the issue acceptance criteria

For issue comments, summarize device validation like this:

```markdown
### Device validation
- Built latest Android app from current branch
- Installed APK: `<path>`
- Launch: ✅
- Process check: `<pid or failure>`
- Acceptance check: `<what was visually/functionally confirmed on device>`
```

---

## When This Skill Is Mandatory

Use this skill instead of ad-hoc device commands when:
- the implementer is validating a UI-facing or device-facing acceptance criterion
- the tester is preparing Stage 4 or Stage 5 verification
- the orchestrator asks for install-and-verify evidence before advancing a feature

This skill complements `dotnet build` and `dotnet test`; it does not replace
them.