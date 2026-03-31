import { test, expect } from '@playwright/test';
import { boundedWait, expectNavigationMenuVisible } from './support/android-ui-driver';

test('US2 navigation menu baseline contract', async () => {
  await boundedWait(10);
  const visible = await expectNavigationMenuVisible();
  expect(visible).toBeTruthy();
  expect('navigation-menu-route').toContain('navigation');
});
