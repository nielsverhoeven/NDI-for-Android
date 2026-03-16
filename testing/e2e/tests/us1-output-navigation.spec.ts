import { test, expect } from "@playwright/test";

test("navigates from source list to output control", async ({ page }) => {
  test.fail(true, "US1 emulator automation wiring pending");

  await page.goto("http://127.0.0.1:7777/source-list");
  await page.getByRole("button", { name: "Start Output" }).first().click();

  await expect(page).toHaveURL(/.*output.*/);
});
