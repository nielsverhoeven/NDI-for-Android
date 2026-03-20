import { expect, test } from "@playwright/test";
import {
  assertDeviceVersionSupported,
  buildSettingsDeepLink,
  computeSupportedVersionWindow,
  isMajorVersionSupported,
  type AndroidVersionInfo,
} from "./android-device-fixtures";

test("@us3 support window uses rolling latest-five majors", () => {
  const window = computeSupportedVersionWindow();

  expect(window.windowSize).toBe(5);
  expect(window.lowestSupportedMajor).toBe(window.highestSupportedMajor - 4);
});

test("@us3 isMajorVersionSupported accepts values inside window", () => {
  const window = computeSupportedVersionWindow();

  expect(isMajorVersionSupported(window.lowestSupportedMajor, window)).toBeTruthy();
  expect(isMajorVersionSupported(window.highestSupportedMajor, window)).toBeTruthy();
});

test("@us3 isMajorVersionSupported rejects values below window", () => {
  const window = computeSupportedVersionWindow();

  expect(isMajorVersionSupported(window.lowestSupportedMajor - 1, window)).toBeFalsy();
});

test("@us3 assertDeviceVersionSupported throws for unsupported major", () => {
  const window = computeSupportedVersionWindow();
  const unsupportedInfo: AndroidVersionInfo = {
    sdkInt: 29,
    majorVersion: window.lowestSupportedMajor - 1,
    release: "10",
    codename: "REL",
    incremental: "test",
  };

  expect(() =>
    assertDeviceVersionSupported("publisher", "emulator-5554", unsupportedInfo, window),
  ).toThrow(/Unsupported Android version/);
});

test("@settings @us2 buildSettingsDeepLink returns settings uri", () => {
  const deeplink = buildSettingsDeepLink("com.ndi.app.debug");

  expect(deeplink.packageName).toBe("com.ndi.app.debug");
  expect(deeplink.uri).toBe("ndi://settings");
});
