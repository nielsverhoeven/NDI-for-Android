import { mkdirSync, writeFileSync } from "node:fs";
import { dirname } from "node:path";

export type SixStepCheckpointName =
  | "START_STREAM_A"
  | "START_VIEW_B"
  | "OPEN_CHROME_A"
  | "VERIFY_CHROME_ON_B"
  | "OPEN_NOS_A"
  | "VERIFY_NOS_ON_B";

export type CheckpointStatus = "PENDING" | "PASSED" | "FAILED" | "SKIPPED";

export type ScenarioCheckpoint = {
  stepIndex: number;
  stepName: SixStepCheckpointName;
  status: CheckpointStatus;
  startedAtEpochMillis: number | null;
  completedAtEpochMillis: number | null;
  failureReason: string | null;
};

export type ScenarioCheckpointTimeline = {
  runStartedAtEpochMillis: number;
  runFinishedAtEpochMillis: number | null;
  failedStepIndex: number | null;
  failedStepName: SixStepCheckpointName | null;
  checkpoints: ScenarioCheckpoint[];
};

const ORDERED_STEPS: SixStepCheckpointName[] = [
  "START_STREAM_A",
  "START_VIEW_B",
  "OPEN_CHROME_A",
  "VERIFY_CHROME_ON_B",
  "OPEN_NOS_A",
  "VERIFY_NOS_ON_B",
];

export class ScenarioCheckpointRecorder {
  private readonly timeline: ScenarioCheckpointTimeline;

  constructor() {
    this.timeline = {
      runStartedAtEpochMillis: Date.now(),
      runFinishedAtEpochMillis: null,
      failedStepIndex: null,
      failedStepName: null,
      checkpoints: ORDERED_STEPS.map((stepName, index) => ({
        stepIndex: index + 1,
        stepName,
        status: "PENDING",
        startedAtEpochMillis: null,
        completedAtEpochMillis: null,
        failureReason: null,
      })),
    };
  }

  begin(step: SixStepCheckpointName): void {
    const checkpoint = this.find(step);
    const expectedStepIndex = this.timeline.checkpoints.findIndex((item) => item.status === "PENDING") + 1;
    if (checkpoint.stepIndex !== expectedStepIndex) {
      throw new Error(
        `Scenario step order violation. Expected step ${expectedStepIndex} but started ${checkpoint.stepIndex} (${checkpoint.stepName}).`,
      );
    }

    checkpoint.startedAtEpochMillis = Date.now();
  }

  pass(step: SixStepCheckpointName): void {
    const checkpoint = this.find(step);
    checkpoint.status = "PASSED";
    checkpoint.completedAtEpochMillis = Date.now();
  }

  fail(step: SixStepCheckpointName, reason: string): void {
    const checkpoint = this.find(step);
    checkpoint.status = "FAILED";
    checkpoint.failureReason = reason;
    checkpoint.completedAtEpochMillis = Date.now();
    this.timeline.failedStepIndex = checkpoint.stepIndex;
    this.timeline.failedStepName = checkpoint.stepName;

    for (const trailing of this.timeline.checkpoints) {
      if (trailing.stepIndex > checkpoint.stepIndex && trailing.status === "PENDING") {
        trailing.status = "SKIPPED";
      }
    }
  }

  finish(): void {
    this.timeline.runFinishedAtEpochMillis = Date.now();
  }

  toJson(): ScenarioCheckpointTimeline {
    return JSON.parse(JSON.stringify(this.timeline)) as ScenarioCheckpointTimeline;
  }

  writeArtifact(path: string): void {
    mkdirSync(dirname(path), { recursive: true });
    writeFileSync(path, JSON.stringify(this.timeline, null, 2), { encoding: "utf-8" });
  }

  private find(step: SixStepCheckpointName): ScenarioCheckpoint {
    const checkpoint = this.timeline.checkpoints.find((item) => item.stepName === step);
    if (!checkpoint) {
      throw new Error(`Unknown scenario step ${step}`);
    }
    return checkpoint;
  }
}

export function createScenarioCheckpointRecorder(): ScenarioCheckpointRecorder {
  return new ScenarioCheckpointRecorder();
}
