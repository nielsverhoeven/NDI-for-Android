import { execFileSync, spawnSync } from "node:child_process";

export type DualEmulatorContext = {
  publisherSerial: string;
  receiverSerial: string;
  packageName: string;
};

export type AndroidVersionInfo = {
  sdkInt: number;
  majorVersion: number;
  release: string;
  codename: string;
  incremental: string;
};

export type SupportedVersionWindow = {
  highestSupportedMajor: number;
  lowestSupportedMajor: number;
  windowSize: number;
};

const DEFAULT_PACKAGE = "com.ndi.app.debug";
const STOP_PROPAGATION_TIMEOUT_MS = 3000;
const DISCOVERY_POLL_INTERVAL_MS = 500;
const SUPPORTED_ANDROID_SDKS = [32, 33, 34, 35, 36] as const;
const SUPPORT_WINDOW_SIZE = 5;
const SDK_TO_MAJOR_VERSION: Record<number, number> = {
  32: 12,
  33: 13,
  34: 14,
  35: 15,
  36: 16,
};

function isTransientAdbFailure(error: unknown): boolean {
  const message = error instanceof Error ? error.message.toLowerCase() : String(error).toLowerCase();
  return (
    message.includes("can't find service") ||
    message.includes("device offline") ||
    message.includes("device not found") ||
    message.includes("adb server") ||
    message.includes("closed")
  );
}

function runAdb(serial: string, args: string[]): string {
  let lastError: unknown;

  for (let attempt = 0; attempt < 3; attempt++) {
    try {
      return execFileSync("adb", ["-s", serial, ...args], {
        encoding: "utf-8",
        stdio: ["ignore", "pipe", "pipe"],
      }).trim();
    } catch (error) {
      lastError = error;
      if (attempt >= 2 || !isTransientAdbFailure(error)) {
        throw error;
      }

      // Recover from transient emulator service hiccups before retrying.
      execFileSync("adb", ["start-server"], { stdio: ["ignore", "ignore", "ignore"] });
      execFileSync("adb", ["-s", serial, "wait-for-device"], { stdio: ["ignore", "ignore", "ignore"] });
    }
  }

  throw lastError instanceof Error ? lastError : new Error("Unknown ADB failure");
}

export function getDualEmulatorContext(): DualEmulatorContext {
  const publisherSerial = process.env.EMULATOR_A_SERIAL ?? "emulator-5554";
  const receiverSerial = process.env.EMULATOR_B_SERIAL ?? "emulator-5556";
  const packageName = process.env.APP_PACKAGE ?? DEFAULT_PACKAGE;

  if (publisherSerial === receiverSerial) {
    throw new Error("Publisher and receiver emulator serials must be different.");
  }

  return { publisherSerial, receiverSerial, packageName };
}

export function verifyDeviceReady(serial: string): void {
  const state = runAdb(serial, ["get-state"]);
  if (state !== "device") {
    throw new Error(`Device ${serial} is not ready. ADB state: ${state}`);
  }
}

export function verifyPackageInstalled(serial: string, packageName: string): void {
  const result = runAdb(serial, ["shell", "pm", "path", packageName]);
  if (!result.startsWith("package:")) {
    throw new Error(`Package ${packageName} is not installed on ${serial}.`);
  }
}

export function restartApplication(serial: string, packageName: string): void {
  runAdb(serial, ["shell", "am", "force-stop", packageName]);
  runAdb(serial, [
    "shell",
    "monkey",
    "-p",
    packageName,
    "-c",
    "android.intent.category.LAUNCHER",
    "1",
  ]);
}

export function buildSettingsDeepLink(packageName: string = DEFAULT_PACKAGE): { packageName: string; uri: string } {
  return {
    packageName,
    uri: "ndi://settings",
  };
}

export function getAndroidVersionInfo(serial: string): AndroidVersionInfo {
  const sdkRaw = runAdb(serial, ["shell", "getprop", "ro.build.version.sdk"]);
  const release = runAdb(serial, ["shell", "getprop", "ro.build.version.release"]);
  const codename = runAdb(serial, ["shell", "getprop", "ro.build.version.codename"]);
  const incremental = runAdb(serial, ["shell", "getprop", "ro.build.version.incremental"]);

  const sdkInt = Number.parseInt(sdkRaw.trim(), 10);
  if (!Number.isFinite(sdkInt)) {
    throw new Error(`Unable to parse Android SDK version for ${serial}: '${sdkRaw}'`);
  }

  const releaseValue = release.trim();
  const majorFromRelease = Number.parseInt(releaseValue.split(".")[0], 10);
  const majorVersion = Number.isFinite(majorFromRelease)
    ? majorFromRelease
    : (SDK_TO_MAJOR_VERSION[sdkInt] ?? sdkInt);

  return {
    sdkInt,
    majorVersion,
    release: releaseValue,
    codename: codename.trim(),
    incremental: incremental.trim(),
  };
}

export function computeSupportedVersionWindow(): SupportedVersionWindow {
  const knownMajors = Object.values(SDK_TO_MAJOR_VERSION);
  const highestSupportedMajor = Math.max(...knownMajors);
  return {
    highestSupportedMajor,
    lowestSupportedMajor: highestSupportedMajor - (SUPPORT_WINDOW_SIZE - 1),
    windowSize: SUPPORT_WINDOW_SIZE,
  };
}

export function isMajorVersionSupported(majorVersion: number, window: SupportedVersionWindow): boolean {
  return majorVersion >= window.lowestSupportedMajor && majorVersion <= window.highestSupportedMajor;
}

export function assertDeviceVersionSupported(
  role: "publisher" | "receiver",
  serial: string,
  info: AndroidVersionInfo,
  window: SupportedVersionWindow = computeSupportedVersionWindow(),
): void {
  if (isMajorVersionSupported(info.majorVersion, window)) {
    return;
  }

  throw new Error(
    `Unsupported Android version for ${role} (${serial}): ` +
      `SDK ${info.sdkInt}, major ${info.majorVersion}, release ${info.release}. ` +
      `Supported majors: ${window.lowestSupportedMajor}-${window.highestSupportedMajor} (latest ${window.windowSize}).`,
  );
}

export function verifySupportedAndroidVersion(
  serial: string,
  role: "publisher" | "receiver" = "publisher",
): AndroidVersionInfo {
  const info = getAndroidVersionInfo(serial);
  assertDeviceVersionSupported(role, serial, info);

  return info;
}

export function collectLogcat(serial: string, outputPath: string): void {
  execFileSync("adb", ["-s", serial, "logcat", "-d", "-v", "time"], {
    stdio: ["ignore", "pipe", "pipe"],
    encoding: "utf-8",
  });
  execFileSync("adb", ["-s", serial, "logcat", "-d", "-f", outputPath], {
    stdio: ["ignore", "ignore", "pipe"],
  });
}

/**
 * Polls logcat on the receiver device until the given log tag/message pattern appears
 * or the timeout expires. Used to verify stop-propagation events.
 */
export function waitForLogcatPattern(
  serial: string,
  pattern: string,
  timeoutMs: number = STOP_PROPAGATION_TIMEOUT_MS,
): boolean {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const logcat = runAdb(serial, ["logcat", "-d", "-t", "200"]);
    if (logcat.includes(pattern)) {
      return true;
    }
    // Busy-wait with a short spin; not ideal for production but acceptable for test automation
    const start = Date.now();
    while (Date.now() - start < DISCOVERY_POLL_INTERVAL_MS) {
      // spin
    }
  }
  return false;
}

/**
 * Clears the logcat buffer on the given device to avoid stale entries.
 */
export function clearLogcat(serial: string): void {
  runAdb(serial, ["logcat", "-c"]);
}

/**
 * Asserts that receiver playback has stopped by verifying a stop-propagation log event
 * within the given timeout window.
 */
export function assertReceiverPlaybackStopped(
  receiverSerial: string,
  timeoutMs: number = STOP_PROPAGATION_TIMEOUT_MS,
): void {
  const found = waitForLogcatPattern(receiverSerial, "playback_stopped", timeoutMs);
  if (!found) {
    const logcat = runAdb(receiverSerial, ["logcat", "-d", "-t", "100"]);
    throw new Error(
      `Receiver playback did not stop within ${timeoutMs}ms.\nLogcat tail:\n${logcat}`,
    );
  }
}

/**
 * Asserts that the receiver is in an interrupted/source-lost state.
 */
export function assertReceiverInterrupted(
  receiverSerial: string,
  timeoutMs: number = STOP_PROPAGATION_TIMEOUT_MS,
): void {
  const found =
    waitForLogcatPattern(receiverSerial, "playback_interrupted", timeoutMs) ||
    waitForLogcatPattern(receiverSerial, "source_lost", timeoutMs);
  if (!found) {
    throw new Error(`Receiver did not enter interrupted state within ${timeoutMs}ms.`);
  }
}

/**
 * Asserts that recovery actions are visible on the publisher device.
 * Checks for the "output_interrupted" logcat event as a proxy.
 */
export function assertPublisherShowsRecoveryActions(
  publisherSerial: string,
  timeoutMs: number = STOP_PROPAGATION_TIMEOUT_MS,
): void {
  const found = waitForLogcatPattern(publisherSerial, "output_interrupted", timeoutMs);
  if (!found) {
    throw new Error(`Publisher recovery actions not shown within ${timeoutMs}ms.`);
  }
}

