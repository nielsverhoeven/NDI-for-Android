import { execFileSync } from "node:child_process";
import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { platform } from "node:os";
import { join, resolve } from "node:path";

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

function findLatestApkPath(): string | null {
  const apkRoot = resolve(__dirname, "../../../../app/build/outputs/apk");
  if (!existsSync(apkRoot)) {
    return null;
  }

  const stack = [apkRoot];
  const apkCandidates: Array<{ path: string; mtimeMs: number }> = [];

  while (stack.length > 0) {
    const current = stack.pop();
    if (!current) {
      continue;
    }

    for (const entry of readdirSync(current, { withFileTypes: true })) {
      const fullPath = join(current, entry.name);
      if (entry.isDirectory()) {
        stack.push(fullPath);
        continue;
      }

      if (entry.isFile() && entry.name.toLowerCase().endsWith(".apk")) {
        apkCandidates.push({ path: fullPath, mtimeMs: statSync(fullPath).mtimeMs });
      }
    }
  }

  if (apkCandidates.length === 0) {
    return null;
  }

  apkCandidates.sort((a, b) => b.mtimeMs - a.mtimeMs);
  return apkCandidates[0].path;
}

async function globalSetup(): Promise<void> {
  if (process.env.DUAL_EMULATOR_AUTOMATION === "0") {
    return;
  }

  runPowerShellScript("../../../../scripts/verify-e2e-dual-emulator-prereqs.ps1", ["-AllowMissingNdiSdk"]);
  runPowerShellScript("../../scripts/provision-dual-emulator.ps1", ["-Action", "provision-dual", "-InstallNdiSdk", "-SkipBootIfAlreadyRunning"]);
  const reportPath = resolve(__dirname, "../../artifacts/runtime/preinstall-report.json");
  const apkPath = findLatestApkPath();
  const serials = [process.env.EMULATOR_A_SERIAL ?? "emulator-5554", process.env.EMULATOR_B_SERIAL ?? "emulator-5556"];
  const expectedVersionIdentifier = apkPath ? readExpectedVersionIdentifier(apkPath) : null;

  // Always execute preinstall before tests to validate and enforce latest APK install.
  const installArgs = apkPath ? ["-ApkPath", apkPath] : [];
  runPowerShellScript("../../scripts/install-app-preinstall.ps1", installArgs);

  // Keep a sanity check against previous report metadata for troubleshooting.
  isReusablePreinstallReport(reportPath, expectedVersionIdentifier, serials);
  runPowerShellScript("../../scripts/start-relay-server.ps1", ["-Action", "start"]);
}

export default globalSetup;
