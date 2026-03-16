import { test, expect } from "@playwright/test";

test("@dual-emulator publish discover play stop interop", async ({ page }) => {
  test.fail(true, "Phase 6 dual-emulator automation wiring pending");

  await page.goto("http://127.0.0.1:7777/source-list");
  await expect(page.getByRole("button", { name: "Start Output" }).first()).toBeVisible();
});
