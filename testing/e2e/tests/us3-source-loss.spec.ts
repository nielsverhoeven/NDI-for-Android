import { test, expect } from "@playwright/test";

test("propagates source loss interruption", async ({ page }) => {
  test.fail(true, "US3 emulator automation wiring pending");

  await page.goto("http://127.0.0.1:7777/output/camera-1");
  await expect(page.getByText("INTERRUPTED")).toBeVisible();
  await expect(page.getByRole("button", { name: "Retry Output" })).toBeVisible();
});
