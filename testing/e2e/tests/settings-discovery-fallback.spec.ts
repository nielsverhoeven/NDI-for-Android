import { test } from "@playwright/test";
import { assertWithinThreshold, TIMING_THRESHOLDS } from "./support/timingAssertions";

import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import { launchDeepLink, editTextTailByResourceIdSuffix, tapText, waitForText } from "./support/android-ui-driver";

test.describe("Settings Discovery Fallback", () => {
  test("@settings @us2 shows fallback warning within 3s after unreachable endpoint is saved", async () => {
    const context = getDualEmulatorContext();
    verifyDeviceReady(context.publisherSerial);
    verifyPackageInstalled(context.publisherSerial, context.packageName);

    launchDeepLink(context.publisherSerial, context.packageName, "ndi://settings");
    editTextTailByResourceIdSuffix(context.publisherSerial, "discoveryServerEditText", "unreachable.ndi.invalid:5960");
    tapText(context.publisherSerial, "Save");

    await assertWithinThreshold(async () => {
      waitForText(context.publisherSerial, /falling back to default|fallback/i);
    }, TIMING_THRESHOLDS.fallbackWarning, "discovery fallback warning latency");

    tapText(context.publisherSerial, "Save");
    waitForText(context.publisherSerial, /fallback|unreachable|default/i);
  });
});