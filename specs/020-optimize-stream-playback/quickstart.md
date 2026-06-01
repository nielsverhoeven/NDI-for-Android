# Quickstart: NDI Stream Playback Optimization

**Phase**: 1 (Design - developer onboarding)  
**Date**: March 28, 2026  
**Target Audience**: Android developers implementing feature 020

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Local Setup](#local-setup)
3. [Module Structure](#module-structure)
4. [First Test](#first-test)
5. [Development Workflow](#development-workflow)
6. [Debugging Guide](#debugging-guide)
7. [Common Issues](#common-issues)

---

## Prerequisites

### System Requirements

- **OS**: Windows 10+ or macOS 12+
- **Java**: JDK 21+ (target: Java 17 bytecode)
- **Kotlin**: Compiler 2.2.10 (via Gradle)
- **Android SDK**: `compileSdk 34`, `minSdk 26`
- **Gradle**: 9.2.1 (via wrapper: `./gradlew` or `gradlew.bat`)

### Android Prerequisites

Run the prerequisite check script:

```powershell
# Windows
.\scripts\verify-android-prereqs.ps1

# Or manually
.\gradlew --version
# Expected: Gradle 9.2.1, JDK: java version "21.x"
```

### NDI SDK Setup

NDI SDK is pre-compiled in `ndi/sdk-bridge/`:

```
ndi/sdk-bridge/
├── src/main/cpp/
│   ├── CMakeLists.txt
│   ├── ndi_receiver.cpp
│   ├── ndi_structs.h
│   └── ...
├── build.gradle.kts
└── README.md
```

**No manual NDI SDK download needed** —JNI bindings already wired.

---

## Local Setup

### 1. Clone Repository

```bash
git clone https://github.com/NDI-for-Android/NDI-for-Android.git
cd NDI-for-Android

# Create feature branch
git checkout -b 020-optimize-stream-playback
```

### 2. Initialize Gradle

```powershell
# Windows
.\scripts\init-gradle-wrapper.ps1

# Verify
.\gradlew --version
```

### 3. Open in Android Studio

```
File → Open → Select NDI-for-Android
```

**Expected structure** in Project panel:

```
NDI-for-Android
├── app (Main app module)
├── feature
│   ├── ndi-browser
│   │   ├── domain
│   │   ├── data
│   │   └── presentation
│   └── theme-editor
├── core
│   ├── model
│   ├── database
│   └── testing
├── ndi
│   └── sdk-bridge (Native NDI JNI bindings)
└── gradle (Wrapper config)
```

### 4. Sync and Build

```bash
# From Android Studio terminal or command line
./gradlew clean assemble

# Or build only the ndi-browser feature module
./gradlew :feature:ndi-browser:presentation:assemble
```

---

## Module Structure

### Affected Modules

#### `feature/ndi-browser/domain/` (Contracts)

```
domain/src/main/java/com/ndi/feature/ndibrowser/domain/
├── repository/
│   ├── NdiRepositories.kt (existing interface)
│   ├── NdiViewerRepository.kt (add quality methods)
│   └── QualityProfileRepository.kt (NEW)
└── model/
    └── QualityProfile.kt (NEW)
```

**Add these interfaces**:
- `QualityProfileRepository` (quality preference CRUD)
- Extend `NdiViewerRepository` with:
  - `applyQualityProfile(profile)`
  - `getPlaybackOptimizationFlow()`
  - `degradeQuality(profile)`
  - `handleStreamDisconnection(sourceId)`

#### `feature/ndi-browser/data/` (Implementation)

```
data/src/main/java/com/ndi/feature/ndibrowser/data/
├── repository/
│   ├── NdiViewerRepositoryImpl.kt (modify)
│   └── QualityProfileRepositoryImpl.kt (NEW)
├── local/
│   └── SharedPreferencesQualityStore.kt (NEW)
└── model/
    ├── PlaybackOptimization.kt (NEW)
    └── QualityPreference.kt (NEW)
```

**Implement**:
- `QualityProfileRepositoryImpl` (+ SharedPreferences wrapper)
- `SharedPreferencesQualityStore` (persist user preference)
- `NdiViewerRepositoryImpl` extensions (call NDI SDK bridge for codec/FPS selection)

#### `feature/ndi-browser/presentation/` (UI & VM)

```
presentation/src/main/java/com/ndi/feature/ndibrowser/
├── viewer/
│   ├── ViewerScreen.kt (modify - add quality menu)
│   ├── ViewerViewModel.kt (modify - add quality state)
│   ├── PlayerScalingViewModel.kt (NEW)
│   ├── PlayerScalingCalculator.kt (NEW interface)
│   ├── PlayerScalingCalculatorImpl.kt (NEW impl)
│   └── QualitySettingsMenuComposable.kt (NEW UI)
└── di/
    └── ViewerDependencies.kt (add QualityProfileRepository)
```

**Implement**:
- `ViewerViewModel` extensions (listen to quality preference, metrics flow)
- `PlayerScalingViewModel` (manage scaled dimensions)
- `PlayerScalingCalculator` interface + implementation
- `QualitySettingsMenuComposable` (Material 3 quality menu UI)

---

## First Test

### Test 1: Unit Test - QualityProfile Data Class

**File**: `feature/ndi-browser/domain/test/QualityProfileTest.kt`

```kotlin
import org.junit.jupiter.api.Test
import com.ndi.feature.ndibrowser.domain.model.QualityProfile
import org.assertj.core.api.Assertions.assertThat

class QualityProfileTest {
    
    @Test
    fun testSmoothProfileDefaults() {
        val smooth = QualityProfile.Smooth
        
        assertThat(smooth.id).isEqualTo("smooth")
        assertThat(smooth.displayName).isEqualTo("Smooth")
        assertThat(smooth.maxFrameRate).isEqualTo(30)
        assertThat(smooth.baseResolution.width).isEqualTo(720)
    }
    
    @Test
    fun testProfilePriorityOrdering() {
        val profiles = listOf(
            QualityProfile.HighQuality,
            QualityProfile.Smooth,
            QualityProfile.Balanced
        )
        
        val sorted = profiles.sortedBy { it.priority }
        
        assertThat(sorted).containsExactly(
            QualityProfile.Smooth,
            QualityProfile.Balanced,
            QualityProfile.HighQuality
        )
    }
}
```

**Run**:
```bash
./gradlew :feature:ndi-browser:domain:test
```

**Expected**: All tests pass (green ✓)

---

### Test 2: Unit Test - PlayerScalingCalculator

**File**: `feature/ndi-browser/presentation/test/PlayerScalingCalculatorTest.kt`

```kotlin
import org.junit.jupiter.api.Test
import org.junit.jupiter.api.BeforeEach
import com.ndi.feature.ndibrowser.presentation.viewer.PlayerScalingCalculatorImpl
import com.ndi.feature.ndibrowser.presentation.viewer.PlayerScalingState
import com.ndi.feature.ndibrowser.presentation.viewer.LetterboxSide
import org.assertj.core.api.Assertions.assertThat

class PlayerScalingCalculatorTest {
    
    private lateinit var calculator: PlayerScalingCalculatorImpl
    
    @BeforeEach
    fun setup() {
        calculator = PlayerScalingCalculatorImpl()
    }
    
    @Test
    fun testWideStreamNarrowContainer() {
        val state = PlayerScalingState(
            availableWidth = 1080,
            availableHeight = 2340,
            streamAspectRatio = 16f / 9f,
            orientation = PlayerScalingState.Orientation.PORTRAIT
        )
        
        val scaled = calculator.calculateScaledDimensions(state)
        
        // Width-constrained: scale to container width
        assertThat(scaled.width).isEqualTo(1080)
        // Height: 1080 / (16/9) = 1080 * 9/16 = 608
        assertThat(scaled.height).isEqualTo(608)
        assertThat(scaled.letterboxSide).isEqualTo(LetterboxSide.TOP_BOTTOM)
        assertThat(calculator.meetsUtilizationTarget(state)).isTrue()
    }
    
    @Test
    fun testPortraitStreamWideContainer() {
        val state = PlayerScalingState(
            availableWidth = 2340,
            availableHeight = 1080,
            streamAspectRatio = 9f / 16f,
            orientation = PlayerScalingState.Orientation.LANDSCAPE
        )
        
        val scaled = calculator.calculateScaledDimensions(state)
        
        // Height-constrained: scale to container height
        // Width: 1080 * (9/16) = 607
        assertThat(scaled.width).isEqualTo(607)
        assertThat(scaled.height).isEqualTo(1080)
        assertThat(scaled.letterboxSide).isEqualTo(LetterboxSide.LEFT_RIGHT)
        assertThat(calculator.meetsUtilizationTarget(state)).isTrue()
    }
    
    @Test
    fun testOrientationChangeDetection() {
        val portrait = PlayerScalingState(1080, 2340, 16f/9f)
        val landscape = PlayerScalingState(2340, 1080, 16f/9f)
        
        assertThat(calculator.requiresRecalculation(portrait, landscape)).isTrue()
    }
}
```

**Run**:
```bash
./gradlew :feature:ndi-browser:presentation:test
```

**Expected**: All tests pass (green ✓)

---

### Test 3: Integration Test - SharedPreferences Storage

**File**: `feature/ndi-browser/data/androidTest/SharedPreferencesQualityStoreTest.kt`

```kotlin
import android.content.Context
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import com.ndi.feature.ndibrowser.data.local.SharedPreferencesQualityStore
import com.ndi.feature.ndibrowser.data.model.QualityPreference
import org.assertj.core.api.Assertions.assertThat

@RunWith(AndroidJUnit4::class)
class SharedPreferencesQualityStoreTest {
    
    private lateinit var context: Context
    private lateinit var store: SharedPreferencesQualityStore
    
    @Before
    fun setup() {
        context = ApplicationProvider.getApplicationContext()
        store = SharedPreferencesQualityStore(context)
    }
    
    @Test
    fun testSaveAndLoadGlobalPreference() {
        val preference = QualityPreference(selectedProfileId = "balanced")
        
        store.saveQualityPreference(preference)
        val loaded = store.loadQualityPreference()
        
        assertThat(loaded.selectedProfileId).isEqualTo("balanced")
    }
    
    @Test
    fun testSaveAndLoadSourceSpecificPreference() {
        val sourceId = "test-ndi-source"
        val preference = QualityPreference(sourceId, "high_quality")
        
        store.saveQualityPreference(preference)
        val loaded = store.loadQualityPreference(sourceId)
        
        assertThat(loaded.selectedProfileId).isEqualTo("high_quality")
    }
    
    @Test
    fun testFallbackToGlobalWhenSourceNotFound() {
        store.saveQualityPreference(QualityPreference(selectedProfileId = "smooth"))
        
        val loaded = store.loadQualityPreference("unknown-source")
        
        assertThat(loaded.selectedProfileId).isEqualTo("smooth")  // Falls back to global
    }
}
```

**Run**:
```bash
./gradlew :feature:ndi-browser:data:connectedAndroidTest
```

**Prerequisites**: Android emulator running (see [Emulator Setup](#emulator-setup))

**Expected**: All tests pass (green ✓)

---

## Development Workflow

### 1. Pick a Task from `specs/020-optimize-stream-playback/tasks.md`

(Tasks will be generated via `/speckit.tasks` command after this planning phase)

Example tasks:

```
- [ ] P1-1: Implement QualityProfile domain model
- [ ] P1-2: Implement QualityProfileRepository interface
- [ ] P1-3: Implement SharedPreferencesQualityStore
- [ ] P2-1: Implement PlayerScalingCalculator
- [ ] P3-1: Implement QualitySettingsMenuComposable
```

### 2. Red-Green-Refactor Cycle (TDD)

**Step 1: Write Failing Test** (Red)

```bash
# Create test file with failing test case
touch feature/ndi-browser/domain/test/QualityProfileRepositoryTest.kt
# Write test that calls non-existent class → RED
./gradlew :feature:ndi-browser:domain:test
# Output: FAILED - class not found
```

**Step 2: Write Minimum Impl** (Green)

```bash
# Create implementation
touch feature/ndi-browser/domain/repository/QualityProfileRepository.kt
# Add minimal interface + stub
./gradlew :feature:ndi-browser:domain:test
# Output: PASSED ✓
```

**Step 3: Refactor** (Refactor)

```bash
# Switch to implementation branch
./gradlew :feature:ndi-browser:data:assemble
# Add proper implementation, run tests
./gradlew :feature:ndi-browser:data:test
# Output: All passing ✓
```

### 3. Build & Validate

```bash
# Full feature module build
./gradlew :feature:ndi-browser:assemble

# Or incremental builds
./gradlew :feature:ndi-browser:domain:assemble
./gradlew :feature:ndi-browser:data:assemble
./gradlew :feature:ndi-browser:presentation:assemble
```

### 4. Lint & Format Check

```bash
# Check for code style violations
./gradlew :feature:ndi-browser:lint

# Auto-format code
./gradlew :feature:ndi-browser:spotlessApply
```

### 5. Commit & Push

```bash
git add feature/ndi-browser/
git commit -m "P1-1: Implement QualityProfile domain model

- Add data class with id, displayName, resolution, frameRate, codec, priority
- Add sealed class subtypes: Smooth, Balanced, HighQuality
- Add Resolution and Codec value objects
- Add unit tests for profile ordering"

git push origin 020-optimize-stream-playback
```

---

## Debugging Guide

### Frame Rate Monitoring

**Enable logging in ViewerViewModel**:

```kotlin
class ViewerViewModel(...) {
    private val frameMonitor = FrameRateMonitor()
    
    private fun startFrameMonitoring() {
        ndiRepository.getPlaybackOptimizationFlow()
            .collect { optimization ->
                Log.d("FrameMonitor", """
                    FPS: ${optimization.currentFrameRate}
                    Drops: ${optimization.frameDropPercentage}%
                    Buffer: ${optimization.bufferHealthPercent}%
                    Profile: ${optimization.selectedProfile.displayName}
                """.trimIndent())
            }
    }
}
```

**Run and monitor**:
```bash
./gradlew :app:installDebug
adb shell am start com.ndi.app/.MainActivity
adb logcat | grep FrameMonitor
```

### Quality Profile Switching

**Add debug UI button to QualitySettingsMenuComposable**:

```kotlin
Button(
    onClick = { 
        viewModel.switchQualityProfile("balanced")
        Log.d("Quality", "User switched to: balanced")
    },
    modifier = Modifier.padding(8.dp)
) {
    Text("Balanced")
}
```

**Monitor in logs**:
```bash
adb logcat | grep "Quality\|FrameMonitor"
```

### SharedPreferences Inspection

**View stored preferences**:
```bash
adb shell
$ su
$ cat /data/data/com.ndi.app/shared_prefs/ndi_quality_prefs.xml
```

Expected output:
```xml
<?xml version='1.0' encoding='utf-8' standalone='yes' ?>
<map>
    <string name="quality_profile_id">balanced</string>
    <string name="source_id_ndi-source-1">high_quality</string>
</map>
```

### Breakpoint Debugging

**Set breakpoint in PlayerScalingCalculator**:

1. Open `feature/ndi-browser/presentation/.../PlayerScalingCalculatorImpl.kt`
2. Click line number (e.g., line 42) to set breakpoint
3. Run app in debug mode:
   ```bash
   ./gradlew :app:installDebug
   ./gradlew :app:debugAssemble
   ```
4. AttachDebugger in Android Studio → Run → Debugger Console
5. Trigger layout recalculation (rotate device)
6. Inspect variables in Debug window

---

## Common Issues

### Issue 1: "Cannot resolve symbol QualityProfileRepository"

**Cause**: Interface not yet implemented in domain module

**Fix**:
```bash
# Check if file exists
ls -la feature/ndi-browser/domain/src/main/java/com/ndi/feature/ndibrowser/domain/repository/

# Sync Gradle
./gradlew :feature:ndi-browser:domain:assemble

# Clear caches if still failing
./gradlew clean
./gradlew :feature:ndi-browser:assemble
```

### Issue 2: "SharedPreferences key not found"

**Cause**: User preference not saved yet (first launch)

**Fix**: Test with fallback logic:
```kotlin
fun loadQualityPreference(sourceId: String? = null): QualityPreference {
    val profileId = prefs.getString("quality_profile_id", "smooth") ?: "smooth"
    return QualityPreference(sourceId, profileId)
}
```

### Issue 3: "Aspect ratio calculation produces NaN"

**Cause**: Stream aspect ratio is 0 or infinity

**Fix**: Add validation:
```kotlin
require(state.streamAspectRatio > 0 && state.streamAspectRatio.isFinite()) {
    "Invalid stream aspect ratio: ${state.streamAspectRatio}"
}
```

### Issue 4: "AndroidTest fails with 'Activity not launched'"

**Cause**: Emulator not running or app not installed

**Fix**:
```bash
# Start emulator
emulator -avd Pixel_4_API_34

# Wait for boot completion
adb wait-for-device

# Run tests
./gradlew :feature:ndi-browser:data:connectedAndroidTest
```

### Issue 5: "Gradles fails: Kotlin compilation error"

**Cause**: Kotlin version mismatch or syntax error

**Fix**:
```bash
# Verify Kotlin version (should be 2.2.10)
grep "kotlin" gradle/libs.versions.toml

# Check for syntax errors
./gradlew :feature:ndi-browser:compileKotlin

# If compilation fails, review error message and fix (e.g., missing semicolon)
```

---

## Next Steps

1. **Read the full specifications**: `specs/020-optimize-stream-playback/spec.md`
2. **Review data model**: `specs/020-optimize-stream-playback/data-model.md`
3. **Study contracts**: `specs/020-optimize-stream-playback/contracts/`
4. **Generate tasks**: `/speckit.tasks` (once planning complete)
5. **Pick first task** from `tasks.md` and start TDD cycle
6. **Push PR** once 3-4 tasks complete and tests pass

---

## Resources

- **NDI Documentation**: https://docs.ndi.video/
- **Android Jetpack Docs**: https://developer.android.com/jetpack
- **Kotlin Coroutines**: https://kotlinlang.org/docs/coroutines-overview.html
- **Room Database**: https://developer.android.com/training/data-storage/room (optional future migration)
- **Project Constitution**: `specs/000-constitution.md`

---

## Support

- **Issues**: Check `troubleshooting.md` in project root
- **Architecture Questions**: Review `docs/architecture.md`
- **NDI Integration**: See `ndi/sdk-bridge/README.md`
- **Testing Strategy**: See `docs/testing.md`

---

## Phase 6 Validation Runbook (2026-03-29)

Use these commands and checks for final validation in this repository state.

### Device install gate

```powershell
adb devices
./gradlew.bat :app:installDebug --console=plain
```

Expected install output includes `Installed on 1 device.`

### About section version visibility

1. Open Settings.
2. Open About.
3. Confirm `App version` is visible and formatted as `versionName (versionCode)`.

Implementation reference:
- `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/settings/SettingsDetailRenderer.kt`

### Full feature module unit tests

```powershell
./gradlew.bat :feature:ndi-browser:data:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest --console=plain
```

### Release hardening/build gate

```powershell
./gradlew.bat --stop
./gradlew.bat :app:assembleRelease --no-daemon --console=plain
```

### Telemetry review checkpoints

Verify telemetry emission paths in:
- `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerRecoveryTelemetry.kt`
- `feature/ndi-browser/presentation/src/main/java/com/ndi/feature/ndibrowser/viewer/ViewerViewModel.kt`

Events reviewed:
- profile selected
- quality downgraded
- quality recovered
- recovery attempted
- recovery result
- playback started/stopped

---

**Happy coding! 🚀**
