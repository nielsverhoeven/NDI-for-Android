import { test } from "@playwright/test";

import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import { launchDeepLink, editTextTailByResourceIdSuffix, tapText, waitForText } from "./support/android-ui-driver";

test("@settings @us2 invalid discovery endpoint is rejected and not applied", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchDeepLink(context.publisherSerial, context.packageName, "ndi://settings");
  await editTextTailByResourceIdSuffix(context.publisherSerial, "discoveryServerEditText", 100, "host:99999");
  await tapText(context.publisherSerial, "Save");
  await waitForText(context.publisherSerial, "Invalid server format", 15_000);
});
