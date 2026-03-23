import { expect, test } from "@playwright/test";
import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import {
  analyzeLatencyWithCrossCorrelation,
  buildInvalidLatencyResult,
  computeCrossCorrelationWindow,
  MOTION_CROSS_CORRELATION_METHOD,
} from "./latency-analysis";

function makeShiftedMotionSeries(length: number, lagFrames: number): { source: number[]; receiver: number[] } {
  const source = Array.from({ length }, (_, index) => {
    const wave = Math.sin(index / 2.7) * 0.8;
    const pulse = index % 9 === 0 ? 2.0 : 0.0;
    return wave + pulse;
  });

  const receiver = Array.from({ length }, (_, index) => {
    const sourceIndex = index - lagFrames;
    if (sourceIndex < 0 || sourceIndex >= source.length) {
      return 0;
    }
    return source[sourceIndex];
  });

  return { source, receiver };
}

const EXISTING_RECORDING_PATH = __filename;

test("@latency @us1 cross-correlation window scores aligned signal strongly", () => {
  const source = [0, 1, 2, 3, 4, 5, 6, 7];
  const receiver = [0, 0, 1, 2, 3, 4, 5, 6];

  const aligned = computeCrossCorrelationWindow(source, receiver, 1);
  const misaligned = computeCrossCorrelationWindow(source, receiver, -1);

  expect(aligned.score).toBeGreaterThan(misaligned.score);
});

test("@latency @us1 latency analysis returns VALID result with expected lag", () => {
  const lagFrames = 6;
  const { source, receiver } = makeShiftedMotionSeries(160, lagFrames);

  const result = analyzeLatencyWithCrossCorrelation({
    sourceMotionSeries: source,
    receiverMotionSeries: receiver,
    sampleRateFps: 30,
    sourceRecordingPath: EXISTING_RECORDING_PATH,
    receiverRecordingPath: EXISTING_RECORDING_PATH,
    receiverPlaybackVerified: true,
    maxLagFrames: 30,
    minCorrelation: 0.05,
  });

  expect(result.method).toBe(MOTION_CROSS_CORRELATION_METHOD);
  expect(result.status).toBe("VALID");
  expect(result.invalidReason).toBeNull();
  expect(result.lagFrames).toBe(lagFrames);
  expect(result.latencyMs).toBeCloseTo(200, 4);
  expect((result.correlation ?? 0) > 0.95).toBeTruthy();
});

test("@latency @us2 invalid result is returned when receiver playback is not verified", () => {
  const { source, receiver } = makeShiftedMotionSeries(80, 5);

  const result = analyzeLatencyWithCrossCorrelation({
    sourceMotionSeries: source,
    receiverMotionSeries: receiver,
    sampleRateFps: 30,
    sourceRecordingPath: "source.mp4",
    receiverRecordingPath: "receiver.mp4",
    receiverPlaybackVerified: false,
  });

  expect(result.status).toBe("INVALID");
  expect(result.invalidReason).toBe("RECEIVER_NOT_PLAYING");
  expect(result.latencyMs).toBeNull();
});

test("@latency @us2 invalid result is returned when recording paths are missing", () => {
  const { source, receiver } = makeShiftedMotionSeries(80, 3);

  const result = analyzeLatencyWithCrossCorrelation({
    sourceMotionSeries: source,
    receiverMotionSeries: receiver,
    sampleRateFps: 30,
    sourceRecordingPath: null,
    receiverRecordingPath: "receiver.mp4",
    receiverPlaybackVerified: true,
  });

  expect(result.status).toBe("INVALID");
  expect(result.invalidReason).toBe("MISSING_RECORDING_ARTIFACT");
});

test("@latency @us2 invalid result is returned for low-correlation input", () => {
  const source = Array.from({ length: 140 }, (_, index) => Math.sin(index / 3));
  const receiver = Array.from({ length: 140 }, (_, index) => Math.cos(index / 2.5));

  const result = analyzeLatencyWithCrossCorrelation({
    sourceMotionSeries: source,
    receiverMotionSeries: receiver,
    sampleRateFps: 30,
    sourceRecordingPath: EXISTING_RECORDING_PATH,
    receiverRecordingPath: EXISTING_RECORDING_PATH,
    receiverPlaybackVerified: true,
    maxLagFrames: 20,
    minCorrelation: 0.8,
  });

  expect(result.status).toBe("INVALID");
  expect(result.invalidReason).toBe("LOW_CORRELATION");
});

test("@latency @us2 invalid result is returned when lag is negative", () => {
  const lagFrames = -4;
  const { source, receiver } = makeShiftedMotionSeries(160, lagFrames);

  const result = analyzeLatencyWithCrossCorrelation({
    sourceMotionSeries: source,
    receiverMotionSeries: receiver,
    sampleRateFps: 30,
    sourceRecordingPath: EXISTING_RECORDING_PATH,
    receiverRecordingPath: EXISTING_RECORDING_PATH,
    receiverPlaybackVerified: true,
    maxLagFrames: 20,
  });

  expect(result.status).toBe("INVALID");
  expect(result.invalidReason).toBe("NEGATIVE_LAG");
  expect(result.detail).toContain("physically invalid");
  expect(result.latencyMs).toBeNull();
});

test("@latency @us2 invalid helper returns standardized payload", () => {
  const result = buildInvalidLatencyResult("INSUFFICIENT_SIGNAL", "no usable frames");

  expect(result.status).toBe("INVALID");
  expect(result.invalidReason).toBe("INSUFFICIENT_SIGNAL");
  expect(result.detail).toContain("no usable frames");
  expect(result.method).toBe(MOTION_CROSS_CORRELATION_METHOD);
});

test("@latency @us2 invalid result is returned when recording files are not found on disk", () => {
  const { source, receiver } = makeShiftedMotionSeries(80, 3);

  const result = analyzeLatencyWithCrossCorrelation({
    sourceMotionSeries: source,
    receiverMotionSeries: receiver,
    sampleRateFps: 30,
    sourceRecordingPath: "missing-source.mp4",
    receiverRecordingPath: "missing-receiver.mp4",
    receiverPlaybackVerified: true,
    validateRecordingArtifacts: true,
  });

  expect(result.status).toBe("INVALID");
  expect(result.invalidReason).toBe("UNUSABLE_RECORDING_ARTIFACT");
});

test("@latency @us2 invalid result is returned when recording files are empty", ({}, testInfo) => {
  const { source, receiver } = makeShiftedMotionSeries(80, 3);
  const emptyDir = testInfo.outputPath("empty-recordings");
  mkdirSync(emptyDir, { recursive: true });
  const sourcePath = join(emptyDir, "source.mp4");
  const receiverPath = join(emptyDir, "receiver.mp4");
  writeFileSync(sourcePath, "", { encoding: "utf-8" });
  writeFileSync(receiverPath, "", { encoding: "utf-8" });

  const result = analyzeLatencyWithCrossCorrelation({
    sourceMotionSeries: source,
    receiverMotionSeries: receiver,
    sampleRateFps: 30,
    sourceRecordingPath: sourcePath,
    receiverRecordingPath: receiverPath,
    receiverPlaybackVerified: true,
    validateRecordingArtifacts: true,
  });

  expect(result.status).toBe("INVALID");
  expect(result.invalidReason).toBe("UNUSABLE_RECORDING_ARTIFACT");
});
