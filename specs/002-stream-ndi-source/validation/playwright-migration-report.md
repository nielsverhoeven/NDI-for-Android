# Espresso to Playwright Migration Report

## Scope

Track migration status for end-to-end flows touched by feature 002-stream-ndi-source.

## Mapping

| Legacy Flow | Previous Coverage | Playwright Coverage | Status |
|---|---|---|---|
| Source list to output navigation | Espresso/manual | `testing/e2e/tests/us1-output-navigation.spec.ts` | SCaffolded |
| Start output discoverability | Espresso/manual | `testing/e2e/tests/us1-start-output.spec.ts` | SCaffolded |
| Output active metadata | N/A (new) | `testing/e2e/tests/us2-output-status.spec.ts` | SCaffolded |
| Stop output propagation | N/A (new) | `testing/e2e/tests/us2-stop-output.spec.ts` | SCaffolded |
| Recovery actions | N/A (new) | `testing/e2e/tests/us3-recovery-actions.spec.ts` | SCaffolded |
| Source loss propagation | N/A (new) | `testing/e2e/tests/us3-source-loss.spec.ts` | SCaffolded |
| Dual-emulator interop | Manual checklist | `testing/e2e/tests/interop-dual-emulator.spec.ts` | SCaffolded |

## Notes

- Scaffolds are intentionally marked with `test.fail(...)` until emulator automation wiring is completed.
