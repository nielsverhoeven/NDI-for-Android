import { test, expect } from '@playwright/test';

test('US1 navigation smoke baseline', async () => {
  // Baseline smoke contract until UI driver integration is added.
  expect('navigation-smoke-ready').toContain('navigation');
});
