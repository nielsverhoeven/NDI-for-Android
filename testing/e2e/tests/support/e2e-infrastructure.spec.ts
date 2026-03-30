import { existsSync } from "node:fs";
import { resolve } from "node:path";
import { expect, test } from "@playwright/test";

test("@dual-emulator infrastructure scripts and feature 009 latency suite are present", () => {
  const requiredPaths = [
    resolve(__dirname, "../../scripts/provision-dual-emulator.ps1"),
    resolve(__dirname, "../../scripts/start-relay-server.ps1"),
    resolve(__dirname, "../../scripts/collect-test-artifacts.ps1"),
    resolve(__dirname, "../interop-dual-emulator.spec.ts"),
    resolve(__dirname, "../../../../specs/009-measure-ndi-latency/tasks.md"),
  ];

  for (const path of requiredPaths) {
    expect(existsSync(path), `Expected file to exist: ${path}`).toBe(true);
  }
});
