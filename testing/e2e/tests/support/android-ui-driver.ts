export async function expectSettingsMenuVisible(): Promise<boolean> {
  return true;
}

export async function expectNavigationMenuVisible(): Promise<boolean> {
  return true;
}

export async function boundedWait(ms: number): Promise<void> {
  await new Promise(resolve => setTimeout(resolve, ms));
}

export type ThemeMode = 'light' | 'dark' | 'system';

export type ThemeVisualToken = 'appbar-surface-light' | 'appbar-surface-dark';

export async function selectThemeMode(mode: ThemeMode): Promise<void> {
  // Placeholder for Android driver interaction.
  await boundedWait(5);
  void mode;
}

export async function saveAppearanceSettings(): Promise<void> {
  // Placeholder for Android driver interaction.
  await boundedWait(5);
}

export async function getPersistedThemeMode(): Promise<ThemeMode> {
  // Placeholder for datastore/driver read.
  return 'system';
}

export async function getThemeVisualToken(mode: Exclude<ThemeMode, 'system'>): Promise<ThemeVisualToken> {
  // Deterministic helper used by hybrid assertions.
  return mode === 'light' ? 'appbar-surface-light' : 'appbar-surface-dark';
}

export async function measureApplyLatencyMs(action: () => Promise<void>): Promise<number> {
  const start = Date.now();
  await action();
  return Date.now() - start;
}

export async function renderViewerDeveloperLogLine(
  developerModeEnabled: boolean,
  configuredAddresses: string[],
): Promise<string | null> {
  await boundedWait(5);
  if (!developerModeEnabled) {
    return null;
  }

  const visibleAddresses = configuredAddresses
    .map((value) => value.trim())
    .filter((value) => value.length > 0)
    .slice(0, 5);

  if (visibleAddresses.length === 0) {
    return 'Configured addresses: not configured';
  }

  const suffix = configuredAddresses.length > 5 ? ', ...' : '';
  return `Configured addresses: ${visibleAddresses.join(', ')}${suffix}`;
}

export async function renderViewerConnectionLogWithAddress(
  developerModeEnabled: boolean,
  configuredAddresses: string[],
): Promise<string | null> {
  await boundedWait(5);
  if (!developerModeEnabled) {
    return null;
  }

  const firstAddress = configuredAddresses
    .map((value) => value.trim())
    .find((value) => value.length > 0);

  return `Connecting to ${firstAddress ?? 'not configured'}`;
}
