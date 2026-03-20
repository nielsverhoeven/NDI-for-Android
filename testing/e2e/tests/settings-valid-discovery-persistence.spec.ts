import { expect, test } from "@playwright/test";
import { assertWithinThreshold, TIMING_THRESHOLDS } from "./support/timingAssertions";

test("@settings @us2 valid discovery endpoint persists across relaunch", async ({ page }) => {
  test.fail(
    true,
    "Device relaunch persistence validation wiring is pending; this test defines required persisted-settings behavior.",
  );

  await page.goto("http://127.0.0.1:7777/settings");
  await page.locator("#discoveryServerEditText").fill("ndi-persist.local:5960");
  await page.getByRole("button", { name: /save/i }).click();

  await assertWithinThreshold(async () => {
    await page.reload();
    await expect(page.locator("#discoveryServerEditText")).toHaveValue(/ndi-persist\.local:5960/);
  }, TIMING_THRESHOLDS.persistenceRelaunch, "persisted value visible after relaunch");
});
