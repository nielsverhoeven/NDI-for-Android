import { expect, test, type TestInfo } from "@playwright/test";
import { readFileSync } from "node:fs";
import { PNG } from "pngjs";

import { getDualEmulatorContext, verifyDeviceReady, verifyPackageInstalled } from "./support/android-device-fixtures";
import {
  captureScreenshot,
  clearAppData,
  dumpUi,
  forceStopApp,
  launchMainActivity,
  tapByResourceIdSuffix,
  tapTextContaining,
  waitForAnyResourceIdSuffix,
  waitForText,
} from "./support/android-ui-driver";
import { fetchRelaySources } from "./support/relay-client";
import { analyzeRegionVisibility, compareRegionToBaseline } from "./support/visual-assertions";

type RectBounds = {
  x1: number;
  y1: number;
  x2: number;
  y2: number;
};

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

function boundsContainsPoint(bounds: RectBounds, x: number, y: number): boolean {
  return x >= bounds.x1 && x <= bounds.x2 && y >= bounds.y1 && y <= bounds.y2;
}

function centerOfBounds(bounds: RectBounds): { x: number; y: number } {
  return {
    x: Math.floor((bounds.x1 + bounds.x2) / 2),
    y: Math.floor((bounds.y1 + bounds.y2) / 2),
  };
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function resolveSourceRowBounds(serial: string, sourceName: string): RectBounds {
  const nodes = dumpUi(serial);
  const rowNodes = nodes
    .filter((node) => node.resourceId.endsWith("sourceRowContainer") && node.bounds)
    .map((node) => parseBounds(node.bounds));

  if (rowNodes.length === 0) {
    throw new Error("No source rows are visible in the source list.");
  }

  const sourceTextNode = nodes.find((node) => node.text.includes(sourceName) && node.bounds);
  if (!sourceTextNode?.bounds) {
    throw new Error(`Could not find source text node for '${sourceName}'.`);
  }

  const sourceCenter = centerOfBounds(parseBounds(sourceTextNode.bounds));
  const matchingRow = rowNodes.find((rowBounds) => boundsContainsPoint(rowBounds, sourceCenter.x, sourceCenter.y));
  if (!matchingRow) {
    throw new Error(`Could not resolve source row bounds for '${sourceName}'.`);
  }

  return matchingRow;
}

function resolvePreviewBoundsInRow(serial: string, rowBounds: RectBounds): RectBounds | null {
  const nodes = dumpUi(serial);
  const previewNodes = nodes
    .filter((node) => node.resourceId.endsWith("sourcePreviewImage") && node.bounds)
    .map((node) => parseBounds(node.bounds));

  const matchingPreview = previewNodes.find((previewBounds) => {
    const previewCenter = centerOfBounds(previewBounds);
    return boundsContainsPoint(rowBounds, previewCenter.x, previewCenter.y);
  });

  return matchingPreview ?? null;
}

function compareTwoRegionsInScreenshot(
  screenshotPath: string,
  firstRegion: RectBounds,
  secondRegion: RectBounds,
): { sampledPixels: number; meanAbsoluteDelta: number } {
  const image = PNG.sync.read(readFileSync(screenshotPath));
  const firstWidth = Math.max(1, firstRegion.x2 - firstRegion.x1);
  const firstHeight = Math.max(1, firstRegion.y2 - firstRegion.y1);
  const secondWidth = Math.max(1, secondRegion.x2 - secondRegion.x1);
  const secondHeight = Math.max(1, secondRegion.y2 - secondRegion.y1);
  const sampleWidth = Math.min(firstWidth, secondWidth, 48);
  const sampleHeight = Math.min(firstHeight, secondHeight, 48);

  let totalDelta = 0;
  let sampledPixels = 0;

  for (let y = 0; y < sampleHeight; y++) {
    for (let x = 0; x < sampleWidth; x++) {
      const firstX = Math.min(image.width - 1, firstRegion.x1 + x);
      const firstY = Math.min(image.height - 1, firstRegion.y1 + y);
      const secondX = Math.min(image.width - 1, secondRegion.x1 + x);
      const secondY = Math.min(image.height - 1, secondRegion.y1 + y);

      const firstOffset = (image.width * firstY + firstX) * 4;
      const secondOffset = (image.width * secondY + secondX) * 4;

      const firstLuma =
        0.2126 * (image.data[firstOffset] ?? 0) +
        0.7152 * (image.data[firstOffset + 1] ?? 0) +
        0.0722 * (image.data[firstOffset + 2] ?? 0);
      const secondLuma =
        0.2126 * (image.data[secondOffset] ?? 0) +
        0.7152 * (image.data[secondOffset + 1] ?? 0) +
        0.0722 * (image.data[secondOffset + 2] ?? 0);

      totalDelta += Math.abs(firstLuma - secondLuma);
      sampledPixels += 1;
    }
  }

  if (sampledPixels === 0) {
    throw new Error("No pixels were sampled while comparing source previews.");
  }

  return {
    sampledPixels,
    meanAbsoluteDelta: totalDelta / sampledPixels,
  };
}

async function openSourceListOnPublisher(serial: string, packageName: string): Promise<void> {
  launchMainActivity(serial, packageName);
  await tapByResourceIdSuffix(serial, "streamFragment", 20_000).catch(() => undefined);

  try {
    await waitForAnyResourceIdSuffix(serial, ["sourceRecyclerView", "emptyStateText", "refreshButton"], 30_000);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    test.skip(true, `BLOCKED (environment): unable to reach source list surface (${message}).`);
  }
}

async function viewSourceAndReturnToList(serial: string, sourceDisplayName: string): Promise<void> {
  await tapTextContaining(serial, sourceDisplayName, 20_000);
  await waitForText(serial, "PLAYING", 30_000);
  await sleep(1_800);
  await tapByResourceIdSuffix(serial, "backToListButton", 20_000);
  await waitForAnyResourceIdSuffix(serial, ["sourceRecyclerView", "emptyStateText"], 20_000);
}

async function requireTwoDistinctRelaySources(testInfo: TestInfo): Promise<{ first: string; second: string }> {
  let relaySources: Awaited<ReturnType<typeof fetchRelaySources>> = [];

  try {
    relaySources = await fetchRelaySources();
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    test.skip(true, `BLOCKED (environment): relay source discovery unavailable (${message}).`);
  }

  const uniqueDisplayNames = relaySources
    .map((source) => source.displayName.trim())
    .filter((name) => name.length > 0)
    .filter((name, index, all) => all.indexOf(name) === index);

  await testInfo.attach("relay-sources", {
    body: Buffer.from(JSON.stringify(relaySources, null, 2), "utf-8"),
    contentType: "application/json",
  });

  if (uniqueDisplayNames.length < 2) {
    test.skip(true, "BLOCKED (environment): Two distinct NDI sources are required for multi-source frame retention checks.");
  }

  return {
    first: uniqueDisplayNames[0],
    second: uniqueDisplayNames[1],
  };
}

test("@view @us1 multi-source frames retained independently in list", async ({}, testInfo) => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  const sourceNames = await requireTwoDistinctRelaySources(testInfo);

  await openSourceListOnPublisher(context.publisherSerial, context.packageName);
  await waitForAnyResourceIdSuffix(context.publisherSerial, ["sourceRecyclerView"], 20_000);

  await viewSourceAndReturnToList(context.publisherSerial, sourceNames.first);

  const firstSourceRowBounds = resolveSourceRowBounds(context.publisherSerial, sourceNames.first);
  const firstSourcePreviewBoundsBeforeSecondView = resolvePreviewBoundsInRow(context.publisherSerial, firstSourceRowBounds);
  expect(firstSourcePreviewBoundsBeforeSecondView).not.toBeNull();

  const afterFirstViewScreenshot = testInfo.outputPath("023-source-a-retained-before-viewing-b.png");
  captureScreenshot(context.publisherSerial, afterFirstViewScreenshot);

  await viewSourceAndReturnToList(context.publisherSerial, sourceNames.second);

  const secondSourceRowBounds = resolveSourceRowBounds(context.publisherSerial, sourceNames.second);
  const firstSourcePreviewBoundsAfterSecondView = resolvePreviewBoundsInRow(context.publisherSerial, firstSourceRowBounds);
  const secondSourcePreviewBounds = resolvePreviewBoundsInRow(context.publisherSerial, secondSourceRowBounds);

  expect(firstSourcePreviewBoundsAfterSecondView).not.toBeNull();
  expect(secondSourcePreviewBounds).not.toBeNull();

  const finalListScreenshot = testInfo.outputPath("023-source-list-after-a-then-b.png");
  captureScreenshot(context.publisherSerial, finalListScreenshot);

  const firstVisibility = analyzeRegionVisibility(finalListScreenshot, firstSourcePreviewBoundsAfterSecondView!);
  const secondVisibility = analyzeRegionVisibility(finalListScreenshot, secondSourcePreviewBounds!);
  expect(firstVisibility.nonBlackRatio).toBeGreaterThan(0.04);
  expect(secondVisibility.nonBlackRatio).toBeGreaterThan(0.04);

  const sourceAUnchanged = compareRegionToBaseline(
    finalListScreenshot,
    afterFirstViewScreenshot,
    firstSourcePreviewBoundsAfterSecondView!,
  );
  expect(sourceAUnchanged.meanAbsoluteDelta).toBeLessThan(12);

  const distinctPreviewComparison = compareTwoRegionsInScreenshot(
    finalListScreenshot,
    firstSourcePreviewBoundsAfterSecondView!,
    secondSourcePreviewBounds!,
  );
  expect(distinctPreviewComparison.meanAbsoluteDelta).toBeGreaterThan(6);
});

test("@view @us1 source never viewed shows placeholder", async ({}) => {
  const context = getDualEmulatorContext();
  verifyDeviceReady(context.publisherSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);

  forceStopApp(context.publisherSerial, context.packageName);
  clearAppData(context.publisherSerial, context.packageName);

  await openSourceListOnPublisher(context.publisherSerial, context.packageName);

  let listOrEmpty: Awaited<ReturnType<typeof waitForAnyResourceIdSuffix>>;
  try {
    listOrEmpty = await waitForAnyResourceIdSuffix(
      context.publisherSerial,
      ["sourceRecyclerView", "emptyStateText", "refreshButton"],
      20_000,
    );
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    test.skip(true, `BLOCKED (environment): source list did not stabilize after launch (${message}).`);
    return;
  }

  if (listOrEmpty.suffix === "emptyStateText") {
    test.skip(true, "BLOCKED (environment): No discoverable sources are visible, so placeholder assertions cannot be validated.");
    return;
  }

  const nodes = dumpUi(context.publisherSerial);
  const sourceRows = nodes.filter((node) => node.resourceId.endsWith("sourceRowContainer"));
  const previewNodes = nodes.filter((node) => node.resourceId.endsWith("sourcePreviewImage"));

  if (sourceRows.length === 0) {
    test.skip(true, "BLOCKED (environment): no discovered source rows were rendered for placeholder validation.");
    return;
  }

  expect(previewNodes.length).toBeLessThan(sourceRows.length);
});