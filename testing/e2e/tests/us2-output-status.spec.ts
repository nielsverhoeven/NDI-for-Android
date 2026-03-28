import { test, expect } from "@playwright/test";
import {
  OUTPUT_ACTIVE_TEXT_CANDIDATES,
  OUTPUT_INTERRUPTED_TEXT_CANDIDATES,
  OUTPUT_READY_TEXT_CANDIDATES,
  OUTPUT_STARTING_TEXT_CANDIDATES,
  OUTPUT_STOPPED_TEXT_CANDIDATES,
} from "./support/android-ui-driver";

test("@us2 output status candidates reflect current UI copy", async () => {
  expect(OUTPUT_READY_TEXT_CANDIDATES).toContain("Status: Ready to share");
  expect(OUTPUT_STARTING_TEXT_CANDIDATES).toContain("Status: Starting output");
  expect(OUTPUT_ACTIVE_TEXT_CANDIDATES).toContain("Status: Sharing live");
  expect(OUTPUT_STOPPED_TEXT_CANDIDATES).toContain("Status: Stopped");
  expect(OUTPUT_INTERRUPTED_TEXT_CANDIDATES).toContain("Status: Interrupted");
});
