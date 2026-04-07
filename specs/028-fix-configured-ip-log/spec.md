# Feature Specification: Developer Log Configured IP Display

**Feature Branch**: `028-fix-configured-ip-log`  
**Created**: 2026-04-07  
**Status**: Draft  
**Input**: User description: "when developer mode is turned on, it shows [redacted ip] in the log on the view screen. This should display the actually configured IP adresses."

## Clarifications

### Session 2026-04-07

- Q: Which configured address formats are valid for developer log display? -> A: Treat IPv4, IPv6, and hostnames as valid configured addresses.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Show Real Configured IPs in Viewer Logs (Priority: P1)

As a developer using developer mode, I need the View screen log output to show the actual configured IP addresses so I can verify network configuration and diagnose connectivity issues.

**Why this priority**: The current redacted value removes key troubleshooting context and blocks practical debugging in developer mode.

**Independent Test**: Can be fully tested by enabling developer mode, configuring one or more IP addresses, opening the View screen, and confirming that each log entry displays the configured values.

**Acceptance Scenarios**:

1. **Given** developer mode is enabled and at least one IP address is configured, **When** the View screen emits developer log lines that include target IP information, **Then** the log shows the actual configured IP address value instead of a redacted placeholder.
2. **Given** developer mode is disabled, **When** the View screen is opened, **Then** developer log output containing configured IP addresses is not shown.

---

### User Story 2 - Keep Multi-Address Logs Accurate (Priority: P2)

As a developer, I need multiple configured addresses to appear correctly in logs so I can verify all configured endpoints and their ordering.

**Why this priority**: Multi-address environments are common during testing, and partial or reordered logs can cause incorrect diagnostics.

**Independent Test**: Can be tested by configuring multiple IP addresses and verifying the View screen log contains each configured address exactly once per corresponding log event.

**Acceptance Scenarios**:

1. **Given** developer mode is enabled and multiple IP addresses are configured, **When** a relevant developer log entry is written, **Then** the entry includes all configured addresses for that event in the same order as the active configuration.

---

### User Story 3 - Handle Missing or Invalid Configuration Safely (Priority: P3)

As a developer, I need clear fallback logging behavior when configured addresses are missing or invalid so logs remain actionable and do not expose misleading values.

**Why this priority**: Clear fallback behavior reduces debugging confusion and avoids false assumptions about active network configuration.

**Independent Test**: Can be tested by clearing address configuration or entering invalid entries and verifying the resulting developer log messaging.

**Acceptance Scenarios**:

1. **Given** developer mode is enabled and no valid IP addresses are configured, **When** a log event that normally includes configured IPs is emitted, **Then** the log shows a clear "not configured" style message and does not show a redacted placeholder.

---

### Visual Change Quality Gate *(mandatory when UI changes are present)*

- This feature changes user-visible log text on the View screen.
- End-to-end validation MUST include emulator-run Playwright coverage for:
  - Developer mode enabled with a single configured IP.
  - Developer mode enabled with multiple configured IPs.
  - Developer mode disabled (no developer IP log output shown).
- End-to-end validation MUST execute the existing Playwright suite and keep all tests passing.

### Test Environment & Preconditions *(mandatory)*

- Runtime dependencies:
  - Android SDK, required build tools, and accepted SDK licenses.
  - One connected Android target (physical device or emulator) for manual verification.
  - Existing Playwright Android test harness under testing/e2e.
- Preflight checks before validation:
  - Run scripts/verify-android-prereqs.ps1.
  - Run scripts/verify-e2e-dual-emulator-prereqs.ps1 for dual-emulator Playwright runs.
  - Run adb devices and confirm at least one authorized target.
- External blocker handling:
  - If device authorization or SDK prerequisites fail, record validation status as "Blocked - Environment" with command output and blocker cause.
  - Re-run preflight after unblocking and only then execute scenario/regression tests.

### Edge Cases

- Developer mode enabled but configured address list contains duplicates: logs should avoid duplicate output for the same event.
- Developer mode enabled but one configured entry is malformed: malformed entries should be excluded from rendered address output while valid IPv4, IPv6, and hostname entries continue to display.
- Configuration updates while View screen is already open: subsequent log events should reflect the latest active configuration without requiring app restart.
- Very long address lists (5+ addresses): logs should display up to 5 addresses and remain readable; additional addresses beyond 5 may be truncated with ellipsis to avoid misleading partial-address exposure. Validation must test single-address, multi-address (2-4), and extended list (5+) scenarios.

## Assumptions

- "Configured IP adresses" refers to the currently active discovery/server IP configuration used by the app at runtime.
- Displaying actual configured addresses is required only in developer mode log output on the View screen.
- No changes are required to production-only user messaging outside developer mode.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST replace redacted IP placeholders with the actual currently configured IP address values in View screen developer logs when developer mode is enabled.
- **FR-002**: System MUST source displayed IP values from the active runtime configuration used by the app at the time each log event is emitted.
- **FR-003**: System MUST suppress developer IP log output on the View screen when developer mode is disabled.
- **FR-004**: System MUST treat IPv4 literals, IPv6 literals, and hostnames as valid configured addresses for developer log display.
- **FR-005**: System MUST display all valid configured addresses for relevant log events when multiple addresses are configured.
- **FR-006**: System MUST exclude malformed address values from displayed configured-address output and indicate when no valid address is configured.
- **FR-007**: For this visual log text change, system MUST include emulator-run Playwright end-to-end coverage for the updated View screen logging behavior.
- **FR-008**: System MUST execute and keep passing all existing Playwright end-to-end tests after this change.
- **FR-009**: Validation runs MUST execute and record prerequisite preflight checks before end-to-end execution.
- **FR-010**: Validation reporting MUST classify each failed gate as either implementation failure or environment blocker, with reproduction details.

### Key Entities *(include if feature involves data)*

- **Developer Log Entry**: A timestamped View screen log message containing event context, mode context, and optionally configured IP address content.
- **Configured Address Set**: The active collection of valid configured addresses (IPv4, IPv6, and hostnames) used for network/discovery behavior at runtime.
- **Developer Mode State**: Boolean state determining whether developer-oriented log content is visible.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In 100% of tested developer-mode sessions with valid configuration, View screen logs show actual configured IP addresses instead of redacted placeholders.
- **SC-002**: In 100% of tested non-developer-mode sessions, developer IP log output is not shown.
- **SC-003**: In multi-address test scenarios, 100% of expected configured addresses appear in the corresponding log output for each validated event.
- **SC-004**: Post-change regression run completes with 0 failing existing Playwright tests and all newly added scenarios passing.
