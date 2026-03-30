import { readFileSync } from "node:fs";
import { join } from "node:path";
import { expect, test } from "@playwright/test";

type RegressionManifest = {
  suite: string;
  specs: string[];
};

const REQUIRED_BASELINES = [
  "tests/interop-dual-emulator.spec.ts",
  "tests/us1-start-output.spec.ts",
  "tests/us2-stop-output.spec.ts",
  "tests/us3-source-loss.spec.ts",
];

function readManifest(): RegressionManifest {
  const manifestPath = join(__dirname, "regression-suite-manifest.json");
  const raw = readFileSync(manifestPath, "utf-8");
  return JSON.parse(raw) as RegressionManifest;
}

test("@us3 regression gate includes required baseline scenarios", () => {
  const manifest = readManifest();

  for (const requiredSpec of REQUIRED_BASELINES) {
    expect(manifest.specs).toContain(requiredSpec);
  }
});

test("@us3 regression gate is not empty", () => {
  const manifest = readManifest();
  expect(manifest.specs.length).toBeGreaterThan(0);
});
