import { test } from "@playwright/test";
import { assertWithinThreshold, TIMING_THRESHOLDS } from "./support/timingAssertions";

import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import { launchDeepLink, tapText, waitForTextContaining, getTextByResourceIdSuffix } from "./support/android-ui-driver";

test.describe("Settings Developer Overlay", () => {
  test("@settings developer overlay appears within 1s of toggle", async () => {
    const context = getDualEmulatorContext();
    verifyDeviceReady(context.publisherSerial);
    verifyPackageInstalled(context.publisherSerial, context.packageName);

    launchDeepLink(context.publisherSerial, context.packageName, "ndi://settings");
    await assertWithinThreshold(async () => {
      await tapText(context.publisherSerial, "Developer Mode");
      await tapText(context.publisherSerial, "Save");
      await waitForTextContaining(context.publisherSerial, "developer", 15_000);
    }, TIMING_THRESHOLDS.overlayToggle, "developer overlay show latency");
  });

  test("@settings overlay shows redacted log entries (no raw IPs)", async () => {
    const context = getDualEmulatorContext();
    verifyDeviceReady(context.publisherSerial);
    verifyPackageInstalled(context.publisherSerial, context.packageName);

    launchDeepLink(context.publisherSerial, context.packageName, "ndi://source-list");
    const overlayText = getTextByResourceIdSuffix(context.publisherSerial, "developerOverlayContainer");
    if (overlayText.match(/\b(?:\d{1,3}\.){3}\d{1,3}\b/) || overlayText.match(/(?:\[[0-9a-fA-F:]+\]|\b[0-9a-fA-F]{1,4}(?::[0-9a-fA-F]{0,4}){2,7}\b)/)) {
      throw new Error("Overlay contains unredacted IP addresses");
    }
  });

  test("@settings developer overlay disappears within 1s when toggled off", async () => {
    const context = getDualEmulatorContext();
    verifyDeviceReady(context.publisherSerial);
    verifyPackageInstalled(context.publisherSerial, context.packageName);

    launchDeepLink(context.publisherSerial, context.packageName, "ndi://settings");
    await assertWithinThreshold(async () => {
      await tapText(context.publisherSerial, "Developer Mode");
      await tapText(context.publisherSerial, "Save");
    }, TIMING_THRESHOLDS.overlayToggle, "developer overlay hide latency");
  });
});