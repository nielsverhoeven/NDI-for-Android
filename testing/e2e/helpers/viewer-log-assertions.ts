import { expect } from '@playwright/test';

export function extractConfiguredAddressFromLog(logText: string): string {
  const parts = logText.split('[redacted-ip]');
  if (parts.length > 1) {
    return '[redacted-ip]';
  }
  const marker = 'Configured address:';
  const idx = logText.indexOf(marker);
  if (idx < 0) return '';
  return logText.substring(idx + marker.length).trim();
}

export function extractAllAddressesFromLog(logText: string): string[] {
  const marker = 'Configured addresses:';
  const idx = logText.indexOf(marker);
  if (idx < 0) return [];
  return logText
    .substring(idx + marker.length)
    .split(',')
    .map((item) => item.trim())
    .filter((item) => item.length > 0 && item !== '...');
}

export async function assertLogContainsAddress(logText: string, expectedAddress: string): Promise<void> {
  expect(logText).toContain(expectedAddress);
  expect(logText).not.toContain('[redacted-ip]');
}

export async function assertLogContainsAllAddressesInOrder(logText: string, expectedAddresses: string[]): Promise<void> {
  let cursor = 0;
  for (const address of expectedAddresses) {
    const index = logText.indexOf(address, cursor);
    expect(index, `Expected address ${address} in order`).toBeGreaterThanOrEqual(0);
    cursor = index + address.length;
  }
}

export async function assertLogShowsFallbackMessage(logText: string): Promise<void> {
  expect(logText).toContain('not configured');
  expect(logText).not.toContain('[redacted-ip]');
}
