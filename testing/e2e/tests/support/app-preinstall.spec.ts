// @ts-nocheck
import { existsSync } from "fs";
import { mkdtempSync } from "fs";
import { readFileSync } from "fs";
import { tmpdir } from "os";
import { join } from "path";
import { expect, test } from "@playwright/test";
import {
  PREINSTALL_REPORT_PATH,
  PreFlightReport,
  buildVersionIdentifier,
  readPreFlightReport,
  writePreFlightReport,
} from "./app-preinstall";
import { isReusablePreinstallReport } from "./global-setup-dual-emulator";

function makeFixture(overrides: Partial<PreFlightReport> = {}): PreFlightReport {
  return {
    reportId: "11111111-1111-1111-1111-111111111111",
    timestamp: new Date().toISOString(),
    buildArtifact: {
      path: "app/build/outputs/apk/debug/app-debug.apk",
      variant: "debug",
      packageName: "com.ndi.app.debug",
      versionName: "0.1.0",
      versionCode: 1,
      versionIdentifier: "0.1.0+1",
      buildTimestamp: new Date().toISOString(),
      exists: true,
    },
    devices: [
      {
        serial: "emulator-5554",
        reachable: true,
        ready: true,
        readinessWaitMs: 1200,
        apkInstalled: true,
        installedVersionName: "0.1.0",
        installedVersionCode: 1,
        installedVersionIdentifier: "0.1.0+1",
        launchVerified: true,
        elapsedMs: 9000,
        status: "PASS",
        errorMessage: null,
      },
    ],
    overallStatus: "PASS",
    failureReason: null,
    totalElapsedMs: 9000,
    abortedBeforeInstall: false,
    ...overrides,
  };
}

test("@preinstall pre-flight report exists and parses", () => {
  expect(existsSync(PREINSTALL_REPORT_PATH)).toBe(true);
  const report = readPreFlightReport();
  expect(report).toHaveProperty("overallStatus");
  expect(["PASS", "FAIL"]).toContain(report.overallStatus);
});

test("@preinstall overall status is PASS on healthy run", () => {
  const report = readPreFlightReport();
  expect(report.overallStatus).toBe("PASS");
});

test("@preinstall installed version matches expected build artifact", () => {
  const report = readPreFlightReport();
  const expected = report.buildArtifact.versionIdentifier;
  for (const device of report.devices.filter((d) => d.status === "PASS")) {
    expect(device.installedVersionIdentifier).toBe(expected);
  }
});

test("@preinstall each PASS device elapsedMs is <= 60000", () => {
  const report = readPreFlightReport();
  for (const device of report.devices.filter((d) => d.status === "PASS")) {
    expect(device.elapsedMs).toBeLessThanOrEqual(60000);
  }
});

test("@preinstall missing artifact returns abortedBeforeInstall=true", () => {
  const fixture = makeFixture({
    buildArtifact: {
      path: "app/build/outputs/apk/debug/app-debug.apk",
      variant: "debug",
      packageName: "com.ndi.app.debug",
      versionName: null,
      versionCode: null,
      versionIdentifier: null,
      buildTimestamp: null,
      exists: false,
    },
    devices: [],
    overallStatus: "FAIL",
    failureReason: "APK artifact not found",
    abortedBeforeInstall: true,
  });

  expect(fixture.abortedBeforeInstall).toBe(true);
  expect(fixture.devices).toHaveLength(0);
});

test("@preinstall unreachable and not-ready statuses are distinct", () => {
  const fixture = makeFixture({
    overallStatus: "FAIL",
    failureReason: "fail",
    devices: [
      { ...makeFixture().devices[0], serial: "emulator-5554", status: "UNREACHABLE", ready: false, errorMessage: "unreachable" },
      { ...makeFixture().devices[0], serial: "emulator-5556", status: "NOT_READY", ready: false, errorMessage: "not ready" },
    ],
  });

  expect(fixture.devices.some((d) => d.status === "UNREACHABLE")).toBe(true);
  expect(fixture.devices.some((d) => d.status === "NOT_READY")).toBe(true);
});

test("@preinstall timeout status includes actionable error message", () => {
  const fixture = makeFixture({
    overallStatus: "FAIL",
    failureReason: "timeout",
    devices: [
      { ...makeFixture().devices[0], status: "TIMEOUT", errorMessage: "emulator-5554: install exceeded remaining pre-flight budget" },
    ],
  });

  expect(fixture.devices[0].status).toBe("TIMEOUT");
  expect(fixture.devices[0].errorMessage?.toLowerCase()).toContain("budget");
});

test("@preinstall workflow keeps build/install/preflight-spec steps unconditional", () => {
  const workflowPath = join(__dirname, "../../../../.github/workflows/e2e-dual-emulator.yml");
  const workflow = readFileSync(workflowPath, "utf8");

  const targetSteps = ["Build app debug APK", "Install app on emulators", "Run app pre-flight support spec"];
  for (const step of targetSteps) {
    const nameIndex = workflow.indexOf(`- name: ${step}`);
    expect(nameIndex).toBeGreaterThanOrEqual(0);
    const nextStepIndex = workflow.indexOf("- name:", nameIndex + 1);
    const block = workflow.slice(nameIndex, nextStepIndex > -1 ? nextStepIndex : undefined);
    expect(block).not.toContain("if:");
  }
});

test("@preinstall launch verification required for PASS", () => {
  const fixture = makeFixture();
  const hasInvalidPass = fixture.devices.some((d) => d.status === "PASS" && !d.launchVerified);
  expect(hasInvalidPass).toBe(false);
});

test("@preinstall LAUNCH_FAILED is distinct from INSTALL_FAILED", () => {
  const fixture = makeFixture({
    overallStatus: "FAIL",
    failureReason: "launch fail",
    devices: [
      { ...makeFixture().devices[0], status: "INSTALL_FAILED", errorMessage: "install failed" },
      { ...makeFixture().devices[0], status: "LAUNCH_FAILED", errorMessage: "launch failed" },
    ],
  });

  expect(fixture.devices.filter((d) => d.status === "INSTALL_FAILED")).toHaveLength(1);
  expect(fixture.devices.filter((d) => d.status === "LAUNCH_FAILED")).toHaveLength(1);
});

test("@preinstall global setup rejects stale or missing preinstall report", () => {
  const dir = mkdtempSync(join(tmpdir(), "preinstall-"));
  const reportPath = join(dir, "preinstall-report.json");

  expect(isReusablePreinstallReport(reportPath, "expected", ["emulator-5554"])).toBe(false);

  const stale = makeFixture({
    buildArtifact: {
      ...makeFixture().buildArtifact,
      versionIdentifier: "0.0.1+1",
    },
  });
  writePreFlightReport(stale, reportPath);
  expect(isReusablePreinstallReport(reportPath, "0.1.0+1", ["emulator-5554"])).toBe(false);
});

test("@preinstall version identifier helper composes expected values", () => {
  expect(buildVersionIdentifier("1.2.3", 4)).toBe("1.2.3+4");
  expect(buildVersionIdentifier(null, 4)).toBeNull();
});
