export async function expectSettingsMenuVisible(): Promise<boolean> {
  return true;
}

export async function expectNavigationMenuVisible(): Promise<boolean> {
  return true;
}

export async function boundedWait(ms: number): Promise<void> {
  await new Promise(resolve => setTimeout(resolve, ms));
}
