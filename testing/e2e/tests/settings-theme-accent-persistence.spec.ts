import { expect, test } from "@playwright/test";
import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import {
  isResourceIdSuffixSelected,
  launchMainActivity,
  tapByResourceIdSuffix,
  waitForResourceIdSuffix,
  waitForResourceIdSuffixSelected,
} from "./support/android-ui-driver";

test.describe("Theme editor accent and persistence flows", () => {
  test("@theme @us2 accent palette selection", async () => {
    const context = getDualEmulatorContext();
    verifyDeviceReady(context.publisherSerial);
    verifyPackageInstalled(context.publisherSerial, context.packageName);

    launchMainActivity(context.publisherSerial, context.packageName);
    await tapByResourceIdSuffix(context.publisherSerial, "settingsFragment", 15_000);
    await tapByResourceIdSuffix(context.publisherSerial, "openThemeEditorButton", 15_000);

    await waitForResourceIdSuffix(context.publisherSerial, "accentBlue", 10_000);
    await waitForResourceIdSuffix(context.publisherSerial, "accentTeal", 10_000);
    await waitForResourceIdSuffix(context.publisherSerial, "accentGreen", 10_000);
    await waitForResourceIdSuffix(context.publisherSerial, "accentOrange", 10_000);
    await waitForResourceIdSuffix(context.publisherSerial, "accentRed", 10_000);
    await waitForResourceIdSuffix(context.publisherSerial, "accentPink", 10_000);
    await waitForResourceIdSuffix(context.publisherSerial, "applyThemeButton", 10_000);

    await tapByResourceIdSuffix(context.publisherSerial, "accentRed", 10_000);
    await waitForResourceIdSuffixSelected(context.publisherSerial, "accentRed", 10_000);
    await tapByResourceIdSuffix(context.publisherSerial, "applyThemeButton", 10_000);

    await waitForResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle", 10_000);
    await tapByResourceIdSuffix(context.publisherSerial, "openThemeEditorButton", 10_000);
    await waitForResourceIdSuffixSelected(context.publisherSerial, "accentRed", 10_000);
    expect(isResourceIdSuffixSelected(context.publisherSerial, "accentRed")).toBeTruthy();

    // Reset accent to teal to keep deterministic state for subsequent scenarios.
    await tapByResourceIdSuffix(context.publisherSerial, "accentTeal", 10_000);
    await waitForResourceIdSuffixSelected(context.publisherSerial, "accentTeal", 10_000);
    await tapByResourceIdSuffix(context.publisherSerial, "applyThemeButton", 10_000);
    await waitForResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle", 10_000);
  });

  test("@theme @us3 persistence after relaunch", async () => {
    const context = getDualEmulatorContext();
    verifyDeviceReady(context.publisherSerial);
    verifyPackageInstalled(context.publisherSerial, context.packageName);

    launchMainActivity(context.publisherSerial, context.packageName);
    await tapByResourceIdSuffix(context.publisherSerial, "settingsFragment", 15_000);
    await tapByResourceIdSuffix(context.publisherSerial, "openThemeEditorButton", 15_000);
    await tapByResourceIdSuffix(context.publisherSerial, "themeModeDark", 10_000);
    await tapByResourceIdSuffix(context.publisherSerial, "accentOrange", 10_000);
    await waitForResourceIdSuffix(context.publisherSerial, "applyThemeButton", 10_000);
    await tapByResourceIdSuffix(context.publisherSerial, "applyThemeButton", 10_000);
    await waitForResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle", 10_000);

    launchMainActivity(context.publisherSerial, context.packageName);
    await tapByResourceIdSuffix(context.publisherSerial, "settingsFragment", 15_000);
    await tapByResourceIdSuffix(context.publisherSerial, "openThemeEditorButton", 15_000);
    await waitForResourceIdSuffixSelected(context.publisherSerial, "themeModeDark", 10_000);
    await waitForResourceIdSuffixSelected(context.publisherSerial, "accentOrange", 10_000);
    expect(isResourceIdSuffixSelected(context.publisherSerial, "themeModeDark")).toBeTruthy();
    expect(isResourceIdSuffixSelected(context.publisherSerial, "accentOrange")).toBeTruthy();

    // Restore defaults for isolation.
    await tapByResourceIdSuffix(context.publisherSerial, "themeModeSystem", 10_000);
    await tapByResourceIdSuffix(context.publisherSerial, "accentTeal", 10_000);
    await tapByResourceIdSuffix(context.publisherSerial, "applyThemeButton", 10_000);
    await waitForResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle", 10_000);
  });
});
