import { expect, test } from "@playwright/test";

test("@settings @us1 output -> settings -> back", async ({ page }) => {
  await page.goto("http://127.0.0.1:7777/output/source-a");
  await page.getByRole("button", { name: /settings|open settings/i }).click();
  await expect(page.getByText(/settings/i)).toBeVisible();
  await page.goBack();
  await expect(page).toHaveURL(/output/);
});
