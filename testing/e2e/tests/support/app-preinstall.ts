// @ts-nocheck
import { mkdirSync, readFileSync, writeFileSync } from "fs";
import { dirname, resolve } from "path";

export const PREINSTALL_REPORT_PATH = resolve(__dirname, "../../artifacts/runtime/preinstall-report.json");

export type PreinstallStatus =
  | "PASS"
  | "NOT_READY"
  | "INSTALL_FAILED"
  | "VERSION_MISMATCH"
  | "LAUNCH_FAILED"
  | "TIMEOUT"
  | "UNREACHABLE";

export interface AppBuildArtifact {
  path: string;
  variant: "debug" | "release";
  packageName: string;
  versionName: string | null;
  versionCode: number | null;
  versionIdentifier: string | null;
  buildTimestamp: string | null;
  exists: boolean;
}

export interface EmulatorInstallRecord {
  serial: string;
  reachable: boolean;
  ready: boolean;
  readinessWaitMs: number;
  apkInstalled: boolean;
  installedVersionName: string | null;
  installedVersionCode: number | null;
  installedVersionIdentifier: string | null;
  launchVerified: boolean;
  elapsedMs: number;
  status: PreinstallStatus;
  errorMessage: string | null;
}

export interface PreFlightReport {
  reportId: string;
  timestamp: string;
  buildArtifact: AppBuildArtifact;
  devices: EmulatorInstallRecord[];
  overallStatus: "PASS" | "FAIL";
  failureReason: string | null;
  totalElapsedMs: number;
  abortedBeforeInstall: boolean;
}

export function readPreFlightReport(path: string = PREINSTALL_REPORT_PATH): PreFlightReport {
  return JSON.parse(readFileSync(path, "utf8")) as PreFlightReport;
}

export function writePreFlightReport(report: PreFlightReport, path: string = PREINSTALL_REPORT_PATH): void {
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, JSON.stringify(report, null, 2));
}

export function buildVersionIdentifier(versionName: string | null, versionCode: number | null): string | null {
  if (!versionName || versionCode == null) {
    return null;
  }
  return `${versionName}+${versionCode}`;
}
