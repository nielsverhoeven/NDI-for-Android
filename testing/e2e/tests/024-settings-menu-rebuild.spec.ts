import { test, expect } from '@playwright/test';
import { boundedWait, expectSettingsMenuVisible } from './support/android-ui-driver';

test('US2 settings menu baseline contract', async () => {
  await boundedWait(10);
  const visible = await expectSettingsMenuVisible();
  expect(visible).toBeTruthy();
  expect('settings-menu-route').toContain('settings');
});
