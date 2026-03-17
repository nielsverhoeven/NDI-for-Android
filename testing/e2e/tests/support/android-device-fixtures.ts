import { execFileSync, spawnSync } from "node:child_process";

export type DualEmulatorContext = {
  publisherSerial: string;
  receiverSerial: string;
  packageName: string;
};

const DEFAULT_PACKAGE = "com.ndi.app.debug";
const STOP_PROPAGATION_TIMEOUT_MS = 3000;
const DISCOVERY_POLL_INTERVAL_MS = 500;

function runAdb(serial: string, args: string[]): string {
  return execFileSync("adb", ["-s", serial, ...args], {
    encoding: "utf-8",
    stdio: ["ignore", "pipe", "pipe"],
  }).trim();
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

