import { test, expect } from "@playwright/test";
import {
  getDualEmulatorContext,
  verifyDeviceReady,
  verifyPackageInstalled,
} from "./support/android-device-fixtures";

test("publisher starts local screen-share output and receiver can discover stream", async () => {
  const context = getDualEmulatorContext();

  verifyDeviceReady(context.publisherSerial);
  verifyDeviceReady(context.receiverSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);
  verifyPackageInstalled(context.receiverSerial, context.packageName);

  test.fail(true, "US1 Android-device UI orchestration still pending fixture/device automation wiring");

  // Sanity assertion so the test stays structured while app-driving steps are implemented next.
  expect(context.publisherSerial).not.toEqual(context.receiverSerial);
});
