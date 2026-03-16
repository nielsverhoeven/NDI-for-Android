import { test, expect } from "@playwright/test";

test("shows start output affordance in source list", async ({ page }) => {
  test.fail(true, "US1 emulator automation wiring pending");

  await page.goto("http://127.0.0.1:7777/source-list");

  await expect(page.getByRole("button", { name: "Start Output" }).first()).toBeVisible();
});
