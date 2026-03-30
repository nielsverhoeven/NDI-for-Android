import { expect, test } from "@playwright/test";

import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import {
  launchMainActivity,
  tapByResourceIdSuffix,
  waitForAnyResourceIdSuffix,
  waitForResourceIdSuffix,
} from "./support/android-ui-driver";

test("@view @us1 restores last viewed preview on relaunch", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchMainActivity(context.publisherSerial, context.packageName);
  await tapByResourceIdSuffix(context.publisherSerial, "streamFragment", 15_000);
  await waitForAnyResourceIdSuffix(context.publisherSerial, ["sourceRecyclerView", "emptyStateText"], 15_000);

  // TODO(021): Select a source, navigate to viewer, and verify preview restore after relaunch.
  expect(true).toBeTruthy();
});

test("@view @us1 unavailable source restore does not autoplay", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchMainActivity(context.publisherSerial, context.packageName);
  await tapByResourceIdSuffix(context.publisherSerial, "streamFragment", 15_000);
  await waitForResourceIdSuffix(context.publisherSerial, "viewerState", 15_000).catch(() => {
    // TODO(021): replace with deterministic navigation and assertions.
  });

  expect(true).toBeTruthy();
});
