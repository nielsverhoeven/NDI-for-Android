import { test, expect } from "@playwright/test";
import {
  getDualEmulatorContext,
  verifyDeviceReady,
  verifyPackageInstalled,
  clearLogcat,
} from "./support/android-device-fixtures";

test.describe("020 US2: Player auto-fit layout", () => {
  test("open Viewer, rotate orientation, and preserve >=90% fill with no distortion @us2 @dual-emulator", async () => {
    const context = getDualEmulatorContext();
    verifyDeviceReady(context.receiverSerial);
    verifyPackageInstalled(context.receiverSerial, context.packageName);
    clearLogcat(context.receiverSerial);

    const hasLiveNdiSource = process.env.NDI_SOURCE_AVAILABLE === "true";
    if (!hasLiveNdiSource) {
      test.skip(true, "Emulator-dependent: no live NDI source configured in this environment.");
    }

    test.fail(
      true,
      "Intentional TDD red test for T034: orientation automation and geometry probes pending implementation.",
    );

    await test.step("open Viewer, render 16:9 NDI stream", async () => {
      expect(context.packageName.length).toBeGreaterThan(0);
      expect(context.receiverSerial.length).toBeGreaterThan(0);
    });

    const portraitRender = { width: 1000, height: 562 };
    await test.step("measure rendered video box dimensions", async () => {
      expect(portraitRender.width).toBe(1000);
      expect(portraitRender.height).toBe(563);
    });

    const landscapeRender = { width: 1000, height: 562 };
    await test.step("rotate emulator to landscape", async () => {
      expect(landscapeRender.width).toBeGreaterThan(0);
      expect(landscapeRender.height).toBeGreaterThan(0);
    });

    await test.step("assert video re-renders with correct new dimensions", async () => {
      expect(landscapeRender.width).toBe(1067);
      expect(landscapeRender.height).toBe(600);
    });

    await test.step("assert fill utilization >= 90%", async () => {
      const landscapeBounds = { width: 1000, height: 600 };
      const utilization =
        (landscapeRender.width * landscapeRender.height) /
        (landscapeBounds.width * landscapeBounds.height);
      expect(utilization).toBeGreaterThanOrEqual(0.9);
      expect(utilization).toBeLessThanOrEqual(1.0);
    });

    await test.step("assert aspect ratio preserved (no distortion)", async () => {
      const targetAspect = 16 / 9;
      const measuredAspect = landscapeRender.width / landscapeRender.height;
      expect(Math.abs(measuredAspect - targetAspect)).toBeLessThanOrEqual(0.01);
    });
  });
});
