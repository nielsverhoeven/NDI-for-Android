# Quickstart: Three-Screen Navigation (Home, Stream, View)

## 1. Prerequisites

From repository root:

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
.\gradlew.bat --version
npm --prefix testing/e2e ci
```

Expected environment:

- Android SDK/NDK/CMake and JDK baseline compatible with repository wrapper.
- NDI SDK bridge prerequisites already configured for existing Stream/View flows.
- At least one phone-sized emulator/device and one tablet-sized emulator/device
  for adaptive navigation validation.

## 2. Test-First Development Sequence

1. Add/adjust failing unit tests first for:
   - top-level destination state selection
   - launcher default Home vs Recents restore behavior
   - duplicate destination tap no-op behavior
   - continuity behavior (Stream keeps running, View stops/no autoplay)
2. Add/adjust failing UI flow tests for:
   - Home -> Stream
   - Home -> View
   - Stream -> View
   - View -> Stream
   - active destination indication on phone bottom nav and tablet rail
3. Implement the minimum code to pass tests.
4. Refactor while all tests stay green.

## 3. Local Build and Test Commands

```powershell
.\gradlew.bat test
.\gradlew.bat connectedAndroidTest
.\gradlew.bat :app:assembleDebug
```

If this branch includes Playwright navigation/e2e assertions, run:

```powershell
npm --prefix testing/e2e run test
```

## 4. Functional Validation Checklist

- Launcher icon launch lands on Home.
- Recents/task restore returns to the last top-level destination.
- Home dashboard shows Stream/View quick actions.
- Stream destination remains reachable and preserves output continuity.
- View destination remains reachable and preserves selection without autoplay.
- Repeated tap on current destination does not duplicate top-level entries.
- Deep links to viewer/output still work and expose top-level navigation to Home,
  Stream, and View.

## 5. Release-Grade Validation

```powershell
pwsh ./scripts/verify-android-prereqs.ps1
.\gradlew.bat verifyReleaseHardening
.\gradlew.bat :app:assembleRelease
```

Collect validation evidence under `specs/003-three-screen-navigation/validation/`
when implementation begins (layout matrix, navigation transition results,
continuity checks, and release-hardening outputs).

