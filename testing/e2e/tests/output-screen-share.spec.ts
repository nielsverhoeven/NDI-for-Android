import { test, expect } from "@playwright/test";
import {
  computeVisibilityDurationMs,
  isWithinSeconds,
} from "./support/output-screen-share-helpers";
import {
  OUTPUT_ACTIVE_TEXT_CANDIDATES,
  OUTPUT_INTERRUPTED_TEXT_CANDIDATES,
  OUTPUT_READY_TEXT_CANDIDATES,
  OUTPUT_START_TEXT_CANDIDATES,
  OUTPUT_STOPPED_TEXT_CANDIDATES,
} from "./support/android-ui-driver";

test.describe("@us1 @us2 @us3 @us4 output-screen-share", () => {
  test("US1 timing helper calculates start-to-visible duration", async () => {
    const timingMs = computeVisibilityDurationMs({
      startEpochMs: 100,
      receiverVisibleEpochMs: 325,
    });
    expect(timingMs).toBe(225);
  });

  test("US1 and US2 output labels track current UI copy", async () => {
    expect(OUTPUT_START_TEXT_CANDIDATES).toEqual(expect.arrayContaining(["Share Screen", "Start Output"]));
    expect(OUTPUT_READY_TEXT_CANDIDATES).toContain("Status: Ready to share");
    expect(OUTPUT_ACTIVE_TEXT_CANDIDATES).toContain("Status: Sharing live");
    expect(OUTPUT_STOPPED_TEXT_CANDIDATES).toContain("Status: Stopped");
  });

  test("US3 visibility budget helper enforces the 10-second requirement", async () => {
    const visibilityMs = computeVisibilityDurationMs({
      startEpochMs: 1000,
      receiverVisibleEpochMs: 9500,
    });
    expect(isWithinSeconds(visibilityMs, 10)).toBe(true);
  });

  test("US4 interruption state remains a first-class surfaced outcome", async () => {
    expect(OUTPUT_INTERRUPTED_TEXT_CANDIDATES).toContain("Status: Interrupted");
    expect(OUTPUT_INTERRUPTED_TEXT_CANDIDATES).toContain("Interrupted");
  });
});
