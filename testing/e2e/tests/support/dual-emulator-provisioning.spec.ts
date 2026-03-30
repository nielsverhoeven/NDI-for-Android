import { execFileSync } from "node:child_process";
import { platform } from "node:os";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { expect, test } from "@playwright/test";

const script = resolve(__dirname, "../../scripts/provision-dual-emulator.ps1");
const outputPath = resolve(__dirname, "../../artifacts/runtime/provisioning-result.test.json");

test("@dual-emulator provisioning script emits valid status JSON", () => {
  test.skip(platform() !== "win32", "Provisioning smoke test currently targets Windows shell runners");

  execFileSync("powershell", ["-ExecutionPolicy", "Bypass", "-File", script, "-Action", "provision-dual", "-SkipBootIfAlreadyRunning", "-OutputPath", outputPath], {
    stdio: "ignore",
  });

  const result = JSON.parse(readFileSync(outputPath, "utf8"));
  expect(["SUCCESS", "PARTIAL_SUCCESS", "FAILURE"]).toContain(result.status);
  expect(result.operation).toBe("Provision-DualEmulator");
});
