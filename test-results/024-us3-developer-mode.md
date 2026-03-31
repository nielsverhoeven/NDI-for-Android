# US3 Capability Validation

## Capable target run

Running 1 test using 1 worker

  Γ£ô  1 tests\024-developer-mode-rebuild.spec.ts:4:5 ΓÇ║ US3 developer mode baseline contract (19ms)

  1 passed (2.2s)
{
  "status": "pass",
  "requiredProfile": true,
  "profile": "us3-only",
  "activeSuiteId": "rebuilt-024-baseline",
  "selectedScenarioIds": [
    "us3-developer-mode"
  ],
  "notApplicableScenarioIds": [],
  "missingSpecPaths": [],
  "gateDecision": "pass",
  "generatedAtUtc": "2026-03-31T18:40:37.1536029Z"
}

CAPABLE_EXIT_CODE=0

## Non-capable target run
{
  "status": "not-applicable",
  "requiredProfile": true,
  "profile": "us3-only",
  "activeSuiteId": "rebuilt-024-baseline",
  "selectedScenarioIds": [
    "us3-developer-mode"
  ],
  "notApplicableScenarioIds": [
    "us3-developer-mode"
  ],
  "missingSpecPaths": [],
  "gateDecision": "pass",
  "generatedAtUtc": "2026-03-31T18:40:38.0412048Z"
}

NON_CAPABLE_EXIT_CODE=0
