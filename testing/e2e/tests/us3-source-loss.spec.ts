import { test, expect } from "@playwright/test";
import {
  getDualEmulatorContext,
  verifyDeviceReady,
  verifyPackageInstalled,
  clearLogcat,
  assertReceiverInterrupted,
} from "./support/android-device-fixtures";

test.describe("US3: Receiver propagates source loss interruption", () => {
  test("receiver transitions to interrupted state when publisher source is lost", async () => {
    const context = getDualEmulatorContext();
    verifyDeviceReady(context.publisherSerial);
    verifyDeviceReady(context.receiverSerial);
    verifyPackageInstalled(context.publisherSerial, context.packageName);
    verifyPackageInstalled(context.receiverSerial, context.packageName);

    clearLogcat(context.receiverSerial);

    // NOTE: Full orchestration (start output on publisher -> kill publisher feed ->
    // verify INTERRUPTED on receiver) requires ADB UI automation to be wired.
    test.fail(true, "US3 source-loss emulator orchestration pending (fixture assertions ready)");

    // When wired: assertReceiverInterrupted(context.receiverSerial, 5000);
    expect(context.receiverSerial).not.toEqual(context.publisherSerial);
  });
});
