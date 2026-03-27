import { test, expect } from "@playwright/test";
import {
  OUTPUT_STOPPED_TEXT_CANDIDATES,
  OUTPUT_START_TEXT_CANDIDATES,
} from "./support/android-ui-driver";

test("@us2 stop output exposes stopped status and restart affordance", async () => {
  expect(OUTPUT_STOPPED_TEXT_CANDIDATES).toContain("Status: Stopped");
  expect(OUTPUT_START_TEXT_CANDIDATES).toContain("Share Screen");
  expect(OUTPUT_START_TEXT_CANDIDATES).toContain("Start Output");
});
