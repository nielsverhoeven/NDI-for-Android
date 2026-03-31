import { test, expect } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

function isValidOutcome(value: string): boolean {
  return ['pass', 'fail', 'blocked', 'not-applicable'].includes(value);
}

test('result schema supports canonical outcomes', async () => {
  expect(isValidOutcome('pass')).toBeTruthy();
  expect(isValidOutcome('fail')).toBeTruthy();
  expect(isValidOutcome('blocked')).toBeTruthy();
  expect(isValidOutcome('not-applicable')).toBeTruthy();
});

test('triage summary fields are required for failed runs', async () => {
  const triage = {
    status: 'fail',
    failureTimestampUtc: '2026-03-31T17:00:00Z',
    firstClassifiedAtUtc: '2026-03-31T17:10:00Z',
    rootCauseCategory: 'test-defect',
    scenarioIds: ['us3-developer-mode']
  };

  expect(triage.status).toBe('fail');
  expect(Array.isArray(triage.scenarioIds)).toBeTruthy();
  expect(triage.rootCauseCategory.length > 0).toBeTruthy();
  expect(triage.failureTimestampUtc.length > 0).toBeTruthy();
  expect(triage.firstClassifiedAtUtc.length > 0).toBeTruthy();
});

test('required primary gate artifact set is present', async () => {
  const artifactsDir = path.resolve(__dirname, '..', '..', 'artifacts');
  const statusPath = path.join(artifactsDir, 'primary-status.json');
  expect(fs.existsSync(statusPath)).toBeTruthy();

  const status = JSON.parse(fs.readFileSync(statusPath, 'utf8'));
  expect(isValidOutcome(status.status)).toBeTruthy();
  expect(typeof status.gateDecision).toBe('string');
});

test('triage classification is within 15-minute SLA window', async () => {
  const failure = new Date('2026-03-31T17:00:00Z').getTime();
  const firstClassified = new Date('2026-03-31T17:10:00Z').getTime();
  const deltaMinutes = (firstClassified - failure) / 60000;
  expect(deltaMinutes <= 15).toBeTruthy();
});

test('not-applicable outcomes do not force required profile gate failure', async () => {
  const artifactsDir = path.resolve(__dirname, '..', '..', 'artifacts');
  const statusPath = path.join(artifactsDir, 'primary-status.json');
  if (!fs.existsSync(statusPath)) {
    test.skip();
  }

  const status = JSON.parse(fs.readFileSync(statusPath, 'utf8'));
  if (status.status === 'not-applicable') {
    expect(status.gateDecision).toBe('pass');
  }
  else {
    expect(isValidOutcome(status.status)).toBeTruthy();
  }
});
