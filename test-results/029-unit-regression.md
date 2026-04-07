# 029 Unit Regression

Date: 2026-04-07
Feature: 029-ndi-server-compatibility

## Command
```powershell
$env:JAVA_HOME='C:\Program Files\Android\Android Studio\jbr'
$env:ANDROID_HOME='C:\Users\Niels\AppData\Local\Android\Sdk'
$env:ANDROID_SDK_ROOT='C:\Users\Niels\AppData\Local\Android\Sdk'
.\gradlew.bat :core:model:test :feature:ndi-browser:data:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest --console=plain
```

## Result
- PASS: BUILD SUCCESSFUL.
- Core model unit task has no source tests in this module (`:core:model:test NO-SOURCE`).
- Data and presentation debug unit suites passed in current workspace state.
