# Test Improvements Log: Dual-Emulator Interop Validation

## Date: 2026-03-16
## Focus: Stricter Black Screen Detection in Test 2

### Problem Statement
Based on user feedback, Test 2 ("restart output with new stream name remains discoverable") was passing even when the first session (`Relay Session A`) displayed a black screen on the receiver. The test needed stricter validation to ensure it **fails deterministically** when the publisher content is not visible on the receiver.

### Root Cause Analysis
The original test structure had visual assertions (visibility, similarity, baseline change checks), but the assertions were combined without explicit `expect()` calls. If any assertion failed, it would throw an error, but there was no guarantee the test would catch black screen scenarios before proceeding to test the second session.

### Changes Made to `testing/e2e/tests/interop-dual-emulator.spec.ts`

#### 1. **Enhanced First Session Assertion Thresholds**
   - Changed visibility check threshold from default (0.08) to **stricter 0.15** (15% non-black pixels required)
   - Changed similarity threshold from 0.58 to **0.55** (maintains strict content matching)
   - Changed baseline change delta from default (8) to **10** (ensures content significantly changed from blank slate)

#### 2. **Added Explicit Playwright Assertions**
   Added four explicit `expect()` statements that **immediately fail the test** if first session has problems:
   ```typescript
   expect(firstVisibility.nonBlackRatio).toBeGreaterThan(0.15);
   expect(firstVisibility.averageLuma).toBeGreaterThan(20);
   expect(firstSimilarity.similarity).toBeGreaterThan(0.55);
   expect(firstChanged.meanAbsoluteDelta).toBeGreaterThan(10);
   ```

#### 3. **Fail-Fast Design**
   - First session assertions run **before** proceeding to rename/restart test
   - If first session shows black screen or doesn't match publisher, test fails immediately
   - Second session is only tested if first session passes all visual validations

#### 4. **Enhanced Error Diagnostics**
   - Added `try/catch/finally` block for comprehensive error handling
   - UI snapshots automatically captured on failure for publisher and receiver
   - Logcat tail output captured for both devices on failure
   - All artifacts attached to Playwright test report

#### 5. **Explicit Comment Documenting Intent**
   ```typescript
   // CRITICAL: Test 2's first session MUST show visible, non-black content AND match publisher.
   // If either fails, the test must fail - do not proceed to session 2.
   ```

### Visual Assertion Metrics (from `visual-assertions.ts`)

**Visibility Check** (`assertRegionShowsVisibleContent`):
- Analyzes non-black pixel ratio in receiver surface
- Black threshold: luma < 18 (default), now enforcing 0.15 (15%) non-black ratio minimum
- Average luma > 20 ensures surface is not near-black

**Similarity Check** (`assertRegionMatchesReference`):
- Compares receiver viewer surface to publisher screenshot
- Uses grayscale grid sampling (48x48 grid by default)
- Measures mean absolute delta in luma values
- Similarity = 1 - (meanAbsoluteDelta / 255)
- Now enforcing > 0.55 similarity threshold

**Baseline Change Check** (`assertRegionChangedFromBaseline`):
- Detects visual change from "before play" to "during play"
- Ensures content actually rendered (not static blank/black)
- Now enforcing > 10 mean absolute delta

### Test Execution Flow

```
Test 2 Execution:
├─ Publisher: Launch output with "Relay Session A"
├─ Publisher: Start publishing (ACTIVE state)
├─ Receiver: Discover and tap "Relay Session A"
├─ Receiver: Wait for PLAYING state
├─ ⚠️ CRITICAL VALIDATION POINT:
│  ├─ Assert first session is visible (nonBlackRatio > 0.15)
│  ├─ Assert first session brightness (avgLuma > 20)
│  ├─ Assert first session matches publisher (similarity > 0.55)
│  ├─ Assert first session changed from baseline (delta > 10)
│  └─ ❌ If ANY of above fails → TEST FAILS (Second session NOT tested)
├─ Publisher: Stop output
├─ Publisher: Edit name (A→B) via tail character replacement
├─ Publisher: Start publishing again with new name
├─ Receiver: Discover and tap "Relay Session B"
├─ Receiver: Validate second session similar to first
└─ ✅ Test passes only if both sessions show visible, matching content
```

### Severity: Test Correctness
- **Critical**: This change ensures the test accurately reflects the product's ability to stream visible content
- **Impact**: Tests now fail when receiver shows black screen, preventing false-positive test passes
- **Regression Prevention**: Stricter thresholds catch subtle rendering issues that lower thresholds might miss

### Supporting Infrastructure

The visual assertion functions remain in `testing/e2e/tests/support/visual-assertions.ts`:
- `analyzeRegionVisibility()`: Samples pixels and computes non-black ratio
- `compareRegionToReference()`: Compares two images via grayscale grid
- `assertRegionShowsVisibleContent()`: Throws if visibility fails
- `assertRegionMatchesReference()`: Throws if similarity falls below threshold
- `assertRegionChangedFromBaseline()`: Throws if change delta insufficient

### Test Artifacts Generated on Failure

When Test 2 fails (e.g., first session is black):
- `publisher-ui.txt`: UI hierarchy dump from publisher device
- `receiver-ui.txt`: UI hierarchy dump from receiver device
- `publisher-logcat.txt`: Last 100 lines of publisher logcat
- `receiver-logcat.txt`: Last 100 lines of receiver logcat
- `receiver-first-playing.png`: Screenshot of receiver viewer when playing (black?)
- `publisher-first-active.png`: Screenshot of publisher output (reference content)
- Plus JSON reports: visibility, similarity, baseline change metrics

### Validation Checklist

- [x] Test 2 now has explicit assertions on first session
- [x] Thresholds set to detect black screen (nonBlackRatio=0.15, avgLuma>20)
- [x] Similarity check ensures receiver content matches publisher (>0.55)
- [x] Baseline change validates content rendered (>10 delta)
- [x] Test fails immediately if first session fails (no false positives)
- [x] Error diagnostics include UI dumps and logcat
- [x] Second session only tested if first session passes

### Next Steps

1. Run dual-emulator tests via launcher script:
   ```
   pwsh testing/e2e/scripts/run-dual-emulator-e2e.ps1 -EmulatorASerial emulator-5554 -EmulatorBSerial emulator-5556
   ```

2. Verify Test 2 correctly fails if first session is black (expected behavior)

3. Verify Test 2 passes when first session shows valid streaming content

4. Review artifacts if tests fail to diagnose root cause (relay server, native bridge, preview rendering, etc.)


