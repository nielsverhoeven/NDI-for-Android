import { test } from "@playwright/test";
import { assertWithinThreshold, TIMING_THRESHOLDS } from "./support/timingAssertions";

import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import { launchDeepLink, editTextTailByResourceIdSuffix, tapText, waitForTextContaining } from "./support/android-ui-driver";

test.describe("Settings Discovery Configuration", () => {
  test("@settings @us2 saves and applies discovery server endpoint within 1s", async () => {
    const context = getDualEmulatorContext();
    verifyDeviceReady(context.publisherSerial);
    verifyPackageInstalled(context.publisherSerial, context.packageName);

    await assertWithinThreshold(async () => {
      launchDeepLink(context.publisherSerial, context.packageName, "ndi://settings");
      await tapText(context.publisherSerial, "Settings");
      await editTextTailByResourceIdSuffix(context.publisherSerial, "discoveryServerEditText", 100, "ndi-server.local");
      await tapText(context.publisherSerial, "Save");
      await waitForTextContaining(context.publisherSerial, "saved", 15_000);
    }, TIMING_THRESHOLDS.discoveryApply, "discovery endpoint apply latency");
  });

  test("@settings @us2 validates invalid discovery input and shows inline error", async () => {
    const context = getDualEmulatorContext();
    verifyDeviceReady(context.publisherSerial);
    verifyPackageInstalled(context.publisherSerial, context.packageName);

    launchDeepLink(context.publisherSerial, context.packageName, "ndi://settings");
    await editTextTailByResourceIdSuffix(context.publisherSerial, "discoveryServerEditText", 100, "::1");
    await tapText(context.publisherSerial, "Save");

    await waitForTextContaining(context.publisherSerial, "invalid", 15_000);
  });
});