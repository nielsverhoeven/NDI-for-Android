import { expect, test } from "@playwright/test";
import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import {
  isResourceIdSuffixSelected,
  launchMainActivity,
  tapByResourceIdSuffix,
  waitForResourceIdSuffix,
  waitForResourceIdSuffixSelected,
} from "./support/android-ui-driver";

test.describe("Theme editor mode flows", () => {
  test("@theme @us1 light dark system mode switching", async () => {
    const context = getDualEmulatorContext();
    verifyDeviceReady(context.publisherSerial);
    verifyPackageInstalled(context.publisherSerial, context.packageName);

    launchMainActivity(context.publisherSerial, context.packageName);

    await tapByResourceIdSuffix(context.publisherSerial, "settingsFragment", 15_000);
    await tapByResourceIdSuffix(context.publisherSerial, "openThemeEditorButton", 15_000);

    await waitForResourceIdSuffix(context.publisherSerial, "themeModeLight", 10_000);
    await waitForResourceIdSuffix(context.publisherSerial, "themeModeDark", 10_000);
    await waitForResourceIdSuffix(context.publisherSerial, "themeModeSystem", 10_000);
    await waitForResourceIdSuffix(context.publisherSerial, "applyThemeButton", 10_000);

    // Apply DARK and verify persistence after reopening theme editor.
    await tapByResourceIdSuffix(context.publisherSerial, "themeModeDark", 10_000);
    await waitForResourceIdSuffixSelected(context.publisherSerial, "themeModeDark", 10_000);
    await tapByResourceIdSuffix(context.publisherSerial, "applyThemeButton", 10_000);

    await waitForResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle", 10_000);
    await tapByResourceIdSuffix(context.publisherSerial, "openThemeEditorButton", 10_000);
    await waitForResourceIdSuffixSelected(context.publisherSerial, "themeModeDark", 10_000);
    expect(isResourceIdSuffixSelected(context.publisherSerial, "themeModeDark")).toBeTruthy();

    // Switch to LIGHT, apply, and verify.
    await tapByResourceIdSuffix(context.publisherSerial, "themeModeLight", 10_000);
    await waitForResourceIdSuffixSelected(context.publisherSerial, "themeModeLight", 10_000);
    await tapByResourceIdSuffix(context.publisherSerial, "applyThemeButton", 10_000);

    await waitForResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle", 10_000);
    await tapByResourceIdSuffix(context.publisherSerial, "openThemeEditorButton", 10_000);
    await waitForResourceIdSuffixSelected(context.publisherSerial, "themeModeLight", 10_000);
    expect(isResourceIdSuffixSelected(context.publisherSerial, "themeModeLight")).toBeTruthy();

    // Reset to SYSTEM to keep deterministic post-test state.
    await tapByResourceIdSuffix(context.publisherSerial, "themeModeSystem", 10_000);
    await waitForResourceIdSuffixSelected(context.publisherSerial, "themeModeSystem", 10_000);
    await tapByResourceIdSuffix(context.publisherSerial, "applyThemeButton", 10_000);
    await waitForResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle", 10_000);
  });
});
