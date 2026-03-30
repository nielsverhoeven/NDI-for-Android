import { test, expect } from "@playwright/test";
import {
  getDualEmulatorContext,
  verifyDeviceReady,
  verifyPackageInstalled,
  clearLogcat,
  assertPublisherShowsRecoveryActions,
} from "./support/android-device-fixtures";
import {
  measureSc003RecoveryPathExposed,
  summarizeMetrics,
} from "./support/metrics-fixtures";

test.describe("US3: Recovery actions after output interruption", () => {
  test("publisher shows recovery actions after source interruption", async () => {
    const context = getDualEmulatorContext();
    verifyDeviceReady(context.publisherSerial);
    verifyPackageInstalled(context.publisherSerial, context.packageName);

    // Clear logcat before test to avoid stale entries
    clearLogcat(context.publisherSerial);

    // NOTE: Full orchestration (start output -> simulate interruption -> verify UI)
    // requires ADB UI automation (UIAutomator2 or Espresso) wired in CI. This test
    // scaffolds the fixture/assertion path; the pending work is wiring the tap/launch
    // sequence via the Android adb shell input commands.
    test.fail(true, "US3 Android-device tap-automation wiring is pending (logcat assertions ready)");

    // When wired: assertPublisherShowsRecoveryActions(context.publisherSerial, 5000);
    const metric = measureSc003RecoveryPathExposed(false);
    const summary = summarizeMetrics([metric]);
    expect(summary.total).toBe(1);
  });
});
