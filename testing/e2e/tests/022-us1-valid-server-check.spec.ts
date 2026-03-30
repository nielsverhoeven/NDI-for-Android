import { test, expect } from '@playwright/test';

test.describe('022 US1: Discovery server add — connection check', () => {
  test('adding a valid discovery server endpoint shows success badge within 5 s', async ({ page }) => {
    // TODO: navigate to discovery server settings and add a reachable server
    // Placeholder — will be implemented when emulator e2e environment is available
    test.skip(true, 'BLOCKED: ENVIRONMENT — requires connected emulator and reachable NDI discovery server');
  });
});
