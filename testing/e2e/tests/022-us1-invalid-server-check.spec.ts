import { test, expect } from '@playwright/test';

test.describe('022 US1: Discovery server add — connection check failure', () => {
  test('adding an unreachable discovery server endpoint shows failure badge within 5 s', async ({ page }) => {
    test.skip(true, 'BLOCKED: ENVIRONMENT — requires connected emulator and unreachable test endpoint');
  });
});
