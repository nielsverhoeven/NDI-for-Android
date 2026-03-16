import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "./tests",
  timeout: 180_000,
  expect: {
    timeout: 20_000,
  },
  fullyParallel: false,
  retries: 1,
  outputDir: "./test-results",
  reporter: [["list"], ["html", { open: "never" }]],
  use: {
    trace: "on-first-retry",
    video: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [
    {
      name: "android-dual-emulator",
      testMatch: /.*\.spec\.ts/,
      grep: /@dual-emulator|@us1|@us2|@us3/,
    },
  ],
});
