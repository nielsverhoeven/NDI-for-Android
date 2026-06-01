import { test, expect } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

function gateDecision(outcome: string, requiredProfile: boolean): 'pass' | 'fail' {
  if (!requiredProfile) {
    return 'pass';
  }
  if (outcome === 'fail' || outcome === 'blocked') {
    return 'fail';
  }
  return 'pass';
}

test('required profile fails on fail and blocked', async () => {
  expect(gateDecision('fail', true)).toBe('fail');
  expect(gateDecision('blocked', true)).toBe('fail');
});

test('required profile does not fail on not-applicable', async () => {
  expect(gateDecision('not-applicable', true)).toBe('pass');
});

test('optional profile does not gate merge', async () => {
  expect(gateDecision('blocked', false)).toBe('pass');
  expect(gateDecision('not-applicable', false)).toBe('pass');
});

test('required profile passes on pass and not-applicable', async () => {
  expect(gateDecision('pass', true)).toBe('pass');
  expect(gateDecision('not-applicable', true)).toBe('pass');
});

test('primary-status artifact reflects required profile gating data', async () => {
  const statusPath = path.resolve(__dirname, '..', '..', 'artifacts', 'primary-status.json');
  expect(fs.existsSync(statusPath)).toBeTruthy();

  const status = JSON.parse(fs.readFileSync(statusPath, 'utf8'));
  expect(typeof status.requiredProfile).toBe('boolean');
  expect(typeof status.gateDecision).toBe('string');
  expect(Array.isArray(status.selectedScenarioIds)).toBeTruthy();
  expect(status.selectedScenarioIds.length > 0).toBeTruthy();
});
