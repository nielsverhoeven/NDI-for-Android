import { test } from "@playwright/test";
import { assertWithinThreshold, TIMING_THRESHOLDS } from "./support/timingAssertions";

import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import { launchDeepLink, editTextTailByResourceIdSuffix, tapText, forceStopApp, getTextByResourceIdSuffix } from "./support/android-ui-driver";

test("@settings @us2 valid discovery endpoint persists across relaunch", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchDeepLink(context.publisherSerial, context.packageName, "ndi://settings");
  await editTextTailByResourceIdSuffix(context.publisherSerial, "discoveryServerEditText", 100, "ndi-persist.local:5960");
  await tapText(context.publisherSerial, "Save");

  await assertWithinThreshold(async () => {
    forceStopApp(context.publisherSerial, context.packageName);
    launchDeepLink(context.publisherSerial, context.packageName, "ndi://settings");
    const value = getTextByResourceIdSuffix(context.publisherSerial, "discoveryServerEditText");
    if (!value.includes("ndi-persist.local:5960")) {
      throw new Error(`Expected value not found: ${value}`);
    }
  }, TIMING_THRESHOLDS.persistenceRelaunch, "persisted value visible after relaunch");
});
