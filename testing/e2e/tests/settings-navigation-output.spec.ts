import { expect, test } from "@playwright/test";

import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import {
  getTextByResourceIdSuffix,
  launchDeepLink,
  launchMainActivity,
  selectSettingsBottomNav,
  tapByResourceIdSuffix,
  waitForAnyResourceIdSuffix,
  waitForResourceIdSuffixAbsent,
  waitForResourceIdSuffix,
  waitForResourceIdSuffixSelected,
  waitForResourceIdTextContaining,
  waitForText,
} from "./support/android-ui-driver";

test("STREAM_TO_SETTINGS: User can navigate from Stream to Settings via bottom nav", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchMainActivity(context.publisherSerial, context.packageName);
  await waitForText(context.publisherSerial, "Home", 15_000);
  await tapByResourceIdSuffix(context.publisherSerial, "streamFragment", 15_000);
  await waitForAnyResourceIdSuffix(context.publisherSerial, ["refreshButton", "sourceRecyclerView"], 15_000);

  await selectSettingsBottomNav(context.publisherSerial, 15_000);

  await waitForResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle", 15_000);
  expect(getTextByResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle")).toBe("Settings");
  await waitForResourceIdSuffixSelected(context.publisherSerial, "settingsFragment", 15_000);
});

test("@settings @us1 output -> settings -> back", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchDeepLink(context.publisherSerial, context.packageName, "ndi://output/source-a");
  await waitForResourceIdTextContaining(context.publisherSerial, "outputTitle", "source-a", 15_000);
  await selectSettingsBottomNav(context.publisherSerial, 15_000);
  expect(getTextByResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle")).toBe("Settings");
  await tapByResourceIdSuffix(context.publisherSerial, "streamFragment", 15_000);
  await waitForResourceIdTextContaining(context.publisherSerial, "outputTitle", "source-a", 15_000);
});

test("@settings @us3 output and settings surfaces have no top-right settings affordance", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchDeepLink(context.publisherSerial, context.packageName, "ndi://output/source-a");
  await waitForResourceIdTextContaining(context.publisherSerial, "outputTitle", "source-a", 15_000);
  await waitForResourceIdSuffixAbsent(context.publisherSerial, "action_settings", 2_000);

  await selectSettingsBottomNav(context.publisherSerial, 15_000);
  await waitForResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle", 15_000);
  await waitForResourceIdSuffixAbsent(context.publisherSerial, "action_settings", 2_000);
});
