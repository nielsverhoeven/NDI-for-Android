import { test, expect } from "@playwright/test";
import {
  getDualEmulatorContext,
  verifyDeviceReady,
  verifyPackageInstalled,
} from "./support/android-device-fixtures";

test("propagates stop output state", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyDeviceReady(context.receiverSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);
  verifyPackageInstalled(context.receiverSerial, context.packageName);

  test.fail(true, "US2 Android-device stop propagation orchestration still pending fixture/device automation wiring");

  expect(context.publisherSerial).not.toEqual(context.receiverSerial);
});
