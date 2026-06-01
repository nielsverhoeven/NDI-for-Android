import { test, expect } from '@playwright/test';
import manifest from './regression-suite-manifest.json';

test('manifest contains rebuilt baseline metadata', async () => {
  const meta = (manifest as any).baseline;
  expect(meta).toBeDefined();
  expect(meta.activeSuiteId).toBe('rebuilt-024-baseline');
  expect(meta.preRebuildSnapshotEvidencePath).toContain('024-transition-baseline-pre-rebuild.md');
});

test('legacy scenarios are excluded from active profiles', async () => {
  const scenarios = (manifest as any).scenarios as any[];
  const legacyIds = scenarios.filter(s => s.legacy === true).map(s => s.id);
  const profileIds = Object.values((manifest as any).profiles).flat() as string[];

  for (const id of legacyIds) {
    expect(profileIds.includes(id)).toBeFalsy();
  }
});
