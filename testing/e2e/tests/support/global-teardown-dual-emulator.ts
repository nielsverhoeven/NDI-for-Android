import { execFileSync } from "node:child_process";
import { platform } from "node:os";
import { resolve } from "node:path";

function runPowerShellScript(scriptRelativePath: string, args: string[] = []): void {
  const shell = platform() === "win32" ? "powershell" : "pwsh";
  const script = resolve(__dirname, scriptRelativePath);
  execFileSync(shell, ["-ExecutionPolicy", "Bypass", "-File", script, ...args], {
    stdio: "inherit",
  });
}

async function globalTeardown(): Promise<void> {
  if (process.env.DUAL_EMULATOR_AUTOMATION === "0") {
    return;
  }

  runPowerShellScript("../../scripts/reset-emulator-state.ps1");
  runPowerShellScript("../../scripts/start-relay-server.ps1", ["-Action", "stop"]);
  runPowerShellScript("../../scripts/collect-test-artifacts.ps1", ["-SessionId", `playwright-${Date.now()}`]);
}

export default globalTeardown;
