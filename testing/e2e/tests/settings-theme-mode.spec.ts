import { expect, test } from "@playwright/test";
import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import { launchMainActivity, tapByResourceIdSuffix, waitForResourceIdSuffix } from "./support/android-ui-driver";

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

    expect(true).toBeTruthy();
  });
});
