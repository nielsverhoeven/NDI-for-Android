# Phase 0 Research: Developer Log Configured IP Display

## Decision 1: Treat configured addresses as IPv4, IPv6, and hostnames
- Decision: Developer log output will display configured addresses when the value is a valid IPv4 literal, IPv6 literal, or hostname.
- Rationale: The clarified spec explicitly requires these three formats; this matches realistic NDI/discovery environments and avoids false "invalid" classifications.
- Alternatives considered:
  - IPv4-only validation: rejected because it excludes valid modern network deployments.
  - IPv4 + IPv6 only: rejected because hostname-based configurations are common in local/dev setups.

## Decision 2: Resolve displayed values from active runtime configuration at log emission time
- Decision: Log rendering will read the active configuration state at the moment the developer log event is generated, not from cached/redacted placeholders.
- Rationale: Prevents stale or misleading log values when settings are changed while viewer is active.
- Alternatives considered:
  - Snapshot configuration at screen-open: rejected due to stale values after configuration edits.
  - Continue redacted output: rejected because it fails the debugging use case.

## Decision 3: Keep change scoped to developer mode output on View screen
- Decision: Only developer-mode View screen logs are changed; non-developer mode behavior remains unchanged.
- Rationale: Matches requested behavior and minimizes risk of user-facing regressions.
- Alternatives considered:
  - Apply across all logs globally: rejected because scope and risk are higher without user requirement.

## Decision 4: Add Playwright validation for visual/log text behavior and run full e2e regression
- Decision: Add emulator Playwright tests for single-address, multi-address, and developer-mode-off paths, then run existing suite.
- Rationale: Constitution requires Playwright coverage and regression for visual behavior changes.
- Alternatives considered:
  - Unit tests only: rejected because user-visible behavior must be verified end-to-end.

## Decision 5: Classify blocked validation as environment blocker with preflight evidence
- Decision: Run prerequisite/device preflights before e2e and explicitly record blocked gates with unblocking command.
- Rationale: Constitution XII and repo workflow demand deterministic environment validation and blocker evidence.
- Alternatives considered:
  - Best-effort testing without preflight: rejected due to non-deterministic failures and weaker diagnostics.
