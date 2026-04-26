# US3 Regression Validation

Date: 2026-04-07
Feature: 029-ndi-server-compatibility

## Command
```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\testing\e2e\scripts\run-matrix-e2e.ps1 -Profiles us3-only
```

## Result
- PASS: 2 passed.
- Profile validated:
  - US3 developer mode baseline contract remained green after compatibility diagnostics changes.

## Notes
- This run covers the current US3 profile contract checks.
- Full cross-profile matrix validation remains available via `run-discovery-compatibility-matrix.ps1`.
