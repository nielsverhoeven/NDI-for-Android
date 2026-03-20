import { expect, test } from "@playwright/test";
import {
  assertAllowedStaticDelay,
  getSettingsEntryCandidates,
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

test("@settings @us1 source-list settings candidates prioritize settings affordance", () => {
  const candidates = getSettingsEntryCandidates("source-list");

  expect(candidates[0]).toBe("Settings");
  expect(candidates).toContain("Open settings");
});

test("@settings @us1 viewer settings candidates include playback-safe fallback", () => {
  const candidates = getSettingsEntryCandidates("viewer");

  expect(candidates).toContain("PLAYING");
});

test("@settings @us1 output settings candidates include output-state controls", () => {
  const candidates = getSettingsEntryCandidates("output");

  expect(candidates).toContain("Start Output");
  expect(candidates).toContain("Stop Output");
});
