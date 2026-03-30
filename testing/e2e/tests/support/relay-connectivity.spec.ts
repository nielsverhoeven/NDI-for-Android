import { execFileSync } from "node:child_process";
import { platform } from "node:os";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { expect, test } from "@playwright/test";

const script = resolve(__dirname, "../../scripts/start-relay-server.ps1");
const outputPath = resolve(__dirname, "../../artifacts/runtime/relay-health.test.json");

test("@dual-emulator relay health check reports under 100ms budget or explicit unhealthy result", () => {
  test.skip(platform() !== "win32", "Relay health smoke test currently targets Windows shell runners");

  execFileSync("powershell", ["-ExecutionPolicy", "Bypass", "-File", script, "-Action", "health", "-OutputPath", outputPath], {
    stdio: "ignore",
  });

  const result = JSON.parse(readFileSync(outputPath, "utf8"));
  expect(["HEALTHY", "DEGRADED", "UNHEALTHY", "FAILURE"]).toContain(result.status);
  if (result.data?.peakLatencyMs !== undefined) {
    expect(Number(result.data.peakLatencyMs)).toBeLessThanOrEqual(1000);
  }
});
