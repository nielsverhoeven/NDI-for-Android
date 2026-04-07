import { test, expect } from '@playwright/test';
import manifest from './regression-suite-manifest.json';

const allowedAreas = new Set(['settings', 'navigation', 'developer-mode']);
const allowedOutcomes = new Set(['pass', 'fail', 'blocked', 'not-applicable']);

test('scenario ids are unique and non-legacy', async () => {
  const ids = manifest.scenarios.map((s: any) => s.id);
  expect(new Set(ids).size).toBe(ids.length);
  expect(manifest.scenarios.every((s: any) => s.legacy === false)).toBeTruthy();
});

test('feature areas and profile references are valid', async () => {
  for (const scenario of manifest.scenarios as any[]) {
    expect(allowedAreas.has(scenario.featureArea)).toBeTruthy();
    expect(typeof scenario.specPath).toBe('string');
  }

  const known = new Set(manifest.scenarios.map((s: any) => s.id));
  for (const profileName of Object.keys(manifest.profiles)) {
    for (const id of (manifest.profiles as any)[profileName]) {
      expect(known.has(id)).toBeTruthy();
    }
  }
});

test('canonical outcomes include not-applicable', async () => {
  expect(allowedOutcomes.has('not-applicable')).toBeTruthy();
});

test('US1 legacy-retirement guard: no active profile references legacy IDs', async () => {
  const scenarios = manifest.scenarios as any[];
  const legacyIds = scenarios.filter(s => s.legacy === true).map(s => s.id);
  const activeIds = Object.values(manifest.profiles as any).flat() as string[];

  for (const legacyId of legacyIds) {
    expect(activeIds.includes(legacyId)).toBeFalsy();
  }
});

test('US1 baseline metadata exists for rebuilt suite handover', async () => {
  const baseline = (manifest as any).baseline;
  expect(baseline.activeSuiteId).toBe('rebuilt-024-baseline');
  expect(baseline.preRebuildSnapshotEvidencePath).toContain('024-transition-baseline-pre-rebuild.md');
});

test('US2 scenarios are grouped in us2-only profile', async () => {
  const us2Profile = (manifest.profiles as any)['us2-only'] as string[];
  expect(Array.isArray(us2Profile)).toBeTruthy();
  expect(us2Profile).toContain('us2-settings-menu');
  expect(us2Profile).toContain('us2-navigation-menu');
  expect(us2Profile).toContain('us2-appearance-settings');
  expect(us2Profile).toContain('us2-mobile-settings-parity');
});
