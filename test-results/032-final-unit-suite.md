# 032 Final Unit Suite

Date: 2026-04-27

## Command

```powershell
./gradlew.bat :app:testDebugUnitTest :feature:ndi-browser:presentation:testDebugUnitTest :feature:ndi-browser:data:testDebugUnitTest -x lint
```

## Outcome

Status: FAILED (code)

## Failure Summary

- `:feature:ndi-browser:data:testDebugUnitTest`
- `NdiDiscoveryRepositoryContractTest.discoverSources_passesAllEndpointsAtOnceToTheBridge`
- `NdiDiscoveryRepositoryContractTest.discoverSources_logsDiscoveryInfoForConfiguredServers`

## Notes

Presentation and app module suites passed. Data module contains existing contract-test failures that must be resolved for full green.
