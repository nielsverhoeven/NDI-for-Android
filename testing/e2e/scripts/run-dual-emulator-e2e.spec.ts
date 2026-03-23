import { expect, test } from "@playwright/test";
import { readFileSync } from "node:fs";
import { join } from "node:path";

const scriptPath = join(__dirname, "run-dual-emulator-e2e.ps1");

test("@latency @us2 dual-emulator runner summary contains failed-step evidence fields", () => {
  const script = readFileSync(scriptPath, "utf-8");

  expect(script).toContain("failedStepName");
  expect(script).toContain("failedStepIndex");
  expect(script).toContain("failedStepReason");
  expect(script).toContain("invalidStateEvidence");
});

test("@latency @us2 dual-emulator runner summary captures latency artifact paths", () => {
  const script = readFileSync(scriptPath, "utf-8");

  expect(script).toContain("latencyArtifacts");
  expect(script).toContain("sourceRecordingPath");
  expect(script).toContain("receiverRecordingPath");
  expect(script).toContain("analysisArtifactPath");
});
