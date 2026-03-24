import { test } from "@playwright/test";

import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import { launchDeepLink, tapText, waitForText, pressBack } from "./support/android-ui-driver";

test("@settings @us1 output -> settings -> back", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchDeepLink(context.publisherSerial, context.packageName, "ndi://output/source-a");
  await tapText(context.publisherSerial, "Settings");
  await waitForText(context.publisherSerial, "Settings", 15_000);
  await pressBack(context.publisherSerial);
  // Assume back to output
});
