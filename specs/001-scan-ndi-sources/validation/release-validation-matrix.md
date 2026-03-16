# Release Validation Matrix

Date: 2026-03-15

| Device Class | Example Target | Validation Scope | Required Commands | Result |
|---|---|---|---|---|
| Phone (API 24+) | Pixel-class handset profile | Discovery, selection, playback, interruption recovery | `./gradlew test`, `./gradlew connectedAndroidTest`, `./gradlew :app:assembleRelease` | Planned |
| Tablet (API 24+) | 10-inch tablet profile | Adaptive list/viewer layout and full flow parity | `./gradlew connectedAndroidTest`, `./gradlew :app:assembleRelease` | Planned |

Notes:
- R8/ProGuard release validation is mandatory for release readiness.
- Toolchain blocker `TOOLCHAIN-001` must be refreshed after validation runs.
