import {
  tapByResourceIdSuffix,
  waitForResourceIdSuffix,
  waitForResourceIdSuffixAbsent,
} from "./android-ui-driver";

export async function assertThreePaneLayout(serial: string): Promise<void> {
  await waitForResourceIdSuffix(serial, "settingsThreePaneContainer", 15_000);
  await waitForResourceIdSuffix(serial, "settingsMainNavigationPanel", 15_000);
  await waitForResourceIdSuffix(serial, "settingsCategoriesList", 15_000);
  await waitForResourceIdSuffix(serial, "settingsDetailPanel", 15_000);
}

export async function assertCompactLayout(serial: string): Promise<void> {
  await waitForResourceIdSuffix(serial, "settingsCompactContainer", 15_000);
  await waitForResourceIdSuffixAbsent(serial, "settingsThreePaneContainer", 5_000);
}

export async function selectSettingsCategory(serial: string, categoryResourceIdSuffix: string): Promise<void> {
  await tapByResourceIdSuffix(serial, categoryResourceIdSuffix, 15_000);
}
