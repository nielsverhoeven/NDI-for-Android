import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  reporter: 'list',
  use: {
    trace: 'on-first-retry'
  },
  projects: [
    {
      name: 'android-primary',
      use: {
        trace: 'on-first-retry'
      }
    },
    {
      name: 'android-secondary',
      use: {
        trace: 'on-first-retry'
      }
    }
  ]
});
