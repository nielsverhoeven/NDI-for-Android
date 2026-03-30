import { test, expect } from "@playwright/test";
import {
  OUTPUT_ACTIVE_TEXT_CANDIDATES,
  OUTPUT_INTERRUPTED_TEXT_CANDIDATES,
  OUTPUT_START_TEXT_CANDIDATES,
  resolveConsentFlowVariant,
} from "./support/android-ui-driver";

test("@us1 consent flow metadata supports modern Android prompts", async () => {
  const variant = resolveConsentFlowVariant(14);
  expect(variant.prefersFullScreenShare).toBe(true);
  expect(variant.selectionLabels).toContain("Share entire screen");
  expect(variant.confirmLabels).toContain("Share screen");
  expect(variant.confirmLabels).toContain("Start now");
});

test("@us1 output start path recognizes active and interrupted outcomes", async () => {
  expect(OUTPUT_START_TEXT_CANDIDATES).toContain("Share Screen");
  expect(OUTPUT_ACTIVE_TEXT_CANDIDATES).toContain("Status: Sharing live");
  expect(OUTPUT_INTERRUPTED_TEXT_CANDIDATES).toContain("Status: Interrupted");
});
