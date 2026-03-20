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
      name: "android-primary",
      testMatch: /.*\.spec\.ts/,
      grep: /@dual-emulator|@us1|@us2|@us3|@settings/,
    },
    {
      name: "android-matrix-api34",
      testMatch: /.*\.spec\.ts/,
      grep: /@dual-emulator|@us1|@us2|@us3|@settings/,
    },
    {
      name: "android-matrix-api35",
      testMatch: /.*\.spec\.ts/,
      grep: /@dual-emulator|@us1|@us2|@us3|@settings/,
    },
  ],
});
