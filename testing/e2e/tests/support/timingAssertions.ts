import { expect } from '@playwright/test';
import { Page } from '@playwright/test';

export const TIMING_THRESHOLDS = {
    overlayToggle: 1000,      // ms: overlay show/hide and toggle save
    discoveryApply: 1000,     // ms: discovery setting immediate apply
    streamStatusUpdate: 3000, // ms: stream status propagation
    fallbackWarning: 3000,    // ms: fallback warning appearance
};

/**
 * Measure elapsed time with monotonic clock semantics using Date.now().
 * For timing assertion compliance: use median of 3 runs.
 */
export async function measureElapsedMs(fn: () => Promise<void>): Promise<number> {
    const start = Date.now();
    await fn();
    return Date.now() - start;
}

/**
 * Run fn 3 times, return median elapsed time.
 */
export async function medianElapsedMs(fn: () => Promise<void>): Promise<number> {
    const runs: number[] = [];
    for (let i = 0; i < 3; i++) {
        runs.push(await measureElapsedMs(fn));
    }
    runs.sort((a, b) => a - b);
    return runs[1];
}

/**
 * Assert that the action completes within the given threshold (ms).
 * Uses median of 3 runs per the timing measurement convention.
 */
export async function assertWithinThreshold(
    fn: () => Promise<void>,
    thresholdMs: number,
    label: string,
): Promise<void> {
    const median = await medianElapsedMs(fn);
    expect(median, `${label}: median elapsed ${median}ms should be <= ${thresholdMs}ms`).toBeLessThanOrEqual(thresholdMs);
}
