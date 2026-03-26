import { test } from "@playwright/test";
import { assertWithinThreshold, TIMING_THRESHOLDS } from "./support/timingAssertions";

import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import { launchDeepLink, editTextTailByResourceIdSuffix, tapText, waitForTextContaining } from "./support/android-ui-driver";

test.describe("Settings Discovery Fallback", () => {
  test("@settings @us2 shows fallback warning within 3s after unreachable endpoint is saved", async () => {
    const context = getDualEmulatorContext();
    verifyDeviceReady(context.publisherSerial);
    verifyPackageInstalled(context.publisherSerial, context.packageName);

    launchDeepLink(context.publisherSerial, context.packageName, "ndi://settings");
    await editTextTailByResourceIdSuffix(context.publisherSerial, "discoveryServerEditText", 100, "unreachable.ndi.invalid:5960");
    await tapText(context.publisherSerial, "Save");

    await assertWithinThreshold(async () => {
      await waitForTextContaining(context.publisherSerial, "fallback", 15_000);
    }, TIMING_THRESHOLDS.fallbackWarning, "discovery fallback warning latency");

    await tapText(context.publisherSerial, "Save");
    await waitForTextContaining(context.publisherSerial, "fallback", 15_000);
  });
});