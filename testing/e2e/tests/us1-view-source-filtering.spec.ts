import { expect, test } from "@playwright/test";

import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import {
  launchMainActivity,
  tapByResourceIdSuffix,
  waitForAnyResourceIdSuffix,
  waitForTextAbsent,
} from "./support/android-ui-driver";

test("@view @us1 source-list excludes local current-device option", async () => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  launchMainActivity(context.publisherSerial, context.packageName);
  await tapByResourceIdSuffix(context.publisherSerial, "streamFragment", 15_000);
  await waitForAnyResourceIdSuffix(context.publisherSerial, ["sourceRecyclerView", "emptyStateText"], 15_000);

  // Local self-source should not be presented as a selectable source option.
  await waitForTextAbsent(context.publisherSerial, "This Device", 2_000);
});
