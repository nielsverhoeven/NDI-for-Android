import { expect, test } from "@playwright/test";
import {
  createScenarioCheckpointRecorder,
  type ScenarioCheckpointTimeline,
} from "./scenario-checkpoints";

// ──────────────────────────────────────────────────────────────────────────────
// T030: Ordered-step enforcement
// ──────────────────────────────────────────────────────────────────────────────

test("@us3 recorder rejects out-of-order step begin", () => {
  const recorder = createScenarioCheckpointRecorder();

  // START_STREAM_A is step 1; jumping to step 3 without beginning step 1 should throw.
  expect(() => recorder.begin("OPEN_CHROME_A")).toThrow(/step order violation/);
});

test("@us3 recorder accepts steps in declared order", () => {
  const recorder = createScenarioCheckpointRecorder();

  expect(() => {
    recorder.begin("START_STREAM_A");
    recorder.pass("START_STREAM_A");

    recorder.begin("START_VIEW_B");
    recorder.pass("START_VIEW_B");

    recorder.begin("OPEN_CHROME_A");
    recorder.pass("OPEN_CHROME_A");
  }).not.toThrow();
});

test("@us3 failing a step marks trailing steps as SKIPPED", () => {
  const recorder = createScenarioCheckpointRecorder();

  recorder.begin("START_STREAM_A");
  recorder.fail("START_STREAM_A", "test-forced-failure");

  const timeline = recorder.toJson();

  const failed = timeline.checkpoints.find((c) => c.stepName === "START_STREAM_A");
  expect(failed?.status).toBe("FAILED");
  expect(failed?.failureReason).toBe("test-forced-failure");

  // All steps after the failed one must be SKIPPED.
  const trailing = timeline.checkpoints.filter((c) => c.stepIndex > (failed?.stepIndex ?? 0));
  for (const step of trailing) {
    expect(step.status).toBe("SKIPPED");
  }
});

test("@us3 failedStepIndex and failedStepName captured on failure", () => {
  const recorder = createScenarioCheckpointRecorder();

  recorder.begin("START_STREAM_A");
  recorder.pass("START_STREAM_A");

  recorder.begin("START_VIEW_B");
  recorder.fail("START_VIEW_B", "connection lost");

  const timeline = recorder.toJson();
  expect(timeline.failedStepIndex).toBe(2);
  expect(timeline.failedStepName).toBe("START_VIEW_B");
});

// ──────────────────────────────────────────────────────────────────────────────
// T032: Checkpoint artifact-shape tests
// ──────────────────────────────────────────────────────────────────────────────

test("@us3 timeline contains exactly six ordered checkpoints", () => {
  const recorder = createScenarioCheckpointRecorder();
  const timeline = recorder.toJson();

  expect(timeline.checkpoints).toHaveLength(6);

  const expectedOrder = [
    "START_STREAM_A",
    "START_VIEW_B",
    "OPEN_CHROME_A",
    "VERIFY_CHROME_ON_B",
    "OPEN_NOS_A",
    "VERIFY_NOS_ON_B",
  ];
  timeline.checkpoints.forEach((checkpoint, index) => {
    expect(checkpoint.stepName).toBe(expectedOrder[index]);
    expect(checkpoint.stepIndex).toBe(index + 1);
  });
});

test("@us3 timeline has runStartedAtEpochMillis set on creation", () => {
  const before = Date.now();
  const recorder = createScenarioCheckpointRecorder();
  const after = Date.now();

  const timeline = recorder.toJson();
  expect(timeline.runStartedAtEpochMillis).toBeGreaterThanOrEqual(before);
  expect(timeline.runStartedAtEpochMillis).toBeLessThanOrEqual(after);
});

test("@us3 fresh timeline has PENDING checkpoints and no failure fields", () => {
  const timeline = createScenarioCheckpointRecorder().toJson();

  expect(timeline.failedStepIndex).toBeNull();
  expect(timeline.failedStepName).toBeNull();
  expect(timeline.runFinishedAtEpochMillis).toBeNull();

  for (const checkpoint of timeline.checkpoints) {
    expect(checkpoint.status).toBe("PENDING");
    expect(checkpoint.startedAtEpochMillis).toBeNull();
    expect(checkpoint.completedAtEpochMillis).toBeNull();
    expect(checkpoint.failureReason).toBeNull();
  }
});

test("@us3 finish sets runFinishedAtEpochMillis", () => {
  const recorder = createScenarioCheckpointRecorder();
  recorder.finish();

  const timeline = recorder.toJson();
  expect(timeline.runFinishedAtEpochMillis).not.toBeNull();
  expect(timeline.runFinishedAtEpochMillis).toBeGreaterThan(0);
});

test("@us3 passed step has timestamps populated", () => {
  const recorder = createScenarioCheckpointRecorder();

  const before = Date.now();
  recorder.begin("START_STREAM_A");
  recorder.pass("START_STREAM_A");
  const after = Date.now();

  const timeline = recorder.toJson();
  const step = timeline.checkpoints[0];

  expect(step.status).toBe("PASSED");
  expect(step.startedAtEpochMillis).toBeGreaterThanOrEqual(before);
  expect(step.completedAtEpochMillis).toBeGreaterThanOrEqual(before);
  expect(step.completedAtEpochMillis).toBeLessThanOrEqual(after);
});
