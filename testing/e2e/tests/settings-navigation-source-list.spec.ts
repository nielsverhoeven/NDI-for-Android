import { test } from "@playwright/test";

import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import { launchMainActivity, tapText, waitForText, pressBack } from "./support/android-ui-driver";

test("@settings @us1 source-list -> settings -> back", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchMainActivity(context.publisherSerial, context.packageName);
  await waitForText(context.publisherSerial, "Available NDI Sources", 15_000);
  await tapText(context.publisherSerial, "Settings");
  await waitForText(context.publisherSerial, "Settings", 15_000);
  await pressBack(context.publisherSerial);
  await waitForText(context.publisherSerial, "Available NDI Sources", 15_000);
});
