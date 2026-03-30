export type ScenarioMetric = {
  name: string;
  durationMs: number;
  passed: boolean;
  details?: string;
};

export function summarizeMetrics(metrics: ScenarioMetric[]) {
  const total = metrics.length;
  const passed = metrics.filter((m) => m.passed).length;
  const failed = total - passed;
  const passRate = total === 0 ? 0 : (passed / total) * 100;
  return {
    total,
    passed,
    failed,
    passRate,
    metrics,
  };
}

// ---- SC-001 through SC-006 evidence helpers (spec 002) ----

/**
 * SC-001: Publisher reaches ACTIVE within 5 seconds in >=90% controlled runs.
 */
export function measureSc001StartLatency(
  startMs: number,
  activeMs: number,
): ScenarioMetric {
  const durationMs = activeMs - startMs;
  return {
    name: "SC-001: publisher_start_latency",
    durationMs,
    passed: durationMs <= 5000,
    details: `Start-to-ACTIVE: ${durationMs}ms (threshold: 5000ms)`,
  };
}

/**
 * SC-002: Stop output propagates to receiver within 2 seconds in >=95% runs.
 */
export function measureSc002StopPropagation(
  stopRequestMs: number,
  receiverStoppedMs: number,
): ScenarioMetric {
  const durationMs = receiverStoppedMs - stopRequestMs;
  return {
    name: "SC-002: stop_propagation_latency",
    durationMs,
    passed: durationMs <= 2000,
    details: `Stop propagation: ${durationMs}ms (threshold: 2000ms)`,
  };
}

/**
 * SC-003: >=95% interruption events expose recovery path within the bounded window.
 */
export function measureSc003RecoveryPathExposed(recoveryShown: boolean): ScenarioMetric {
  return {
    name: "SC-003: recovery_path_exposed",
    durationMs: 0,
    passed: recoveryShown,
    details: recoveryShown ? "Recovery actions visible after interruption" : "Recovery actions NOT shown",
  };
}

/**
 * SC-004: >=90% of operators complete start/stop flow on first attempt.
 * Tracked as a binary pass/fail per run.
 */
export function measureSc004FirstAttemptFlowCompletion(completedFirstAttempt: boolean): ScenarioMetric {
  return {
    name: "SC-004: first_attempt_completion",
    durationMs: 0,
    passed: completedFirstAttempt,
    details: completedFirstAttempt ? "Flow completed on first attempt" : "Flow required retry or failed",
  };
}

/**
 * SC-005: >=95% phone/tablet layout validation pass rate.
 */
export function measureSc005LayoutValidation(
  testedLayouts: number,
  passedLayouts: number,
): ScenarioMetric {
  const passRate = testedLayouts === 0 ? 0 : passedLayouts / testedLayouts;
  return {
    name: "SC-005: layout_validation_pass_rate",
    durationMs: 0,
    passed: passRate >= 0.95,
    details: `${passedLayouts}/${testedLayouts} layouts passed (${(passRate * 100).toFixed(1)}%)`,
  };
}

/**
 * SC-006: Dual-emulator publish->discover->play->stop end-to-end pass rate.
 */
export function measureSc006DualEmulatorE2EPassRate(
  totalRuns: number,
  passedRuns: number,
): ScenarioMetric {
  const passRate = totalRuns === 0 ? 0 : passedRuns / totalRuns;
  return {
    name: "SC-006: dual_emulator_e2e_pass_rate",
    durationMs: 0,
    passed: passRate >= 0.9,
    details: `${passedRuns}/${totalRuns} e2e runs passed (${(passRate * 100).toFixed(1)}%)`,
  };
}

