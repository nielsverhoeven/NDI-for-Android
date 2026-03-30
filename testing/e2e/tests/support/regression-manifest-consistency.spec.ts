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

test("@us3 quality summary wiring includes latency scenario and existing regression", () => {
  const scriptsRoot = join(__dirname, "..", "..", "scripts");
  const primaryRunner = readFileSync(join(scriptsRoot, "run-primary-pr-e2e.ps1"), "utf-8");

  expect(primaryRunner).toContain("latency-scenario");
  expect(primaryRunner).toContain("-LatencyScenarioJson");
  expect(primaryRunner).toContain("tests/support/latency-analysis.spec.ts");
  expect(primaryRunner).toContain("tests/support/scenario-checkpoints.spec.ts");
  expect(primaryRunner).toContain("@latency");
});

test("@us3 summary script emits latency and regression suites", () => {
  const scriptsRoot = join(__dirname, "..", "..", "scripts");
  const summarizeScript = readFileSync(join(scriptsRoot, "summarize-e2e-results.ps1"), "utf-8");

  expect(summarizeScript).toContain("LatencyScenarioJson");
  expect(summarizeScript).toContain("Latency Scenario");
  expect(summarizeScript).toContain("Existing Regression");
});
