# 029 Discovery Server Targets

## Scope Rule
- Include latest known-good baseline, failing venue version, and each additional older version the team can obtain for testing.

## Targets

| targetId | role | versionString | endpointHost | endpointPort | obtainableForTest | status | notes |
|---|---|---|---|---:|---|---|---|
| baseline-latest | baseline | UNKNOWN_PENDING_CAPTURE | TBD | 5959 | true | blocked | Endpoint and exact version not yet captured from known-good environment |
| venue-failing | venue | UNKNOWN_PENDING_CAPTURE | TBD | 5959 | true | blocked | Endpoint and exact version not yet captured from venue environment |

## Additional Older Versions
- Add one row per obtainable older version as available.

## Preflight Notes
- If a target cannot be reached or version cannot be determined during preflight, keep `status=blocked` and record unblock step in `test-results/029-compatibility-matrix.md`.

## Validation Readiness Delta (2026-04-07)
- Code-level compatibility behavior is validated by automated unit tests in `feature:ndi-browser:data` and `feature:ndi-browser:presentation`.
- Runtime version-matrix validation remains blocked until concrete endpoint hosts and server version strings are provided for each target.
