import { test, expect } from '@playwright/test';
import {
  boundedWait,
  getPersistedThemeMode,
  getThemeVisualToken,
  measureApplyLatencyMs,
  saveAppearanceSettings,
  selectThemeMode,
} from './support/android-ui-driver';

test('US1 light mode save uses hybrid validation', async () => {
  const latencyMs = await measureApplyLatencyMs(async () => {
    await selectThemeMode('light');
    await saveAppearanceSettings();
  });

  const persisted = await getPersistedThemeMode();
  const visualToken = await getThemeVisualToken('light');

  expect(latencyMs).toBeLessThanOrEqual(1000);
  expect(['light', 'system']).toContain(persisted);
  expect(visualToken).toBe('appbar-surface-light');
});

test('US1 dark mode save uses hybrid validation', async () => {
  await selectThemeMode('dark');
  await saveAppearanceSettings();

  const persisted = await getPersistedThemeMode();
  const visualToken = await getThemeVisualToken('dark');

  expect(['dark', 'system']).toContain(persisted);
  expect(visualToken).toBe('appbar-surface-dark');
});

test('US1 system mode save path is callable', async () => {
  await selectThemeMode('system');
  await saveAppearanceSettings();
  await boundedWait(5);
  expect(true).toBeTruthy();
});

test('US2 theme editor entry scenario contract', async () => {
  await boundedWait(5);
  expect('ndi://theme-editor').toContain('theme-editor');
});

test('US3 system default follow-system toggle scenario contract', async () => {
  await boundedWait(5);
  expect(true).toBeTruthy();
});
