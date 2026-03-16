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
