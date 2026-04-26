# 032 US1 Regression

Date: 2026-04-27

## Commands

```powershell
npm --prefix testing/e2e run test:pr:primary
npm --prefix testing/e2e exec playwright test tests/032-fluent-electron-redesign.spec.ts
./gradlew.bat :feature:ndi-browser:presentation:testDebugUnitTest --tests "*SourceListUiStateTest*" --tests "*ViewerViewModelTest*" --tests "*OutputControlViewModelTest*" -x lint
```

## Outcome

- Playwright regression profile: PASS (40 passed)
- Feature 032 Playwright scenarios: PASS (3 passed)
- US1 targeted presentation unit tests: PASS

## Classification

Pass
