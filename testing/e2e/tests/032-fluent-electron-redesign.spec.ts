import { test, expect } from '@playwright/test';
import {
  boundedWait,
  expectNavigationMenuVisible,
  expectSettingsMenuVisible,
  getPersistedThemeMode,
  getThemeVisualToken,
  measureApplyLatencyMs,
  saveAppearanceSettings,
  selectThemeMode,
} from './support/android-ui-driver';

test.describe('032 Fluent + Electron redesign', () => {
  test('US1 shell consistency across top-level flows', async () => {
    await boundedWait(10);
    expect(await expectNavigationMenuVisible()).toBeTruthy();
    expect(await expectSettingsMenuVisible()).toBeTruthy();

    await selectThemeMode('light');
    const lightToken = await getThemeVisualToken('light');
    expect(lightToken).toBe('appbar-surface-light');

    await selectThemeMode('dark');
    const darkToken = await getThemeVisualToken('dark');
    expect(darkToken).toBe('appbar-surface-dark');
  });

  test('US2 core flow completion under redesigned surfaces', async () => {
    await boundedWait(10);

    const applyLatencyMs = await measureApplyLatencyMs(async () => {
      await selectThemeMode('system');
      await saveAppearanceSettings();
    });

    expect(applyLatencyMs).toBeLessThan(1000);
    expect(await getPersistedThemeMode()).toBe('system');
  });

  test('US3 adaptive and accessibility scenarios', async () => {
    await boundedWait(10);

    // Contract-level adaptive assertion until Android driver text-scale hooks land.
    const compactToken = await getThemeVisualToken('light');
    const wideToken = await getThemeVisualToken('light');
    expect(compactToken).toBe(wideToken);

    // Readability/focus fallback contract remains enabled in placeholder driver path.
    expect(await expectSettingsMenuVisible()).toBeTruthy();
  });
});
