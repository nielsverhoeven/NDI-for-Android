import { expect, test } from "@playwright/test";
import { assertWithinThreshold, TIMING_THRESHOLDS } from "./support/timingAssertions";

test.describe("Settings Developer Overlay", () => {
  test("developer overlay appears within 1s of toggle", async ({ page }) => {
    test.fail(
      true,
      "Emulator overlay wiring is pending; this spec documents the expected <=1s toggle-on behavior.",
    );

    await page.goto("http://127.0.0.1:7777/settings");
    await assertWithinThreshold(async () => {
      await page.getByRole("switch", { name: /developer mode/i }).check();
      await page.getByRole("button", { name: /save/i }).click();
      await page.locator("#developerOverlayContainer").waitFor({ state: "visible" });
    }, TIMING_THRESHOLDS.overlayToggle, "developer overlay show latency");
  });

  test("overlay shows redacted log entries (no raw IPs)", async ({ page }) => {
    test.fail(
      true,
      "Emulator overlay wiring is pending; this spec documents the expected redaction behavior.",
    );

    await page.goto("http://127.0.0.1:7777/source-list");
    const overlayText = await page.locator("#developerOverlayContainer").innerText();
    expect(overlayText).not.toMatch(/\b(?:\d{1,3}\.){3}\d{1,3}\b/);
    expect(overlayText).not.toMatch(/(?:\[[0-9a-fA-F:]+\]|\b[0-9a-fA-F]{1,4}(?::[0-9a-fA-F]{0,4}){2,7}\b)/);
  });

  test("developer overlay disappears within 1s when toggled off", async ({ page }) => {
    test.fail(
      true,
      "Emulator overlay wiring is pending; this spec documents the expected <=1s toggle-off behavior.",
    );

    await page.goto("http://127.0.0.1:7777/settings");
    await assertWithinThreshold(async () => {
      await page.getByRole("switch", { name: /developer mode/i }).uncheck();
      await page.getByRole("button", { name: /save/i }).click();
      await page.locator("#developerOverlayContainer").waitFor({ state: "hidden" });
    }, TIMING_THRESHOLDS.overlayToggle, "developer overlay hide latency");
  });
});