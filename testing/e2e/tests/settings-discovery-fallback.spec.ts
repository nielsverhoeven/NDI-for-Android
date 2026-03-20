import { test } from "@playwright/test";
import { assertWithinThreshold, TIMING_THRESHOLDS } from "./support/timingAssertions";

test.describe("Settings Discovery Fallback", () => {
  test("shows fallback warning within 3s after unreachable endpoint is saved", async ({ page }) => {
    test.fail(
      true,
      "Emulator fallback wiring is pending; this spec documents <=3s warning requirements.",
    );

    await page.goto("http://127.0.0.1:7777/settings");
    await page.locator("#discoveryServerEditText").fill("unreachable.ndi.invalid:5960");
    await page.getByRole("button", { name: /save/i }).click();

    await assertWithinThreshold(async () => {
      await page.getByText(/falling back to default|fallback/i).waitFor({ state: "visible" });
    }, TIMING_THRESHOLDS.fallbackWarning, "discovery fallback warning latency");
  });
});