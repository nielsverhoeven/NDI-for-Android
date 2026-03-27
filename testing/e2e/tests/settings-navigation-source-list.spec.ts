import { expect, test } from "@playwright/test";

import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import {
  getTextByResourceIdSuffix,
  launchMainActivity,
  rotateToLandscape,
  rotateToPortrait,
  selectSettingsBottomNav,
  tapByResourceIdSuffix,
  waitForAnyResourceIdSuffix,
  waitForResourceIdSuffixAbsent,
  waitForResourceIdSuffix,
  waitForResourceIdSuffixSelected,
  waitForText,
} from "./support/android-ui-driver";

test("HOME_TO_SETTINGS: User can navigate from Home to Settings via bottom nav", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchMainActivity(context.publisherSerial, context.packageName);
  await waitForText(context.publisherSerial, "Home", 15_000);

  await selectSettingsBottomNav(context.publisherSerial, 15_000);

  await waitForResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle", 15_000);
  expect(getTextByResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle")).toBe("Settings");
  await waitForResourceIdSuffixSelected(context.publisherSerial, "settingsFragment", 15_000);
});

test("@settings @us1 source-list -> settings -> back", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchMainActivity(context.publisherSerial, context.packageName);
  await waitForText(context.publisherSerial, "Home", 15_000);
  await tapByResourceIdSuffix(context.publisherSerial, "streamFragment", 15_000);
  await waitForAnyResourceIdSuffix(context.publisherSerial, ["refreshButton", "sourceRecyclerView"], 15_000);
  await selectSettingsBottomNav(context.publisherSerial, 15_000);
  expect(getTextByResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle")).toBe("Settings");
  await tapByResourceIdSuffix(context.publisherSerial, "streamFragment", 15_000);
  await waitForAnyResourceIdSuffix(context.publisherSerial, ["refreshButton", "sourceRecyclerView"], 15_000);
});

test("@settings @us2 settings exits to home/stream/view with selected-state sync", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchMainActivity(context.publisherSerial, context.packageName);
  await waitForText(context.publisherSerial, "Home", 15_000);
  await selectSettingsBottomNav(context.publisherSerial, 15_000);
  await waitForResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle", 15_000);

  await tapByResourceIdSuffix(context.publisherSerial, "homeDashboardFragment", 15_000);
  await waitForText(context.publisherSerial, "Home", 15_000);
  await waitForResourceIdSuffixSelected(context.publisherSerial, "homeDashboardFragment", 15_000);

  await tapByResourceIdSuffix(context.publisherSerial, "streamFragment", 15_000);
  await waitForAnyResourceIdSuffix(context.publisherSerial, ["refreshButton", "sourceRecyclerView"], 15_000);
  await waitForResourceIdSuffixSelected(context.publisherSerial, "streamFragment", 15_000);

  await selectSettingsBottomNav(context.publisherSerial, 15_000);
  await waitForResourceIdSuffixSelected(context.publisherSerial, "settingsFragment", 15_000);

  await tapByResourceIdSuffix(context.publisherSerial, "viewFragment", 15_000);
  await waitForAnyResourceIdSuffix(context.publisherSerial, ["sourceRecyclerView", "viewTitle"], 15_000);
  await waitForResourceIdSuffixSelected(context.publisherSerial, "viewFragment", 15_000);
});

test("@settings @us2 rapid tab switching keeps destination sync", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchMainActivity(context.publisherSerial, context.packageName);
  await waitForText(context.publisherSerial, "Home", 15_000);

  await selectSettingsBottomNav(context.publisherSerial, 15_000);
  await tapByResourceIdSuffix(context.publisherSerial, "homeDashboardFragment", 15_000);
  await selectSettingsBottomNav(context.publisherSerial, 15_000);
  await tapByResourceIdSuffix(context.publisherSerial, "streamFragment", 15_000);
  await selectSettingsBottomNav(context.publisherSerial, 15_000);
  await tapByResourceIdSuffix(context.publisherSerial, "viewFragment", 15_000);
  await selectSettingsBottomNav(context.publisherSerial, 15_000);

  await waitForResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle", 15_000);
  await waitForResourceIdSuffixSelected(context.publisherSerial, "settingsFragment", 15_000);
});

test("@settings @us2 rotation in settings preserves state and does not crash", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchMainActivity(context.publisherSerial, context.packageName);
  await waitForText(context.publisherSerial, "Home", 15_000);
  await selectSettingsBottomNav(context.publisherSerial, 15_000);
  await waitForResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle", 15_000);

  await rotateToLandscape(context.publisherSerial);
  await waitForResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle", 15_000);
  await waitForResourceIdSuffixSelected(context.publisherSerial, "settingsFragment", 15_000);

  await rotateToPortrait(context.publisherSerial);
  await waitForResourceIdSuffix(context.publisherSerial, "settingsHeaderTitle", 15_000);
  await waitForResourceIdSuffixSelected(context.publisherSerial, "settingsFragment", 15_000);

  await tapByResourceIdSuffix(context.publisherSerial, "homeDashboardFragment", 15_000);
  await waitForText(context.publisherSerial, "Home", 15_000);
});

test("@settings @us3 source-list has no top-right settings affordance", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchMainActivity(context.publisherSerial, context.packageName);
  await waitForText(context.publisherSerial, "Home", 15_000);
  await tapByResourceIdSuffix(context.publisherSerial, "streamFragment", 15_000);
  await waitForAnyResourceIdSuffix(context.publisherSerial, ["refreshButton", "sourceRecyclerView"], 15_000);
  await waitForResourceIdSuffixAbsent(context.publisherSerial, "action_settings", 2_000);
});
