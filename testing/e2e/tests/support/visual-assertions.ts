import { readFileSync } from "node:fs";
import { PNG } from "pngjs";
import type { RectBounds } from "./android-ui-driver";

export type VisibilityCheckResult = {
  sampledPixels: number;
  nonBlackPixels: number;
  nonBlackRatio: number;
  averageLuma: number;
};

export type SimilarityCheckResult = {
  sampledPixels: number;
  meanAbsoluteDelta: number;
  similarity: number;
};

export type ChangeCheckResult = {
  sampledPixels: number;
  meanAbsoluteDelta: number;
};

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}

export function analyzeRegionVisibility(
  screenshotPath: string,
  bounds: RectBounds,
  options?: { blackThreshold?: number; stride?: number },
): VisibilityCheckResult {
  const blackThreshold = options?.blackThreshold ?? 18;
  const stride = options?.stride ?? 3;

  const image = PNG.sync.read(readFileSync(screenshotPath));
  const x1 = clamp(bounds.x1, 0, image.width - 1);
  const y1 = clamp(bounds.y1, 0, image.height - 1);
  const x2 = clamp(bounds.x2, x1 + 1, image.width);
  const y2 = clamp(bounds.y2, y1 + 1, image.height);

  let sampledPixels = 0;
  let nonBlackPixels = 0;
  let lumaTotal = 0;

  for (let y = y1; y < y2; y += stride) {
    for (let x = x1; x < x2; x += stride) {
      const offset = (image.width * y + x) * 4;
      const r = image.data[offset] ?? 0;
      const g = image.data[offset + 1] ?? 0;
      const b = image.data[offset + 2] ?? 0;

      const luma = 0.2126 * r + 0.7152 * g + 0.0722 * b;
      sampledPixels += 1;
      lumaTotal += luma;
      if (luma > blackThreshold) {
        nonBlackPixels += 1;
      }
    }
  }

  if (sampledPixels === 0) {
    throw new Error("No pixels were sampled for visibility analysis.");
  }

  return {
    sampledPixels,
    nonBlackPixels,
    nonBlackRatio: nonBlackPixels / sampledPixels,
    averageLuma: lumaTotal / sampledPixels,
  };
}

export function assertRegionShowsVisibleContent(
  screenshotPath: string,
  bounds: RectBounds,
  minNonBlackRatio = 0.08,
): VisibilityCheckResult {
  const result = analyzeRegionVisibility(screenshotPath, bounds);
  if (result.nonBlackRatio < minNonBlackRatio) {
    throw new Error(
      `Viewer surface appears black: nonBlackRatio=${result.nonBlackRatio.toFixed(4)}, ` +
        `averageLuma=${result.averageLuma.toFixed(2)}, sampledPixels=${result.sampledPixels}`,
    );
  }

  return result;
}

function sampleGrayscaleGrid(
  image: PNG,
  bounds: RectBounds,
  gridWidth: number,
  gridHeight: number,
): number[] {
  const x1 = clamp(bounds.x1, 0, image.width - 1);
  const y1 = clamp(bounds.y1, 0, image.height - 1);
  const x2 = clamp(bounds.x2, x1 + 1, image.width);
  const y2 = clamp(bounds.y2, y1 + 1, image.height);

  const samples: number[] = [];
  for (let gy = 0; gy < gridHeight; gy++) {
    for (let gx = 0; gx < gridWidth; gx++) {
      const px = Math.floor(x1 + ((gx + 0.5) * (x2 - x1)) / gridWidth);
      const py = Math.floor(y1 + ((gy + 0.5) * (y2 - y1)) / gridHeight);
      const offset = (image.width * clamp(py, 0, image.height - 1) + clamp(px, 0, image.width - 1)) * 4;
      const r = image.data[offset] ?? 0;
      const g = image.data[offset + 1] ?? 0;
      const b = image.data[offset + 2] ?? 0;
      const luma = 0.2126 * r + 0.7152 * g + 0.0722 * b;
      samples.push(luma);
    }
  }
  return samples;
}

export function compareRegionToReference(
  receiverScreenshotPath: string,
  receiverBounds: RectBounds,
  publisherScreenshotPath: string,
  options?: { gridWidth?: number; gridHeight?: number },
): SimilarityCheckResult {
  const gridWidth = options?.gridWidth ?? 48;
  const gridHeight = options?.gridHeight ?? 48;

  const receiver = PNG.sync.read(readFileSync(receiverScreenshotPath));
  const publisher = PNG.sync.read(readFileSync(publisherScreenshotPath));

  const receiverSamples = sampleGrayscaleGrid(receiver, receiverBounds, gridWidth, gridHeight);
  const publisherSamples = sampleGrayscaleGrid(
    publisher,
    { x1: 0, y1: 0, x2: publisher.width, y2: publisher.height },
    gridWidth,
    gridHeight,
  );

  let totalDelta = 0;
  for (let i = 0; i < receiverSamples.length; i++) {
    totalDelta += Math.abs(receiverSamples[i] - publisherSamples[i]);
  }

  const sampledPixels = receiverSamples.length;
  const meanAbsoluteDelta = totalDelta / sampledPixels;
  const similarity = 1 - meanAbsoluteDelta / 255;

  return {
    sampledPixels,
    meanAbsoluteDelta,
    similarity,
  };
}

export function assertRegionMatchesReference(
  receiverScreenshotPath: string,
  receiverBounds: RectBounds,
  publisherScreenshotPath: string,
  minSimilarity = 0.52,
): SimilarityCheckResult {
  const result = compareRegionToReference(receiverScreenshotPath, receiverBounds, publisherScreenshotPath);
  if (result.similarity < minSimilarity) {
    throw new Error(
      `Receiver frame does not match publisher content closely enough: similarity=${result.similarity.toFixed(4)}, ` +
        `meanAbsoluteDelta=${result.meanAbsoluteDelta.toFixed(2)}`,
    );
  }
  return result;
}

export function compareRegionToBaseline(
  afterScreenshotPath: string,
  beforeScreenshotPath: string,
  bounds: RectBounds,
  options?: { gridWidth?: number; gridHeight?: number },
): ChangeCheckResult {
  const gridWidth = options?.gridWidth ?? 48;
  const gridHeight = options?.gridHeight ?? 48;

  const afterImage = PNG.sync.read(readFileSync(afterScreenshotPath));
  const beforeImage = PNG.sync.read(readFileSync(beforeScreenshotPath));

  const afterSamples = sampleGrayscaleGrid(afterImage, bounds, gridWidth, gridHeight);
  const beforeSamples = sampleGrayscaleGrid(beforeImage, bounds, gridWidth, gridHeight);

  let totalDelta = 0;
  for (let i = 0; i < afterSamples.length; i++) {
    totalDelta += Math.abs(afterSamples[i] - beforeSamples[i]);
  }

  return {
    sampledPixels: afterSamples.length,
    meanAbsoluteDelta: totalDelta / afterSamples.length,
  };
}

export function assertRegionChangedFromBaseline(
  afterScreenshotPath: string,
  beforeScreenshotPath: string,
  bounds: RectBounds,
  minMeanAbsoluteDelta = 8,
): ChangeCheckResult {
  const result = compareRegionToBaseline(afterScreenshotPath, beforeScreenshotPath, bounds);
  if (result.meanAbsoluteDelta < minMeanAbsoluteDelta) {
    throw new Error(
      `Receiver surface did not change from baseline enough: meanAbsoluteDelta=${result.meanAbsoluteDelta.toFixed(2)}, ` +
        `sampledPixels=${result.sampledPixels}`,
    );
  }
  return result;
}

