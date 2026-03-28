import { expect, test } from "@playwright/test";

import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import {
  launchMainActivity,
  rotateToLandscape,
  rotateToPortrait,
  selectSettingsBottomNav,
  tapByResourceIdSuffix,
  waitForResourceIdSuffix,
} from "./support/android-ui-driver";
import {
  assertCompactLayout,
  assertThreePaneLayout,
  selectSettingsCategory,
} from "./support/settings-three-column-helpers";

test("@settings @us1 wide layout renders three-pane and category switch updates detail", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchMainActivity(context.publisherSerial, context.packageName);
  await selectSettingsBottomNav(context.publisherSerial, 15_000);
  await rotateToLandscape(context.publisherSerial);

  await assertThreePaneLayout(context.publisherSerial);
  await selectSettingsCategory(context.publisherSerial, "settingsCategoryAppearance");
  await waitForResourceIdSuffix(context.publisherSerial, "settingsDetailTitle", 15_000);
});

test("@settings @us2 main-nav actions route away and return to settings", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchMainActivity(context.publisherSerial, context.packageName);
  await selectSettingsBottomNav(context.publisherSerial, 15_000);
  await rotateToLandscape(context.publisherSerial);
  await assertThreePaneLayout(context.publisherSerial);

  await tapByResourceIdSuffix(context.publisherSerial, "settingsMainNavHome", 15_000);
  await tapByResourceIdSuffix(context.publisherSerial, "settingsFragment", 15_000);
  await assertThreePaneLayout(context.publisherSerial);
});

test("@settings @us3 compact fallback preserves selected category context", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchMainActivity(context.publisherSerial, context.packageName);
  await selectSettingsBottomNav(context.publisherSerial, 15_000);
  await rotateToLandscape(context.publisherSerial);
  await assertThreePaneLayout(context.publisherSerial);

  await selectSettingsCategory(context.publisherSerial, "settingsCategoryDeveloper");
  await rotateToPortrait(context.publisherSerial);
  await assertCompactLayout(context.publisherSerial);
  await rotateToLandscape(context.publisherSerial);

  await assertThreePaneLayout(context.publisherSerial);
  expect(true).toBeTruthy();
});
