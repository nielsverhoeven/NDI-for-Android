import { test, expect } from "@playwright/test";
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname } from "node:path";
import { PNG } from "pngjs";
import {
  analyzeRegionVisibility,
  compareRegionToBaseline,
  compareRegionToReference,
} from "./visual-assertions";

type Rgb = { r: number; g: number; b: number };

function writeSolidPng(path: string, color: Rgb, width = 64, height = 64): void {
  mkdirSync(dirname(path), { recursive: true });
  const png = new PNG({ width, height });

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const offset = (y * width + x) * 4;
      png.data[offset] = color.r;
      png.data[offset + 1] = color.g;
      png.data[offset + 2] = color.b;
      png.data[offset + 3] = 255;
    }
  }

  writeFileSync(path, PNG.sync.write(png));
}

test("@us2 visibility metrics detect non-black content", async ({}, testInfo) => {
  const black = testInfo.outputPath("black.png");
  const bright = testInfo.outputPath("bright.png");

  writeSolidPng(black, { r: 0, g: 0, b: 0 });
  writeSolidPng(bright, { r: 220, g: 220, b: 220 });

  const bounds = { x1: 0, y1: 0, x2: 64, y2: 64 };
  const blackVisibility = analyzeRegionVisibility(black, bounds);
  const brightVisibility = analyzeRegionVisibility(bright, bounds);

  expect(blackVisibility.nonBlackRatio).toBeLessThan(0.01);
  expect(brightVisibility.nonBlackRatio).toBeGreaterThan(0.9);
});

test("@us2 baseline delta detects frame change", async ({}, testInfo) => {
  const before = testInfo.outputPath("before.png");
  const after = testInfo.outputPath("after.png");

  writeSolidPng(before, { r: 30, g: 30, b: 30 });
  writeSolidPng(after, { r: 180, g: 180, b: 180 });

  const bounds = { x1: 0, y1: 0, x2: 64, y2: 64 };
  const delta = compareRegionToBaseline(after, before, bounds);

  expect(delta.meanAbsoluteDelta).toBeGreaterThan(20);
});

test("@us2 reference similarity favors matching frame", async ({}, testInfo) => {
  const receiverMatch = testInfo.outputPath("receiver-match.png");
  const receiverDifferent = testInfo.outputPath("receiver-different.png");
  const publisher = testInfo.outputPath("publisher.png");

  writeSolidPng(publisher, { r: 100, g: 180, b: 220 });
  writeSolidPng(receiverMatch, { r: 100, g: 180, b: 220 });
  writeSolidPng(receiverDifferent, { r: 15, g: 15, b: 15 });

  const bounds = { x1: 0, y1: 0, x2: 64, y2: 64 };
  const match = compareRegionToReference(receiverMatch, bounds, publisher);
  const different = compareRegionToReference(receiverDifferent, bounds, publisher);

  expect(match.similarity).toBeGreaterThan(different.similarity);
  expect(match.similarity).toBeGreaterThan(0.95);
});
