export interface OutputScreenShareTiming {
  startEpochMs: number;
  receiverVisibleEpochMs: number;
}

export function computeVisibilityDurationMs(timing: OutputScreenShareTiming): number {
  return timing.receiverVisibleEpochMs - timing.startEpochMs;
}

export function isWithinSeconds(durationMs: number, seconds: number): boolean {
  return durationMs <= seconds * 1000;
}
