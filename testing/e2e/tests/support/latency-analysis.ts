import { existsSync, statSync } from "node:fs";

export const MOTION_CROSS_CORRELATION_METHOD = "MOTION_CROSS_CORRELATION" as const;

export type LatencyInvalidReason =
  | "RECEIVER_NOT_PLAYING"
  | "MISSING_RECORDING_ARTIFACT"
  | "UNUSABLE_RECORDING_ARTIFACT"
  | "INVALID_SAMPLE_RATE"
  | "INSUFFICIENT_SIGNAL"
  | "LOW_CORRELATION"
  | "NEGATIVE_LAG";

export type LatencyAnalysisInput = {
  sourceMotionSeries: number[];
  receiverMotionSeries: number[];
  sampleRateFps: number;
  sourceRecordingPath: string | null;
  receiverRecordingPath: string | null;
  receiverPlaybackVerified: boolean;
  validateRecordingArtifacts?: boolean;
  maxLagFrames?: number;
  minCorrelation?: number;
};

export type CorrelationWindowResult = {
  lagFrames: number;
  overlapSamples: number;
  score: number;
};

export type LatencyAnalysisResult = {
  method: typeof MOTION_CROSS_CORRELATION_METHOD;
  status: "VALID" | "INVALID";
  latencyMs: number | null;
  lagFrames: number | null;
  correlation: number | null;
  invalidReason: LatencyInvalidReason | null;
  detail: string;
};

const DEFAULT_MAX_LAG_FRAMES = 180;
const DEFAULT_MIN_CORRELATION = 0.35;
const MIN_REQUIRED_SAMPLES = 12;

function mean(values: number[]): number {
  if (values.length === 0) {
    return 0;
  }

  return values.reduce((sum, value) => sum + value, 0) / values.length;
}

function normalizeSeries(values: number[]): number[] {
  const baseline = mean(values);
  return values.map((value) => value - baseline);
}

export function buildInvalidLatencyResult(reason: LatencyInvalidReason, detail: string): LatencyAnalysisResult {
  return {
    method: MOTION_CROSS_CORRELATION_METHOD,
    status: "INVALID",
    latencyMs: null,
    lagFrames: null,
    correlation: null,
    invalidReason: reason,
    detail,
  };
}

export function computeCrossCorrelationWindow(
  sourceSeries: number[],
  receiverSeries: number[],
  lagFrames: number,
): CorrelationWindowResult {
  let dot = 0;
  let sourceEnergy = 0;
  let receiverEnergy = 0;
  let overlap = 0;

  for (let sourceIndex = 0; sourceIndex < sourceSeries.length; sourceIndex++) {
    const receiverIndex = sourceIndex + lagFrames;
    if (receiverIndex < 0 || receiverIndex >= receiverSeries.length) {
      continue;
    }

    const sourceSample = sourceSeries[sourceIndex];
    const receiverSample = receiverSeries[receiverIndex];
    dot += sourceSample * receiverSample;
    sourceEnergy += sourceSample * sourceSample;
    receiverEnergy += receiverSample * receiverSample;
    overlap += 1;
  }

  if (overlap === 0 || sourceEnergy <= 0 || receiverEnergy <= 0) {
    return {
      lagFrames,
      overlapSamples: overlap,
      score: -1,
    };
  }

  return {
    lagFrames,
    overlapSamples: overlap,
    score: dot / Math.sqrt(sourceEnergy * receiverEnergy),
  };
}

export function analyzeLatencyWithCrossCorrelation(input: LatencyAnalysisInput): LatencyAnalysisResult {
  if (!input.receiverPlaybackVerified) {
    return buildInvalidLatencyResult(
      "RECEIVER_NOT_PLAYING",
      "Receiver playback verification failed during measurement window.",
    );
  }

  if (!input.sourceRecordingPath || !input.receiverRecordingPath) {
    return buildInvalidLatencyResult(
      "MISSING_RECORDING_ARTIFACT",
      "Latency analysis requires source and receiver recording artifact paths.",
    );
  }

  if (input.validateRecordingArtifacts) {
    if (!existsSync(input.sourceRecordingPath) || !existsSync(input.receiverRecordingPath)) {
      return buildInvalidLatencyResult(
        "UNUSABLE_RECORDING_ARTIFACT",
        "Source and receiver recording artifacts must exist on disk for latency analysis.",
      );
    }

    if (statSync(input.sourceRecordingPath).size <= 0 || statSync(input.receiverRecordingPath).size <= 0) {
      return buildInvalidLatencyResult(
        "UNUSABLE_RECORDING_ARTIFACT",
        "Recording artifacts are present but contain no data.",
      );
    }
  }

  if (!Number.isFinite(input.sampleRateFps) || input.sampleRateFps <= 0) {
    return buildInvalidLatencyResult("INVALID_SAMPLE_RATE", `Invalid sampleRateFps=${input.sampleRateFps}`);
  }

  if (input.sourceMotionSeries.length < MIN_REQUIRED_SAMPLES || input.receiverMotionSeries.length < MIN_REQUIRED_SAMPLES) {
    return buildInvalidLatencyResult(
      "INSUFFICIENT_SIGNAL",
      `Insufficient motion samples (source=${input.sourceMotionSeries.length}, receiver=${input.receiverMotionSeries.length}).`,
    );
  }

  const maxLagFrames = Math.max(1, Math.floor(input.maxLagFrames ?? DEFAULT_MAX_LAG_FRAMES));
  const minCorrelation = input.minCorrelation ?? DEFAULT_MIN_CORRELATION;
  const source = normalizeSeries(input.sourceMotionSeries);
  const receiver = normalizeSeries(input.receiverMotionSeries);

  let best: CorrelationWindowResult = { lagFrames: 0, overlapSamples: 0, score: -1 };
  for (let lag = -maxLagFrames; lag <= maxLagFrames; lag++) {
    const window = computeCrossCorrelationWindow(source, receiver, lag);
    if (window.overlapSamples < MIN_REQUIRED_SAMPLES) {
      continue;
    }

    if (window.score > best.score) {
      best = window;
    }
  }

  if (best.score < minCorrelation || best.overlapSamples < MIN_REQUIRED_SAMPLES) {
    return buildInvalidLatencyResult(
      "LOW_CORRELATION",
      `Correlation below threshold (score=${best.score.toFixed(4)}, threshold=${minCorrelation.toFixed(4)}).`,
    );
  }

  if (best.lagFrames < 0) {
    return buildInvalidLatencyResult(
      "NEGATIVE_LAG",
      `Computed lagFrames=${best.lagFrames}, which implies receiver leads publisher and is physically invalid.`,
    );
  }

  const latencyMs = (best.lagFrames * 1000) / input.sampleRateFps;
  return {
    method: MOTION_CROSS_CORRELATION_METHOD,
    status: "VALID",
    latencyMs,
    lagFrames: best.lagFrames,
    correlation: best.score,
    invalidReason: null,
    detail: `Latency measured with lagFrames=${best.lagFrames} at ${input.sampleRateFps}fps.`,
  };
}
