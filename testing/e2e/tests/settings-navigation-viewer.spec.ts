import { expect, test } from "@playwright/test";

import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import {
  closeSettingsFromSettingsSurface,
  getTextByResourceIdSuffix,
  launchDeepLink,
  openSettingsFromSurface,
  waitForResourceIdSuffix,
  waitForResourceIdTextContaining,
} from "./support/android-ui-driver";

test("@settings @us1 viewer -> settings -> back", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchDeepLink(context.publisherSerial, context.packageName, "ndi://viewer/source-a");
  await waitForResourceIdTextContaining(context.publisherSerial, "viewerTitle", "source-a", 15_000);
  await waitForResourceIdSuffix(context.publisherSerial, "action_settings", 15_000);
  await openSettingsFromSurface(context.publisherSerial, "viewer");
  await waitForResourceIdSuffix(context.publisherSerial, "action_settings", 15_000);
  expect(getTextByResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle")).toBe("Settings");
  await closeSettingsFromSettingsSurface(context.publisherSerial, 15_000);
  await waitForResourceIdTextContaining(context.publisherSerial, "viewerTitle", "source-a", 15_000);
});
