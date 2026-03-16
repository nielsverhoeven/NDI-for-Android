import { test, expect } from "@playwright/test";

test("renders active output metadata", async ({ page }) => {
  test.fail(true, "US2 emulator automation wiring pending");

  await page.goto("http://127.0.0.1:7777/output/camera-1");
  await expect(page.getByText("ACTIVE")).toBeVisible();
});
