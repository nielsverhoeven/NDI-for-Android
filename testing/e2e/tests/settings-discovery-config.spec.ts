import { expect, test } from "@playwright/test";
import { assertWithinThreshold, TIMING_THRESHOLDS } from "./support/timingAssertions";

test.describe("Settings Discovery Configuration", () => {
  test("@settings @us2 saves and applies discovery server endpoint within 1s", async ({ page }) => {
    test.fail(
      true,
      "Emulator UI wiring is pending; this spec documents the expected <=1s apply behavior.",
    );

    await assertWithinThreshold(async () => {
      await page.goto("http://127.0.0.1:7777/settings");
      await page.getByRole("button", { name: /settings/i }).click();
      await page.locator("#discoveryServerEditText").fill("ndi-server.local");
      await page.getByRole("button", { name: /save/i }).click();
      await expect(page.getByText(/settings saved|applied/i)).toBeVisible();
      await expect(page.locator("#discoveryServerEditText")).toHaveValue(/ndi-server\.local/);
    }, TIMING_THRESHOLDS.discoveryApply, "discovery endpoint apply latency");
  });

  test("@settings @us2 validates invalid discovery input and shows inline error", async ({ page }) => {
    test.fail(
      true,
      "Emulator UI wiring is pending; this spec documents expected inline validation.",
    );

    await page.goto("http://127.0.0.1:7777/settings");
    await page.locator("#discoveryServerEditText").fill("::1");
    await page.getByRole("button", { name: /save/i }).click();

    await expect(page.locator("#validationMessage")).toBeVisible();
    await expect(page.locator("#validationMessage")).toContainText(/invalid|host|format/i);
  });
});