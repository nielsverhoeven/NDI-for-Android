import { test, expect } from "@playwright/test";
import {
  getDualEmulatorContext,
  verifyDeviceReady,
  verifyPackageInstalled,
} from "./support/android-device-fixtures";

test.describe("020 US3: Quality settings menu", () => {
  test("viewer quality preset can be switched and restored", async () => {
    const context = getDualEmulatorContext();
    verifyDeviceReady(context.receiverSerial);
    verifyPackageInstalled(context.receiverSerial, context.packageName);

    test.fail(
      true,
      "Pending Android UI menu automation and latency assertions for quality preset application.",
    );

    expect(context.packageName).toContain("com.ndi");
  });
});
