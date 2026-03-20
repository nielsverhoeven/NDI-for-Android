# Quickstart Validation Report

## Execution Summary

| Date | Engineer | Result | Notes |
|---|---|---|---|
| TBD | TBD | NOT_RUN | Pending setup |
| 2026-03-16 | Copilot | PARTIAL | Unit/data tests and debug assemble passed; dual-emulator and release validation still pending |

## Command Outcomes

| Command | Result | Notes |
|---|---|---|
| `pwsh ./scripts/verify-android-prereqs.ps1` | NOT_RUN | |
| `./gradlew test` | NOT_RUN | |
| `npm --prefix testing/e2e ci` | NOT_RUN | |
| `npm --prefix testing/e2e run test:dual-emulator` | NOT_RUN | |
| `./gradlew connectedAndroidTest` | NOT_RUN | API compatibility verification only |
| `./gradlew verifyReleaseHardening` | NOT_RUN | |
| `./gradlew :app:assembleRelease` | NOT_RUN | |
| `./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest :feature:ndi-browser:data:testDebugUnitTest :app:assembleDebug` | PASS | Executed with JAVA_HOME=`C:\Program Files\Java\jdk-21.0.10` |
