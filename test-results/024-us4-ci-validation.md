# US4 CI-Equivalent Validation

CI-equivalent execution sequence completed locally:
1. pwsh ./scripts/verify-android-prereqs.ps1 -CiMode -AllowMissingNdiSdk
2. pwsh ./testing/e2e/scripts/validate-command-contract.ps1 -Execute
3. pwsh ./testing/e2e/scripts/run-primary-pr-e2e.ps1 -Profile pr-primary

Observed outcomes:
- Android prereqs: PASS
- Command-contract execute mode: PASS
- Primary required profile gate: PASS
- Primary status artifact: present
- Triage summary artifact on green run: not present (expected)

Artifact checks:
- testing/e2e/artifacts/primary-status.json
  - status: pass
  - requiredProfile: true
  - gateDecision: pass
  - missingSpecPaths: []
- testing/e2e/artifacts/triage-summary.json
  - absent on green run (expected)

Conclusion:
US4 CI contract expectations are satisfied for required profile execution and artifact publishing behavior.
