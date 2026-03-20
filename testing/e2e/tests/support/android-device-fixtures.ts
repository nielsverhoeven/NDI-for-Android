import { execFileSync } from "node:child_process";

export type DualEmulatorContext = {
  publisherSerial: string;
  receiverSerial: string;
  packageName: string;
};

const DEFAULT_PACKAGE = "com.ndi.app.debug";

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

