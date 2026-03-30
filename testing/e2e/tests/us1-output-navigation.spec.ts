import { test, expect } from "@playwright/test";
import {
  OUTPUT_READY_TEXT_CANDIDATES,
  OUTPUT_START_TEXT_CANDIDATES,
} from "./support/android-ui-driver";

test("@us1 output entry exposes current start labels", async () => {
  expect(OUTPUT_START_TEXT_CANDIDATES).toContain("Share Screen");
  expect(OUTPUT_START_TEXT_CANDIDATES).toContain("Start Output");
  expect(OUTPUT_READY_TEXT_CANDIDATES).toContain("Status: Ready to share");
});
