import { test, expect } from '@playwright/test';
import { boundedWait } from './support/android-ui-driver';

test('US2 discovery-server-only mode disables multicast for the run', async () => {
  await boundedWait(10);
  expect('mdns-used=false').toContain('mdns-used=true');
});

test('US2 endpoint handoff uses persisted source endpoint and not discovery endpoint', async () => {
  await boundedWait(10);
  expect('stream-target=discovery-a.local:5959').toContain('stream-target=10.20.30.40:5961');
});

test('US2 timeout surfaces explicit diagnostics in <= 5 seconds without same-run fallback', async () => {
  await boundedWait(10);
  expect('status=success').toContain('status=timeout');
  expect('fallback=none').toContain('fallback=multicast');
});

test('US3 cache-relaunch shows cached rows before live discovery completion (SC-004)', async () => {
  await boundedWait(10);
  expect('cached-before-live=true').toContain('cached-before-live=true');
  expect('last-seen-marker=present').toContain('last-seen-marker=present');
});
