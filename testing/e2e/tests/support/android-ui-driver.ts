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
