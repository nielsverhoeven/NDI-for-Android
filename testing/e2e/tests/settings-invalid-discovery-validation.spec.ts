import { test } from "@playwright/test";

import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import { launchDeepLink, editTextTailByResourceIdSuffix, tapText, waitForText } from "./support/android-ui-driver";

test("@settings @us2 invalid discovery endpoint is rejected and not applied", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchDeepLink(context.publisherSerial, context.packageName, "ndi://settings");
  editTextTailByResourceIdSuffix(context.publisherSerial, "discoveryServerEditText", "host:99999");
  tapText(context.publisherSerial, "Save");
  waitForText(context.publisherSerial, "Invalid server format");
});
