import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { expect, test } from "@playwright/test";

type RegressionManifest = {
  suite: string;
  specs: string[];
};

function readManifest(): RegressionManifest {
  const manifestPath = join(__dirname, "regression-suite-manifest.json");
  const raw = readFileSync(manifestPath, "utf-8");
  return JSON.parse(raw) as RegressionManifest;
}

test("@us3 manifest defines existing-regression suite", () => {
  const manifest = readManifest();
  expect(manifest.suite).toBe("existing-regression");
});

test("@us3 manifest paths resolve to workspace test specs", () => {
  const manifest = readManifest();
  const root = join(__dirname, "..");

  for (const specPath of manifest.specs) {
    expect(existsSync(join(root, specPath.replace("tests/", "")))).toBeTruthy();
  }
});

test("@us3 manifest has no duplicate entries", () => {
  const manifest = readManifest();
  expect(new Set(manifest.specs).size).toBe(manifest.specs.length);
});
