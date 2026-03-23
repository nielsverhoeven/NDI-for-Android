import { execFileSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { platform } from "node:os";
import { resolve } from "node:path";

type PreFlightReport = {
  buildArtifact?: {
    versionIdentifier?: string | null;
  };
  devices?: Array<{ serial: string; status: string }>;
  overallStatus?: "PASS" | "FAIL";
};

function runPowerShellScript(scriptRelativePath: string, args: string[] = []): void {
  const shell = platform() === "win32" ? "powershell" : "pwsh";
  const script = resolve(__dirname, scriptRelativePath);
  execFileSync(shell, ["-ExecutionPolicy", "Bypass", "-File", script, ...args], {
    stdio: "inherit",
  });
}

function readExpectedVersionIdentifier(apkPath: string): string | null {
  const shell = platform() === "win32" ? "powershell" : "pwsh";
  const script = "$aapt='aapt'; if ($env:ANDROID_SDK_ROOT) { $bt = Join-Path $env:ANDROID_SDK_ROOT 'build-tools'; if (Test-Path $bt) { $d=Get-ChildItem $bt -Directory | Sort-Object Name -Descending | Select-Object -First 1; if ($d) { $c=Join-Path $d.FullName 'aapt.exe'; if (Test-Path $c) { $aapt=$c } } } }; $o=& $aapt dump badging $args[0] 2>$null; if ($LASTEXITCODE -ne 0) { exit 3 }; if ($o -match \"versionCode='(?<code>\\d+)'\\s+versionName='(?<name>[^']+)'\") { Write-Output \"$($Matches.name)+$($Matches.code)\" }";
  try {
    const output = execFileSync(shell, ["-NoProfile", "-Command", script, apkPath], {
      encoding: "utf8",
      stdio: ["ignore", "pipe", "ignore"],
    }).trim();
    return output || null;
  } catch {
    return null;
  }
}

export function isReusablePreinstallReport(reportPath: string, expectedVersionIdentifier: string | null, serials: string[]): boolean {
  if (!existsSync(reportPath)) {
    return false;
  }

  try {
    const report = JSON.parse(readFileSync(reportPath, "utf8")) as PreFlightReport;
    if (report.overallStatus !== "PASS") {
      return false;
    }
    if (!expectedVersionIdentifier) {
      return false;
    }
    if (report.buildArtifact?.versionIdentifier !== expectedVersionIdentifier) {
      return false;
    }

    const statuses = new Map((report.devices ?? []).map((d) => [d.serial, d.status]));
    return serials.every((s) => statuses.get(s) === "PASS");
  } catch {
    return false;
  }
}

async function globalSetup(): Promise<void> {
  if (process.env.DUAL_EMULATOR_AUTOMATION === "0") {
    return;
  }

  runPowerShellScript("../../../../scripts/verify-e2e-dual-emulator-prereqs.ps1");
  runPowerShellScript("../../scripts/provision-dual-emulator.ps1", ["-Action", "provision-dual", "-InstallNdiSdk", "-SkipBootIfAlreadyRunning"]);
  const reportPath = resolve(__dirname, "../../artifacts/runtime/preinstall-report.json");
  const apkPath = resolve(__dirname, "../../../../app/build/outputs/apk/debug/app-debug.apk");
  const serials = [process.env.EMULATOR_A_SERIAL ?? "emulator-5554", process.env.EMULATOR_B_SERIAL ?? "emulator-5556"];
  const expectedVersionIdentifier = readExpectedVersionIdentifier(apkPath);
  if (!isReusablePreinstallReport(reportPath, expectedVersionIdentifier, serials)) {
    runPowerShellScript("../../scripts/install-app-preinstall.ps1");
  }
  runPowerShellScript("../../scripts/start-relay-server.ps1", ["-Action", "start"]);
}

export default globalSetup;
