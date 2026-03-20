# Agent Instructions for Test Execution - NDI for Android

## Guiding Principles for Test Workflow

### 1. **Minimize Terminal Sessions**
- Use ONE persistent terminal per workflow phase
- Avoid spawning background processes for long-running tasks
- Chain commands with `;` on single line when possible
- Report completion before moving to next phase

### 2. **Sequential Execution (No Premature Parallelization)**
- ❌ DO NOT start emulators, download gradle, and run tests in parallel
- ✅ DO: Complete each phase, validate success, proceed to next
- Each phase should have a clear "done" indicator before starting the next

### 3. **Validate Prerequisites Before Action**
- Check if gradle exists → skip download if yes
- Check if emulators exist → skip creation if yes
- Check if APK is built → skip rebuild if source unchanged
- This prevents redundant operations and rate limiting

### 4. **Efficient Output Handling**
- Don't use `Select-String | tail` or Unix commands on Windows PowerShell
- Use PowerShell native: `Select-Object -Last N`, `Select-String`, `Where-Object`
- Save large outputs to files, only print summaries to console
- Check `$LASTEXITCODE` to detect failures

### 5. **Clear Failure Reporting**
- Stop immediately on first failure (use `$ErrorActionPreference = "Stop"`)
- Show the specific error, not entire build log
- Include the command that failed and why
- Suggest corrective action

### 6. **Test-Specific Behaviors**

#### Unit Tests
- Single command: `.\gradlew.bat test --no-daemon`
- Wait for completion (may take 2-5 minutes)
- Check for "BUILD SUCCESSFUL" in output
- Don't assume tests pass just because no error thrown

#### Assertions to Verify
When tests include deduplication/injection logic:
- **Input**: Raw list of items with potential duplicates
- **Transformation**: `.distinctBy(key)` removes duplicates, `.add(0, injected)` adds at head
- **Output**: `[injected, unique1, unique2]` - injection at position 0, then unique items

Example: NdiDiscoveryRepositoryContractTest
- Input: `[source-a, source-a (dup), source-b]`
- Process: deduplicate → `[source-a, source-b]` → inject LOCAL_SCREEN at [0]
- Output: `[device-screen:local, source-a, source-b]`

#### Emulator Tests
- Serial numbers: `emulator-5554` (first), `emulator-5556` (second)
- Boot time: 30-60 seconds (use `adb get-state` to check)
- Must show "device" in `adb devices` before installing APK
- Installation failure often means app is already running → `adb shell am force-stop <package>`

### 7. **When to Skip Phases**
| Condition | Skip Phase | Reason |
|-----------|-----------|--------|
| gradle --version shows 9.2.1 | Environment setup | Already configured |
| *.apk exists and newer than source code | APK build | No source changes |
| `avdmanager list avd` shows 2+ emulators | Emulator creation | Already created |
| APK already installed on emulator | Install | No new build |

### 8. **Error Handling Strategy**
```powershell
# GOOD: Immediate failure detection
$result = & .\gradlew.bat test --no-daemon
if ($LASTEXITCODE -ne 0) {
    Write-Host "Tests failed" -ForegroundColor Red
    exit 1
}

# GOOD: Clear assertion checking
if ($output -match "BUILD SUCCESSFUL") {
    Write-Host "✓ Success"
} else {
    Write-Host "✗ Failed"
    exit 1
}

# BAD: Ignoring exit codes
& .\gradlew.bat test --no-daemon
Write-Host "All done!" # Process may have failed silently

# BAD: Assuming success from lack of errors
$result = & .\gradlew.bat test --no-daemon 2>&1
Write-Host "Tests passed" # No - check the actual output
```

### 9. **Reporting Results**
Use structured format for test results:
```
✓ Phase Name: PASSED
  - Detail 1: description
  - Detail 2: description
  
✗ Phase Name: FAILED
  - Error: description
  - Command: command that failed
  - Fix: corrective action
```

### 10. **Documentation in Code**
When tests validate new logic:
- Include comment explaining the assertion
- Document the input-transform-output flow
- Show expected vs. actual behavior
- Make it clear to future reviewers what's being tested

Example:
```kotlin
@Test
fun discoverSources_deduplicatesByCanonicalSourceId() = runTest {
    val snapshot = repository.discoverSources(DiscoveryTrigger.MANUAL)
    
    assertEquals(DiscoveryStatus.SUCCESS, snapshot.status)
    // Expected: LOCAL_SCREEN (device-screen:local) is added first, then deduped sources
    // Input:    [source-a, source-a (dup), source-b]
    // Process:  .distinctBy { sourceId } removes duplicates
    // Result:   [source-a, source-b] then LOCAL_SCREEN injected at [0]
    assertEquals(listOf("device-screen:local", "source-a", "source-b"), 
                 snapshot.sources.map { it.sourceId })
}
```

## Recommended Scripts to Use

### Validation Script
```powershell
# Simple validation with clear reporting
.\scripts\validate-tests.ps1
```

### Manual Sequential Workflow
If using manual commands:
1. Set environment once at start: `$env:JAVA_HOME = "C:\..."`
2. Run gradle tasks sequentially
3. Check each result before proceeding
4. Use `adb devices` to confirm emulator status
5. Use `adb -s <serial> install <apk>` to install

### Avoid
- Multiple parallel `adb` commands
- Downloading gradle while tests are running
- Creating emulators while gradle is compiling
- Piping to `tail`, `grep`, or other Unix tools
- Assuming silent completion means success

## Success Indicators

- ✅ Single persistent terminal session used
- ✅ Each phase completes before next begins
- ✅ Failed steps are immediately identified
- ✅ Test assertions documented and validated
- ✅ Output is concise and actionable
- ✅ Rate limits not encountered
- ✅ Results reported in structured format

