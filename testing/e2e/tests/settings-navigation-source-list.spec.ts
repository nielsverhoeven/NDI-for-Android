import { test } from "@playwright/test";

import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import { launchMainActivity, tapText, waitForText, pressBack } from "./support/android-ui-driver";

test("@settings @us1 source-list -> settings -> back", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchMainActivity(context.publisherSerial, context.packageName);
  waitForText(context.publisherSerial, "Available NDI Sources");
  tapText(context.publisherSerial, "Settings");
  waitForText(context.publisherSerial, "Settings");
  pressBack(context.publisherSerial);
  waitForText(context.publisherSerial, "Available NDI Sources");
});
