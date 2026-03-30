import { test, expect } from '@playwright/test';

test.describe('022 US3: Developer diagnostics overlay', () => {
  test('discovery diagnostics are visible in developer overlay when developer mode is enabled', async ({ page }) => {
    test.skip(true, 'BLOCKED: ENVIRONMENT — requires connected emulator with developer mode and NDI discovery server');
  });
});
