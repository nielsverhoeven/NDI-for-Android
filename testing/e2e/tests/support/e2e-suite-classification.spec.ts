import { expect, test } from "@playwright/test";

export const NEW_SETTINGS_SPECS = [
  "tests/settings-navigation-source-list.spec.ts",
  "tests/settings-navigation-viewer.spec.ts",
  "tests/settings-navigation-output.spec.ts",
  "tests/settings-valid-discovery-persistence.spec.ts",
  "tests/settings-invalid-discovery-validation.spec.ts",
  "tests/settings-discovery-fallback.spec.ts",
  "tests/settings-discovery-config.spec.ts",
] as const;

export const EXISTING_REGRESSION_SPECS = [
  "tests/interop-dual-emulator.spec.ts",
  "tests/three-screen-navigation.spec.ts",
  "tests/us1-output-navigation.spec.ts",
  "tests/us1-start-output.spec.ts",
  "tests/us2-output-status.spec.ts",
  "tests/us2-stop-output.spec.ts",
  "tests/us3-recovery-actions.spec.ts",
  "tests/us3-source-loss.spec.ts",
  "tests/settings-developer-overlay.spec.ts",
] as const;

export const LATENCY_SCENARIO_SPECS = [
  "tests/interop-dual-emulator.spec.ts",
] as const;

function hasDuplicates(values: readonly string[]): boolean {
  return new Set(values).size !== values.length;
}

test("@settings @us1 suite classification: new settings specs are unique", () => {
  expect(hasDuplicates(NEW_SETTINGS_SPECS)).toBeFalsy();
});

test("@settings @us2 suite classification: existing regression specs are unique", () => {
  expect(hasDuplicates(EXISTING_REGRESSION_SPECS)).toBeFalsy();
});

test("@settings @us3 suite classification: settings suite does not overlap regression suite", () => {
  const regression = new Set<string>(EXISTING_REGRESSION_SPECS);
  const overlap = NEW_SETTINGS_SPECS.filter((spec) => regression.has(spec));
  expect(overlap).toEqual([]);
});

test("@settings @us1 suite classification: toggle entry specs are present", () => {
  expect(NEW_SETTINGS_SPECS).toContain("tests/settings-navigation-source-list.spec.ts");
  expect(NEW_SETTINGS_SPECS).toContain("tests/settings-navigation-viewer.spec.ts");
  expect(NEW_SETTINGS_SPECS).toContain("tests/settings-navigation-output.spec.ts");
});

test("@latency @us1 suite classification: latency specs are unique", () => {
  expect(hasDuplicates(LATENCY_SCENARIO_SPECS)).toBeFalsy();
});

test("@latency @us1 suite classification: latency specs stay isolated from settings suite", () => {
  const settings = new Set<string>(NEW_SETTINGS_SPECS);
  const overlap = LATENCY_SCENARIO_SPECS.filter((spec) => settings.has(spec));
  expect(overlap).toEqual([]);
});

test("@latency @us3 suite classification: latency specs are tracked by existing regression suite", () => {
  const regression = new Set<string>(EXISTING_REGRESSION_SPECS);
  for (const spec of LATENCY_SCENARIO_SPECS) {
    expect(regression.has(spec)).toBeTruthy();
  }
});
