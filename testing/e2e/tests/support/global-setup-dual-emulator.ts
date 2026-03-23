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

async function globalSetup(): Promise<void> {
  if (process.env.DUAL_EMULATOR_AUTOMATION === "0") {
    return;
  }

  runPowerShellScript("../../../../scripts/verify-e2e-dual-emulator-prereqs.ps1");
  runPowerShellScript("../../scripts/provision-dual-emulator.ps1", ["-Action", "provision-dual", "-InstallNdiSdk", "-SkipBootIfAlreadyRunning"]);
  runPowerShellScript("../../scripts/start-relay-server.ps1", ["-Action", "start"]);
}

export default globalSetup;
