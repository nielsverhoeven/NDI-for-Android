import { execFileSync } from "node:child_process";
import { writeFileSync } from "node:fs";

const MAIN_ACTIVITY = "com.ndi.app.MainActivity";

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

function runAdbRaw(serial: string, args: string[]): string {
  let lastError: unknown;

  for (let attempt = 0; attempt < 3; attempt++) {
    try {
      return execFileSync("adb", ["-s", serial, ...args], {
        encoding: "utf-8",
        stdio: ["ignore", "pipe", "pipe"],
        maxBuffer: 16 * 1024 * 1024,
      });
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

function runAdb(serial: string, args: string[]): string {
  return runAdbRaw(serial, args).trim();
}

export type UiNode = {
  text: string;
  resourceId: string;
  bounds: string;
};

export type RectBounds = {
  x1: number;
  y1: number;
  x2: number;
  y2: number;
};

function parseNodes(xml: string): UiNode[] {
  const nodes: UiNode[] = [];
  const nodeMatches = xml.match(/<node\b[^>]*>/g) ?? [];

  for (const match of nodeMatches) {
    const text = /\btext="([^"]*)"/.exec(match)?.[1] ?? "";
    const resourceId = /\bresource-id="([^"]*)"/.exec(match)?.[1] ?? "";
    const bounds = /\bbounds="([^"]*)"/.exec(match)?.[1] ?? "";
    nodes.push({ text, resourceId, bounds });
  }

  return nodes;
}

function boundsCenter(bounds: string): { x: number; y: number } {
  const result = /^\[(\d+),(\d+)\]\[(\d+),(\d+)\]$/.exec(bounds);
  if (!result) {
    throw new Error(`Unable to parse node bounds: ${bounds}`);
  }

  const x1 = Number.parseInt(result[1], 10);
  const y1 = Number.parseInt(result[2], 10);
  const x2 = Number.parseInt(result[3], 10);
  const y2 = Number.parseInt(result[4], 10);
  return {
    x: Math.floor((x1 + x2) / 2),
    y: Math.floor((y1 + y2) / 2),
  };
}

function parseBounds(bounds: string): RectBounds {
  const result = /^\[(\d+),(\d+)\]\[(\d+),(\d+)\]$/.exec(bounds);
  if (!result) {
    throw new Error(`Unable to parse node bounds: ${bounds}`);
  }

  return {
    x1: Number.parseInt(result[1], 10),
    y1: Number.parseInt(result[2], 10),
    x2: Number.parseInt(result[3], 10),
    y2: Number.parseInt(result[4], 10),
  };
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export function clearLogcat(serial: string): void {
  runAdb(serial, ["logcat", "-c"]);
}

export function forceStopApp(serial: string, packageName: string): void {
  runAdb(serial, ["shell", "am", "force-stop", packageName]);
}

export function launchMainActivity(serial: string, packageName: string): void {
  runAdb(serial, [
    "shell",
    "am",
    "start",
    "-W",
    "-n",
    `${packageName}/${MAIN_ACTIVITY}`,
  ]);
}

export function launchDeepLink(serial: string, packageName: string, uri: string): void {
  runAdb(serial, [
    "shell",
    "am",
    "start",
    "-W",
    "-n",
    `${packageName}/${MAIN_ACTIVITY}`,
    "-a",
    "android.intent.action.VIEW",
    "-d",
    uri,
  ]);
}

export function captureScreenshot(serial: string, destinationPath: string): void {
  const screenshotBytes = execFileSync("adb", ["-s", serial, "exec-out", "screencap", "-p"], {
    stdio: ["ignore", "pipe", "pipe"],
    encoding: null,
    maxBuffer: 16 * 1024 * 1024,
  });
  writeFileSync(destinationPath, screenshotBytes);
}

export function dumpUi(serial: string): UiNode[] {
  runAdb(serial, ["shell", "uiautomator", "dump", "/sdcard/window_dump.xml"]);
  const xml = runAdb(serial, ["exec-out", "cat", "/sdcard/window_dump.xml"]);
  return parseNodes(xml);
}

export function getBoundsByResourceIdSuffix(serial: string, resourceIdSuffix: string): RectBounds {
  const nodes = dumpUi(serial);
  const match = nodes.find((node) => node.resourceId.endsWith(resourceIdSuffix));
  if (!match || !match.bounds) {
    throw new Error(`Could not find UI node with resource id suffix '${resourceIdSuffix}' on ${serial}`);
  }

  return parseBounds(match.bounds);
}

export function getTextByResourceIdSuffix(serial: string, resourceIdSuffix: string): string {
  const nodes = dumpUi(serial);
  const match = nodes.find((node) => node.resourceId.endsWith(resourceIdSuffix));
  if (!match) {
    throw new Error(`Could not find UI node with resource id suffix '${resourceIdSuffix}' on ${serial}`);
  }

  return match.text.trim();
}

export function writeUiSnapshot(serial: string, destinationPath: string): void {
  const nodes = dumpUi(serial);
  const lines = nodes
    .filter((node) => node.text.trim().length > 0)
    .map((node) => `${node.text}\t${node.resourceId}\t${node.bounds}`);
  writeFileSync(destinationPath, lines.join("\n"), { encoding: "utf-8" });
}

export function writeLogcatSnapshot(serial: string, destinationPath: string, tailLines = 500): void {
  const raw = runAdbRaw(serial, ["logcat", "-d", "-v", "time"]);
  const lines = raw.split(/\r?\n/);
  const snapshot = lines.slice(Math.max(0, lines.length - tailLines)).join("\n");
  writeFileSync(destinationPath, snapshot, { encoding: "utf-8" });
}

function findFirstByText(nodes: UiNode[], text: string): UiNode | undefined {
  return nodes.find((node) => node.text.trim() === text);
}

export async function waitForText(serial: string, text: string, timeoutMs: number): Promise<void> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const nodes = dumpUi(serial);
    if (findFirstByText(nodes, text)) {
      return;
    }
    await delay(300);
  }
  throw new Error(`Timed out waiting for text '${text}' on ${serial}`);
}

export async function tapText(serial: string, text: string, timeoutMs = 15_000): Promise<void> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const nodes = dumpUi(serial);
    const node = findFirstByText(nodes, text);
    if (node && node.bounds) {
      const { x, y } = boundsCenter(node.bounds);
      runAdb(serial, ["shell", "input", "tap", `${x}`, `${y}`]);
      await delay(150);
      return;
    }
    await delay(250);
  }

  throw new Error(`Timed out tapping text '${text}' on ${serial}`);
}

export async function tapFirstAvailableText(serial: string, candidates: string[], timeoutMs = 10_000): Promise<string> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const nodes = dumpUi(serial);
    for (const text of candidates) {
      const node = findFirstByText(nodes, text);
      if (node && node.bounds) {
        const { x, y } = boundsCenter(node.bounds);
        runAdb(serial, ["shell", "input", "tap", `${x}`, `${y}`]);
        await delay(150);
        return text;
      }
    }
    await delay(200);
  }

  throw new Error(`Timed out finding any candidate text on ${serial}: ${candidates.join(", ")}`);
}

export async function waitForTextContaining(serial: string, fragment: string, timeoutMs: number): Promise<string> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const nodes = dumpUi(serial);
    const match = nodes.find((node) => node.text.includes(fragment));
    if (match) {
      return match.text;
    }
    await delay(300);
  }
  throw new Error(`Timed out waiting for text containing '${fragment}' on ${serial}`);
}

export async function tapTextContaining(serial: string, fragment: string, timeoutMs = 15_000): Promise<string> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const nodes = dumpUi(serial);
    const match = nodes.find((node) => node.text.includes(fragment) && node.bounds);
    if (match) {
      const { x, y } = boundsCenter(match.bounds);
      runAdb(serial, ["shell", "input", "tap", `${x}`, `${y}`]);
      await delay(150);
      return match.text;
    }
    await delay(250);
  }
  throw new Error(`Timed out tapping text containing '${fragment}' on ${serial}`);
}

export async function replaceTextByResourceIdSuffix(
  serial: string,
  resourceIdSuffix: string,
  value: string,
  timeoutMs = 15_000,
): Promise<void> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const nodes = dumpUi(serial);
    const match = nodes.find((node) => node.resourceId.endsWith(resourceIdSuffix) && node.bounds);
    if (match) {
      const { x, y } = boundsCenter(match.bounds);
      runAdb(serial, ["shell", "input", "tap", `${x}`, `${y}`]);
      await delay(100);
      runAdb(serial, ["shell", "input", "keyevent", "123"]); // move cursor to end
      for (let i = 0; i < 96; i++) {
        runAdb(serial, ["shell", "input", "keyevent", "67"]); // DEL
      }

      const encoded = value.replace(/ /g, "%s");
      runAdb(serial, ["shell", "input", "text", encoded]);
      await delay(150);
      return;
    }
    await delay(200);
  }

  throw new Error(`Timed out replacing text for resource id suffix '${resourceIdSuffix}' on ${serial}`);
}

export async function waitForTextAbsent(serial: string, text: string, timeoutMs: number): Promise<void> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const nodes = dumpUi(serial);
    const found = nodes.some((node) => node.text.trim() === text);
    if (!found) {
      return;
    }
    await delay(300);
  }

  throw new Error(`Timed out waiting for text '${text}' to disappear on ${serial}`);
}

export async function editTextTailByResourceIdSuffix(
  serial: string,
  resourceIdSuffix: string,
  deleteCharsFromEnd: number,
  appendText: string,
  timeoutMs = 15_000,
): Promise<void> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const nodes = dumpUi(serial);
    const match = nodes.find((node) => node.resourceId.endsWith(resourceIdSuffix) && node.bounds);
    if (match) {
      const { x, y } = boundsCenter(match.bounds);
      runAdb(serial, ["shell", "input", "tap", `${x}`, `${y}`]);
      await delay(100);
      runAdb(serial, ["shell", "input", "keyevent", "123"]); // end of line
      for (let i = 0; i < deleteCharsFromEnd; i++) {
        runAdb(serial, ["shell", "input", "keyevent", "67"]);
      }
      if (appendText.length > 0) {
        runAdb(serial, ["shell", "input", "text", appendText.replace(/ /g, "%s")]);
      }
      await delay(150);
      return;
    }
    await delay(150);
  }

  throw new Error(`Timed out editing text tail for resource id suffix '${resourceIdSuffix}' on ${serial}`);
}

export async function pressBack(serial: string): Promise<void> {
  runAdb(serial, ["shell", "input", "keyevent", "4"]);
  await delay(150);
}

export type ConsentFlowVariant = {
  majorVersion: number;
  selectionLabels: string[];
  confirmLabels: string[];
  prefersFullScreenShare: boolean;
};

export function resolveConsentFlowVariant(majorVersion: number): ConsentFlowVariant {
  const selectionLabels = [
    "Share entire screen",
    "Entire screen",
    "Share one app",
    "Screen",
  ];

  const confirmLabels = majorVersion >= 14
    ? ["Share screen", "Next", "Start now", "Share", "Allow", "Start broadcasting"]
    : ["Share screen", "Next", "Start now", "Allow", "OK", "Start broadcasting"];

  return {
    majorVersion,
    selectionLabels,
    confirmLabels,
    prefersFullScreenShare: true,
  };
}

export async function completeScreenShareConsent(
  serial: string,
  majorVersion: number,
  timeoutMs = 15_000,
  options?: { allowSkipWhenNoDialog?: boolean },
): Promise<{ selectionLabel: string; confirmLabel: string | null }> {
  const variant = resolveConsentFlowVariant(majorVersion);
  const allowSkipWhenNoDialog = options?.allowSkipWhenNoDialog ?? true;

  // Some Android builds show a two-step flow (selection, then confirmation and app pick).
  // Poll within the full timeout and progress the flow until the dialog disappears.
  const start = Date.now();
  const skipGraceMs = Math.min(1_500, Math.floor(timeoutMs / 3));
  let selectionLabel: string | null = null;
  let confirmLabel: string | null = null;
  let selectionTappedAt = 0;
  let lastAppPickerSwipeAt = 0;

  while (Date.now() - start < timeoutMs) {
    const nodes = dumpUi(serial);
    const visibleTexts = new Set(nodes.map((node) => node.text.trim()).filter((text) => text.length > 0));
    const dialogVisible = Array.from(visibleTexts).some((text) =>
      text.includes("Share your screen") || text.includes("share your screen")
    );
    const outputUiVisible = ["Start Output", "READY", "STARTING", "ACTIVE", "STOPPED", "INTERRUPTED"]
      .some((text) => visibleTexts.has(text));
    const selectionUiVisible = variant.selectionLabels.some((text) => visibleTexts.has(text));
    const confirmUiVisible = variant.confirmLabels.some((text) => visibleTexts.has(text));
    const consentUiVisible = dialogVisible || selectionUiVisible || confirmUiVisible || visibleTexts.has("NDI for Android");

    // If the app output UI is already visible and no dialog is present, there is nothing to consent.
    if (allowSkipWhenNoDialog && !dialogVisible && outputUiVisible && !selectionLabel && !confirmLabel) {
      if (Date.now() - start >= skipGraceMs) {
        return { selectionLabel: "skipped (dialog not found)", confirmLabel: null };
      }
      await delay(200);
      continue;
    }

    // Prefer full-screen share when both options are visible.
    const preferredSelection = visibleTexts.has("Share entire screen")
      ? "Share entire screen"
      : visibleTexts.has("Entire screen")
        ? "Entire screen"
        : variant.selectionLabels.find((text) => visibleTexts.has(text));

    if (preferredSelection && selectionLabel !== preferredSelection) {
      const node = findFirstByText(nodes, preferredSelection);
      if (node?.bounds) {
        const { x, y } = boundsCenter(node.bounds);
        runAdb(serial, ["shell", "input", "tap", `${x}`, `${y}`]);
        selectionLabel = preferredSelection;
        selectionTappedAt = Date.now();
        await delay(250);
        continue;
      }
    }

    // In one-app share flows, the app picker may be shown before final confirmation.
    if (visibleTexts.has("NDI for Android")) {
      const appNode = findFirstByText(nodes, "NDI for Android");
      if (appNode?.bounds) {
        const { x, y } = boundsCenter(appNode.bounds);
        runAdb(serial, ["shell", "input", "tap", `${x}`, `${y}`]);
        confirmLabel = confirmLabel ?? "app-selected";
        await delay(250);
        continue;
      }
    }

    // Android 15/16 may show a dedicated "Choose app to share" page.
    if (visibleTexts.has("Choose app to share")) {
      const appNode = findFirstByText(nodes, "NDI for Android");

      if (appNode?.bounds) {
        const { x, y } = boundsCenter(appNode.bounds);
        runAdb(serial, ["shell", "input", "tap", `${x}`, `${y}`]);
        confirmLabel = confirmLabel ?? "app-selected";
        await delay(350);
        continue;
      }

      // Keep scanning app pages until the NDI tile is visible.
      if (Date.now() - lastAppPickerSwipeAt >= 700) {
        runAdb(serial, ["shell", "input", "swipe", "540", "2050", "540", "950", "250"]);
        lastAppPickerSwipeAt = Date.now();
      }
      await delay(250);
      continue;
    }

    const confirm = variant.confirmLabels.find((text) => visibleTexts.has(text));
    if (confirm) {
      const node = findFirstByText(nodes, confirm);
      if (node?.bounds) {
        const { x, y } = boundsCenter(node.bounds);
        runAdb(serial, ["shell", "input", "tap", `${x}`, `${y}`]);
        confirmLabel = confirm;
        await delay(350);
        continue;
      }
    }

    // Some chooser variants hide/omit positive button text; Enter advances the default action.
    if (selectionLabel && !confirmLabel && consentUiVisible && selectionTappedAt > 0 && Date.now() - selectionTappedAt >= 600) {
      runAdb(serial, ["shell", "input", "keyevent", "66"]);
      await delay(350);
      continue;
    }

    if (!consentUiVisible && (selectionLabel || confirmLabel)) {
      return {
        selectionLabel: selectionLabel ?? "implicit",
        confirmLabel,
      };
    }

    await delay(200);
  }

  if (selectionLabel || confirmLabel) {
    return {
      selectionLabel: selectionLabel ?? "implicit",
      confirmLabel,
    };
  }

  // Dialog might not appear when consent is already cached.
  return { selectionLabel: "skipped (dialog not found)", confirmLabel: null };
}



