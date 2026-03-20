import { expect, test } from "@playwright/test";
import {
  assertAllowedStaticDelay,
  resolveConsentFlowVariant,
  STATIC_DELAY_MAX_MS,
} from "./android-ui-driver";

test("@us3 consent flow variant prefers full-screen share path", () => {
  const variant = resolveConsentFlowVariant(16);

  expect(variant.prefersFullScreenShare).toBeTruthy();
  expect(variant.selectionLabels[0]).toContain("Share entire screen");
});

test("@us3 assertAllowedStaticDelay accepts policy maximum", () => {
  expect(() => assertAllowedStaticDelay(STATIC_DELAY_MAX_MS)).not.toThrow();
});

test("@us3 assertAllowedStaticDelay rejects values above policy maximum", () => {
  expect(() => assertAllowedStaticDelay(STATIC_DELAY_MAX_MS + 1)).toThrow(/exceeds policy maximum/);
});
