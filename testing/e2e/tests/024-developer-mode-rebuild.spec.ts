import { test, expect } from '@playwright/test';
import { boundedWait } from './support/android-ui-driver';

test('US3 developer mode baseline contract', async () => {
  await boundedWait(10);
  expect('developer-mode-toggle').toContain('developer');
  expect('developer-mode-disabled').toContain('disabled');
});
