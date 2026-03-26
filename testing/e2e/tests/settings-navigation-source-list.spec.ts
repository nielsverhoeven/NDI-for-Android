import { expect, test } from "@playwright/test";

import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import {
  closeSettingsFromSettingsSurface,
  getTextByResourceIdSuffix,
  launchMainActivity,
  openSettingsFromSurface,
  tapByResourceIdSuffix,
  waitForAnyResourceIdSuffix,
  waitForResourceIdSuffix,
  waitForText,
} from "./support/android-ui-driver";

test("@settings @us1 source-list -> settings -> back", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchMainActivity(context.publisherSerial, context.packageName);
  await waitForText(context.publisherSerial, "Home", 15_000);
  await tapByResourceIdSuffix(context.publisherSerial, "streamFragment", 15_000);
  await waitForAnyResourceIdSuffix(context.publisherSerial, ["refreshButton", "sourceRecyclerView"], 15_000);
  await waitForResourceIdSuffix(context.publisherSerial, "action_settings", 15_000);
  await openSettingsFromSurface(context.publisherSerial, "source-list");
  await waitForResourceIdSuffix(context.publisherSerial, "action_settings", 15_000);
  expect(getTextByResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle")).toBe("Settings");
  await closeSettingsFromSettingsSurface(context.publisherSerial, 15_000);
  await waitForAnyResourceIdSuffix(context.publisherSerial, ["refreshButton", "sourceRecyclerView"], 15_000);
});
