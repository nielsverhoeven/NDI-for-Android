import { execFileSync } from "node:child_process";
import { writeFileSync } from "node:fs";

const MAIN_ACTIVITY = "com.ndi.app.MainActivity";

function runAdbRaw(serial: string, args: string[]): string {
  return execFileSync("adb", ["-s", serial, ...args], {
    encoding: "utf-8",
    stdio: ["ignore", "pipe", "pipe"],
    maxBuffer: 16 * 1024 * 1024,
  });
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
    await delay(750);
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
      await delay(350);
      return;
    }
    await delay(500);
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
        await delay(350);
        return text;
      }
    }
    await delay(400);
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
    await delay(750);
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
      await delay(350);
      return match.text;
    }
    await delay(500);
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
      await delay(200);
      runAdb(serial, ["shell", "input", "keyevent", "123"]); // move cursor to end
      for (let i = 0; i < 96; i++) {
        runAdb(serial, ["shell", "input", "keyevent", "67"]); // DEL
      }

      const encoded = value.replace(/ /g, "%s");
      runAdb(serial, ["shell", "input", "text", encoded]);
      await delay(300);
      return;
    }
    await delay(400);
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
    await delay(700);
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
      await delay(200);
      runAdb(serial, ["shell", "input", "keyevent", "123"]); // end of line
      for (let i = 0; i < deleteCharsFromEnd; i++) {
        runAdb(serial, ["shell", "input", "keyevent", "67"]);
      }
      if (appendText.length > 0) {
        runAdb(serial, ["shell", "input", "text", appendText.replace(/ /g, "%s")]);
      }
      await delay(300);
      return;
    }
    await delay(350);
  }

  throw new Error(`Timed out editing text tail for resource id suffix '${resourceIdSuffix}' on ${serial}`);
}

export async function pressBack(serial: string): Promise<void> {
  runAdb(serial, ["shell", "input", "keyevent", "4"]);
  await delay(300);
}



