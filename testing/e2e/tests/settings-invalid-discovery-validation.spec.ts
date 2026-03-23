import { expect, test } from "@playwright/test";

test("@settings @us2 invalid discovery endpoint is rejected and not applied", async ({ page }) => {
  test.fail(
    true,
    "Invalid-value apply rejection wiring is pending; this test defines the required non-application behavior.",
  );

  await page.goto("http://127.0.0.1:7777/settings");
  await page.locator("#discoveryServerEditText").fill("host:99999");
  await page.getByRole("button", { name: /save/i }).click();

  await expect(page.locator("#validationMessage")).toBeVisible();
  await expect(page.locator("#validationMessage")).toContainText(/invalid|port|range/i);
  await expect(page.getByText(/settings saved|applied/i)).toHaveCount(0);
});
