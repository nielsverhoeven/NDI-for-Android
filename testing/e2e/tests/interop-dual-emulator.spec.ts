import { expect, test, type TestInfo } from "@playwright/test";
import { copyFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import {
  type AndroidVersionInfo,
  type SupportedVersionWindow,
  computeSupportedVersionWindow,
  getDualEmulatorContext,
  verifySupportedAndroidVersion,
  verifyDeviceReady,
  verifyPackageInstalled,
} from "./support/android-device-fixtures";
import {
  captureScreenshot,
  clearAppData,
  clearLogcat,
  completeScreenShareConsent,
  pressHome,
  forceStopApp,
  editTextTailByResourceIdSuffix,
  getBoundsByResourceIdSuffix,
  getTextByResourceIdSuffix,
  launchChrome,
  launchChromeUrl,
  launchDeepLink,
  launchMainActivity,
  launchPackageFromLauncher,
  pressBack,
  replaceTextByResourceIdSuffix,
  startScreenRecording,
  stopScreenRecording,
  tapFirstAvailableText,
  tapText,
  tapTextContaining,
  waitForText,
  waitForTextAbsent,
  waitForTextContaining,
  waitForAnyResourceIdSuffix,
  waitForResourceIdTextContaining,
  writeLogcatSnapshot,
  writeUiSnapshot,
} from "./support/android-ui-driver";
import {
  fetchRelaySources,
  uploadRelayFrame,
} from "./support/relay-client";
import {
  analyzeRegionVisibility,
  assertRegionChangedFromBaseline,
  assertRegionMatchesReference,
  assertRegionShowsVisibleContent,
  compareRegionToBaseline,
  compareRegionToReference,
} from "./support/visual-assertions";
import {
  createLatencyScenarioCheckpointRecorder,
  createScenarioCheckpointRecorder,
  type LatencyCheckpointName,
  type SixStepCheckpointName,
} from "./support/scenario-checkpoints";
import { analyzeLatencyWithCrossCorrelation } from "./support/latency-analysis";

const externalScreenshotDir = process.env.DUAL_EMULATOR_SCREENSHOT_DIR;
const checkpointArtifactPath = process.env.DUAL_EMULATOR_CHECKPOINT_PATH;

const LATENCY_SAMPLE_INTERVAL_MS = 200;
const LATENCY_SAMPLE_COUNT = 28;
const FULL_FRAME_BOUNDS = { x1: 0, y1: 0, x2: 10_000, y2: 10_000 };
const YOUTUBE_VIDEO_URLS = [
  "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
  "https://www.youtube.com/watch?v=aqz-KE-bpKQ",
  "https://www.youtube.com/watch?v=jNQXAC9IVRw",
] as const;

type LatencyScenarioRunResult = {
  analysisArtifactPath: string;
  sourceRecordingPath: string;
  receiverRecordingPath: string;
  sourceSnapshotPaths: string[];
  receiverSnapshotPaths: string[];
};

function mirrorScreenshotIfConfigured(fileName: string, sourcePath: string): void {
  if (!externalScreenshotDir || !existsSync(sourcePath)) {
    return;
  }

  mkdirSync(externalScreenshotDir, { recursive: true });
  copyFileSync(sourcePath, join(externalScreenshotDir, fileName));
}

function getRunnerArtifactRoot(): string | null {
  if (!externalScreenshotDir) {
    return null;
  }

  return dirname(externalScreenshotDir);
}

function mirrorLatencyArtifactIfConfigured(relativePath: string, sourcePath: string): string {
  const runnerArtifactRoot = getRunnerArtifactRoot();
  if (!runnerArtifactRoot || !existsSync(sourcePath)) {
    return sourcePath;
  }

  const destinationPath = join(runnerArtifactRoot, relativePath);
  mkdirSync(dirname(destinationPath), { recursive: true });
  copyFileSync(sourcePath, destinationPath);
  return destinationPath;
}

async function sleep(ms: number): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, ms));
}

function computeSeriesSpread(values: number[]): { range: number; stddev: number } {
  if (values.length === 0) {
    return { range: 0, stddev: 0 };
  }

  const min = Math.min(...values);
  const max = Math.max(...values);
  const mean = values.reduce((sum, value) => sum + value, 0) / values.length;
  const variance = values.reduce((sum, value) => sum + (value - mean) * (value - mean), 0) / values.length;
  return {
    range: max - min,
    stddev: Math.sqrt(variance),
  };
}

function pickRandomYoutubeUrl(): string {
  const explicitUrl = process.env.LATENCY_YOUTUBE_URL;
  if (explicitUrl && explicitUrl.trim().length > 0) {
    return explicitUrl.trim();
  }

  const index = Math.floor(Math.random() * YOUTUBE_VIDEO_URLS.length);
  return YOUTUBE_VIDEO_URLS[index] ?? YOUTUBE_VIDEO_URLS[0];
}

function normalizeErrorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

function isYoutubeUnavailableFailure(error: unknown): boolean {
  const message = normalizeErrorMessage(error).toLowerCase();
  return (
    message.includes("youtube") ||
    message.includes("chrome") ||
    message.includes("url_bar") ||
    message.includes("timed out") ||
    message.includes("playback did not progress")
  );
}

function resolveCheckpointArtifactPath(checkpointOutputPath: string): string {
  if (!checkpointArtifactPath) {
    return checkpointOutputPath;
  }

  const resolvedPath = resolve(checkpointArtifactPath);
  mkdirSync(dirname(resolvedPath), { recursive: true });
  return resolvedPath;
}

async function verifyPublisherYoutubePlaybackProgression(
  serial: string,
  testInfo: TestInfo,
): Promise<{ range: number; stddev: number; sampleCount: number }> {
  const sampleCount = 5;
  const sampleIntervalMs = 900;
  const publisherMotionSeries: number[] = [];

  for (let sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++) {
    const screenshotPath = testInfo.outputPath(`latency-publisher-youtube-progress-${sampleIndex}.png`);
    captureScreenshot(serial, screenshotPath);
    publisherMotionSeries.push(analyzeRegionVisibility(screenshotPath, FULL_FRAME_BOUNDS, { stride: 6 }).averageLuma);

    if (sampleIndex < sampleCount - 1) {
      await sleep(sampleIntervalMs);
    }
  }

  const spread = computeSeriesSpread(publisherMotionSeries);
  if (spread.range < 10 || spread.stddev < 2.5) {
    throw new Error(
      `Publisher YouTube playback did not progress after launch: range=${spread.range.toFixed(2)}, stddev=${spread.stddev.toFixed(2)}, sampleCount=${sampleCount}.`,
    );
  }

  return {
    ...spread,
    sampleCount,
  };
}

async function runLatencyMeasurementScenario(testInfo: TestInfo): Promise<LatencyScenarioRunResult> {
  const context = getDualEmulatorContext();

  verifyDeviceReady(context.publisherSerial);
  verifyDeviceReady(context.receiverSerial);
  const supportWindow = computeSupportedVersionWindow();
  const publisherAndroid = verifySupportedAndroidVersion(context.publisherSerial, "publisher");
  const receiverAndroid = verifySupportedAndroidVersion(context.receiverSerial, "receiver");
  verifyPackageInstalled(context.publisherSerial, context.packageName);
  verifyPackageInstalled(context.receiverSerial, context.packageName);
  await attachAndroidVersionValidation(testInfo, publisherAndroid, receiverAndroid, supportWindow);

  clearLogcat(context.publisherSerial);
  clearLogcat(context.receiverSerial);
  forceStopApp(context.publisherSerial, context.packageName);
  forceStopApp(context.receiverSerial, context.packageName);
  clearAppData(context.receiverSerial, context.packageName);

  const receiverBeforePlaybackPath = testInfo.outputPath("latency-receiver-before-playback.png");
  const sourceRecordingPath = testInfo.outputPath("recordings/source-recording.mp4");
  const receiverRecordingPath = testInfo.outputPath("recordings/receiver-recording.mp4");
  const analysisArtifactPath = testInfo.outputPath("latency-analysis.json");
  const artifactManifestPath = testInfo.outputPath("latency-artifacts.json");
  const checkpointOutputPath = testInfo.outputPath("latency-checkpoints.json");
  const runnerCheckpointOutputPath = resolveCheckpointArtifactPath(checkpointOutputPath);
  const publisherUiPath = testInfo.outputPath("latency-publisher-ui.txt");
  const receiverUiPath = testInfo.outputPath("latency-receiver-ui.txt");
  const publisherLogcatPath = testInfo.outputPath("latency-publisher-logcat.txt");
  const receiverLogcatPath = testInfo.outputPath("latency-receiver-logcat.txt");

  const checkpoints = createLatencyScenarioCheckpointRecorder();
  let activeStep: LatencyCheckpointName | null = null;

  const sourceSnapshots: string[] = [];
  const receiverSnapshots: string[] = [];
  const sourceMotionSeries: number[] = [];
  const receiverMotionSeries: number[] = [];
  const youtubeUrl = pickRandomYoutubeUrl();

  let receiverPlaybackVerified = false;
  let viewerBounds: { x1: number; y1: number; x2: number; y2: number } | null = null;

  let sourceRecording = null as Awaited<ReturnType<typeof startScreenRecording>> | null;
  let receiverRecording = null as Awaited<ReturnType<typeof startScreenRecording>> | null;

  const beginStep = (step: LatencyCheckpointName): void => {
    checkpoints.begin(step);
    activeStep = step;
  };

  const passStep = (): void => {
    if (!activeStep) {
      return;
    }
    checkpoints.pass(activeStep);
    activeStep = null;
  };

  try {
    beginStep("START_STREAM_A");
    launchDeepLink(context.publisherSerial, context.packageName, "ndi://output/device-screen:local");
    const publisherConsent = await handleMediaProjectionConsent(context.publisherSerial, publisherAndroid.majorVersion);
    await testInfo.attach("latency-publisher-consent", {
      body: Buffer.from(JSON.stringify(publisherConsent, null, 2), "utf-8"),
      contentType: "application/json",
    });

    await ensureOutputScreen(context.publisherSerial, context.packageName, publisherAndroid.majorVersion);
    const streamConfig = await configureDiscoverableName(context.publisherSerial, "Latency Session");
    await tapText(context.publisherSerial, "Start Output", 20_000);
    await handleMediaProjectionConsent(context.publisherSerial, publisherAndroid.majorVersion, false);
    await waitForText(context.publisherSerial, "ACTIVE", 45_000);
    passStep();

    beginStep("OPEN_VIEWER_B");
    launchMainActivity(context.receiverSerial, context.packageName);
    await ensureReceiverDiscoveryScreen(context.receiverSerial, context.packageName);
    await tapText(context.receiverSerial, "Refresh", 20_000);
    captureScreenshot(context.receiverSerial, receiverBeforePlaybackPath);
    const discoveredName = await waitForTextContaining(context.receiverSerial, streamConfig.discoverableName, 60_000);
    await tapTextContaining(context.receiverSerial, discoveredName, 20_000);
    await waitForText(context.receiverSerial, "PLAYING", 30_000);
    viewerBounds = getBoundsByResourceIdSuffix(context.receiverSerial, ":id/viewerSurfacePlaceholder");
    passStep();

    beginStep("START_SOURCE_RECORDING");
    sourceRecording = startScreenRecording(context.publisherSerial, "source", {
      remotePath: "/sdcard/ndi-e2e-source-recording.mp4",
      maxDurationSeconds: 90,
      bitRateMbps: 10,
    });
    passStep();

    beginStep("START_RECEIVER_RECORDING");
    receiverRecording = startScreenRecording(context.receiverSerial, "receiver", {
      remotePath: "/sdcard/ndi-e2e-receiver-recording.mp4",
      maxDurationSeconds: 90,
      bitRateMbps: 10,
    });
    passStep();

    beginStep("PLAY_RANDOM_YOUTUBE_A");
    try {
      launchChromeUrl(context.publisherSerial, youtubeUrl);
      await waitForResourceIdTextContaining(context.publisherSerial, ":id/url_bar", "youtube", 30_000);
      const publisherPlaybackProgress = await verifyPublisherYoutubePlaybackProgression(context.publisherSerial, testInfo);
      await testInfo.attach("latency-youtube-url", {
        body: Buffer.from(JSON.stringify({ youtubeUrl }, null, 2), "utf-8"),
        contentType: "application/json",
      });
      await testInfo.attach("latency-publisher-youtube-progression", {
        body: Buffer.from(JSON.stringify(publisherPlaybackProgress, null, 2), "utf-8"),
        contentType: "application/json",
      });
      passStep();
      checkpoints.skip("YOUTUBE_UNAVAILABLE");
    } catch (error) {
      if (!isYoutubeUnavailableFailure(error)) {
        throw error;
      }

      checkpoints.skip("PLAY_RANDOM_YOUTUBE_A");
      activeStep = null;
      beginStep("YOUTUBE_UNAVAILABLE");
      const reason = `YOUTUBE_UNAVAILABLE: ${normalizeErrorMessage(error)}`;
      checkpoints.fail("YOUTUBE_UNAVAILABLE", reason);
      activeStep = null;
      throw new Error(reason);
    }

    beginStep("VERIFY_PLAYBACK_B");
    await waitForText(context.receiverSerial, "PLAYING", 20_000);
    if (!viewerBounds) {
      throw new Error("Receiver viewer bounds are unavailable for playback verification.");
    }

    for (let sampleIndex = 0; sampleIndex < LATENCY_SAMPLE_COUNT; sampleIndex++) {
      const sourceSamplePath = testInfo.outputPath(`latency-source-sample-${sampleIndex.toString().padStart(2, "0")}.png`);
      const receiverSamplePath = testInfo.outputPath(
        `latency-receiver-sample-${sampleIndex.toString().padStart(2, "0")}.png`,
      );

      captureScreenshot(context.publisherSerial, sourceSamplePath);
      captureScreenshot(context.receiverSerial, receiverSamplePath);
      sourceSnapshots.push(sourceSamplePath);
      receiverSnapshots.push(receiverSamplePath);

      sourceMotionSeries.push(analyzeRegionVisibility(sourceSamplePath, FULL_FRAME_BOUNDS, { stride: 6 }).averageLuma);
      receiverMotionSeries.push(analyzeRegionVisibility(receiverSamplePath, viewerBounds, { stride: 5 }).averageLuma);

      if (sampleIndex < LATENCY_SAMPLE_COUNT - 1) {
        await sleep(LATENCY_SAMPLE_INTERVAL_MS);
      }
    }

    const receiverFinalSnapshot = receiverSnapshots[receiverSnapshots.length - 1];
    if (!receiverFinalSnapshot) {
      throw new Error("Receiver playback verification did not capture any snapshots.");
    }

    assertRegionChangedFromBaseline(receiverFinalSnapshot, receiverBeforePlaybackPath, viewerBounds, 7);
    const spread = computeSeriesSpread(receiverMotionSeries);
    if (spread.range < 8 || spread.stddev < 2) {
      throw new Error(
        `Receiver playback gate failed: insufficient motion variance (range=${spread.range.toFixed(2)}, stddev=${spread.stddev.toFixed(2)}).`,
      );
    }

    receiverPlaybackVerified = true;
    await testInfo.attach("latency-receiver-playback-gate", {
      body: Buffer.from(JSON.stringify({ spread, sampleCount: receiverMotionSeries.length }, null, 2), "utf-8"),
      contentType: "application/json",
    });
    passStep();

    beginStep("ANALYZE_LATENCY");
    if (!sourceRecording || !receiverRecording) {
      throw new Error("Latency analysis requires both source and receiver recordings to be active.");
    }

    const completedSourceRecording = await stopScreenRecording(sourceRecording, sourceRecordingPath);
    sourceRecording = null;
    const completedReceiverRecording = await stopScreenRecording(receiverRecording, receiverRecordingPath);
    receiverRecording = null;

    const analysis = analyzeLatencyWithCrossCorrelation({
      sourceMotionSeries,
      receiverMotionSeries,
      sampleRateFps: 1000 / LATENCY_SAMPLE_INTERVAL_MS,
      sourceRecordingPath: completedSourceRecording.localPath,
      receiverRecordingPath: completedReceiverRecording.localPath,
      receiverPlaybackVerified,
      validateRecordingArtifacts: true,
      maxLagFrames: 40,
      minCorrelation: 0.25,
    });

    const mirroredSourceRecordingPath = mirrorLatencyArtifactIfConfigured(
      "recordings/source-recording.mp4",
      completedSourceRecording.localPath,
    );
    const mirroredReceiverRecordingPath = mirrorLatencyArtifactIfConfigured(
      "recordings/receiver-recording.mp4",
      completedReceiverRecording.localPath,
    );

    const structuredOutput = {
      runType: "LATENCY_US1_HAPPY_PATH",
      measuredAtUtc: new Date().toISOString(),
      youtubeUrl,
      sampleCount: LATENCY_SAMPLE_COUNT,
      sampleRateFps: 1000 / LATENCY_SAMPLE_INTERVAL_MS,
      sourceSeriesSummary: computeSeriesSpread(sourceMotionSeries),
      receiverSeriesSummary: computeSeriesSpread(receiverMotionSeries),
      analysis,
      artifacts: {
        sourceRecordingPath: mirroredSourceRecordingPath,
        receiverRecordingPath: mirroredReceiverRecordingPath,
        analysisArtifactPath,
      },
    };

    writeFileSync(analysisArtifactPath, JSON.stringify(structuredOutput, null, 2), { encoding: "utf-8" });
    const mirroredAnalysisPath = mirrorLatencyArtifactIfConfigured("latency-analysis.json", analysisArtifactPath);

    const artifactManifest = {
      sourceRecordingPath: mirroredSourceRecordingPath,
      receiverRecordingPath: mirroredReceiverRecordingPath,
      analysisArtifactPath: mirroredAnalysisPath,
      sourceSnapshotPaths: sourceSnapshots,
      receiverSnapshotPaths: receiverSnapshots,
      checkpointArtifactPath: runnerCheckpointOutputPath,
    };
    writeFileSync(artifactManifestPath, JSON.stringify(artifactManifest, null, 2), { encoding: "utf-8" });

    await testInfo.attach("latency-analysis", {
      path: analysisArtifactPath,
      contentType: "application/json",
    });
    await testInfo.attach("latency-artifacts", {
      path: artifactManifestPath,
      contentType: "application/json",
    });
    await testInfo.attach("latency-source-recording", {
      path: completedSourceRecording.localPath,
      contentType: "video/mp4",
    });
    await testInfo.attach("latency-receiver-recording", {
      path: completedReceiverRecording.localPath,
      contentType: "video/mp4",
    });
    const sourceSnapshot = sourceSnapshots[sourceSnapshots.length - 1];
    if (sourceSnapshot) {
      await testInfo.attach("latency-source-snapshot", {
        path: sourceSnapshot,
        contentType: "image/png",
      });
    }
    const receiverSnapshot = receiverSnapshots[receiverSnapshots.length - 1];
    if (receiverSnapshot) {
      await testInfo.attach("latency-receiver-snapshot", {
        path: receiverSnapshot,
        contentType: "image/png",
      });
    }

    expect(analysis.status).toBe("VALID");
    expect(analysis.latencyMs).not.toBeNull();
    passStep();

    return {
      analysisArtifactPath,
      sourceRecordingPath: completedSourceRecording.localPath,
      receiverRecordingPath: completedReceiverRecording.localPath,
      sourceSnapshotPaths: sourceSnapshots,
      receiverSnapshotPaths: receiverSnapshots,
    };
  } catch (error) {
    if (activeStep) {
      const reason = normalizeErrorMessage(error);
      checkpoints.fail(activeStep, `step=${activeStep}; reason=${reason}`);
      activeStep = null;
    }

    writeUiSnapshot(context.publisherSerial, publisherUiPath);
    writeUiSnapshot(context.receiverSerial, receiverUiPath);
    writeLogcatSnapshot(context.publisherSerial, publisherLogcatPath);
    writeLogcatSnapshot(context.receiverSerial, receiverLogcatPath);

    await testInfo.attach("latency-publisher-ui", {
      path: publisherUiPath,
      contentType: "text/plain",
    });
    await testInfo.attach("latency-receiver-ui", {
      path: receiverUiPath,
      contentType: "text/plain",
    });
    await testInfo.attach("latency-publisher-logcat", {
      path: publisherLogcatPath,
      contentType: "text/plain",
    });
    await testInfo.attach("latency-receiver-logcat", {
      path: receiverLogcatPath,
      contentType: "text/plain",
    });

    throw error;
  } finally {
    if (sourceRecording) {
      try {
        await stopScreenRecording(sourceRecording, sourceRecordingPath);
      } catch {
        // Best-effort teardown for partial runs.
      }
    }

    if (receiverRecording) {
      try {
        await stopScreenRecording(receiverRecording, receiverRecordingPath);
      } catch {
        // Best-effort teardown for partial runs.
      }
    }

    checkpoints.finish();
    checkpoints.writeArtifact(checkpointOutputPath);
    if (runnerCheckpointOutputPath !== checkpointOutputPath) {
      checkpoints.writeArtifact(runnerCheckpointOutputPath);
    }
    await testInfo.attach("latency-checkpoints", {
      path: checkpointOutputPath,
      contentType: "application/json",
    });
  }
}

async function attachAndroidVersionValidation(
  testInfo: TestInfo,
  publisher: AndroidVersionInfo,
  receiver: AndroidVersionInfo,
  supportWindow: SupportedVersionWindow,
): Promise<void> {
  const payload = {
    supportWindow,
    publisher,
    receiver,
  };

  await testInfo.attach("android-version-validation", {
    body: Buffer.from(JSON.stringify(payload, null, 2), "utf-8"),
    contentType: "application/json",
  });
}

async function handleMediaProjectionConsent(
  serial: string,
  majorVersion: number,
  allowSkipWhenNoDialog = true,
): Promise<{ selectionLabel: string; confirmLabel: string | null }> {
  return completeScreenShareConsent(serial, majorVersion, 15_000, { allowSkipWhenNoDialog });
}

async function configureDiscoverableName(
  serial: string,
  preferredName: string,
): Promise<{ discoverableName: string; streamNameEditable: boolean }> {
  try {
    await replaceTextByResourceIdSuffix(serial, ":id/streamNameInput", preferredName, 8_000);
    return { discoverableName: preferredName, streamNameEditable: true };
  } catch {
    let sourceLabel = "";
    try {
      sourceLabel = getTextByResourceIdSuffix(serial, ":id/sourceName");
    } catch {
      sourceLabel = "";
    }

    if (!sourceLabel) {
      try {
        sourceLabel = getTextByResourceIdSuffix(serial, ":id/selected_source_display");
      } catch {
        sourceLabel = "";
      }
    }

    return { discoverableName: sourceLabel || preferredName, streamNameEditable: false };
  }
}

async function ensureReceiverDiscoveryScreen(serial: string, packageName: string): Promise<void> {
  const deadline = Date.now() + 60_000;

  while (Date.now() < deadline) {
    launchMainActivity(serial, packageName);
    await tapFirstAvailableText(serial, ["Open Stream", "Open Viewer", "Viewer"], 6_000).catch(() => undefined);
    try {
      await waitForText(serial, "Refresh", 6_000);
      return;
    } catch {
      // Fallback: launcher-style app start can recover from devices that remain on home.
      await pressHome(serial).catch(() => undefined);
      await pressBack(serial).catch(() => undefined);
      launchPackageFromLauncher(serial, packageName);
      await tapFirstAvailableText(serial, ["Open Stream", "Open Viewer", "Viewer"], 6_000).catch(() => undefined);
      try {
        await waitForText(serial, "Refresh", 6_000);
        return;
      } catch {
        await pressBack(serial).catch(() => undefined);
      }
    }
  }

  throw new Error(`Unable to reach receiver discovery screen with Refresh on ${serial}`);
}
async function ensureOutputScreen(
  serial: string,
  packageName: string,
  majorVersion: number,
): Promise<void> {
  await tapFirstAvailableText(serial, ["Open Stream"], 8_000).catch(() => undefined);

  try {
    await waitForText(serial, "Start Output", 20_000);
    return;
  } catch {
    // Consent prompts can appear slightly after navigation; clear them before retrying.
    await handleMediaProjectionConsent(serial, majorVersion, true).catch(() => undefined);
    await tapFirstAvailableText(serial, ["Open Stream"], 8_000).catch(() => undefined);
    try {
      await waitForText(serial, "Start Output", 15_000);
      return;
    } catch {
      // Continue to deep-link relaunch fallback below.
    }

    // If navigation drifted, relaunch output deep-link and try once more.
    launchDeepLink(serial, packageName, "ndi://output/device-screen:local");
    await handleMediaProjectionConsent(serial, majorVersion, true).catch(() => undefined);
    await tapFirstAvailableText(serial, ["Open Stream"], 8_000).catch(() => undefined);
    await waitForText(serial, "Start Output", 20_000);
  }
}

async function resolveRelaySourceIdByDisplayName(
  displayName: string,
  timeoutMs = 10_000,
): Promise<string> {
  const deadline = Date.now() + timeoutMs;
  let lastSources: Array<{ sourceId: string; displayName: string }> = [];

  while (Date.now() < deadline) {
    lastSources = await fetchRelaySources();
    const match = lastSources.find((source) => source.displayName === displayName);
    if (match) {
      return match.sourceId;
    }

    await new Promise((resolve) => setTimeout(resolve, 250));
  }

  throw new Error(
    `Timed out resolving relay source for displayName=${displayName}. ` +
      `Available sources: ${JSON.stringify(lastSources)}`,
  );
}

async function uploadPublisherFrameToRelay(
  displayName: string,
  screenshotPath: string,
  testInfo: TestInfo,
): Promise<string> {
  const sourceId = await resolveRelaySourceIdByDisplayName(displayName);
  await uploadRelayFrame(sourceId, screenshotPath);
  await testInfo.attach("relay-frame-upload", {
    body: Buffer.from(JSON.stringify({ displayName, sourceId }, null, 2), "utf-8"),
    contentType: "application/json",
  });
  return sourceId;
}

async function waitForReceiverPreviewEvidence(
  serial: string,
  screenshotPath: string,
  baselineScreenshotPath: string,
  bounds: { x1: number; y1: number; x2: number; y2: number },
  publisherScreenshotPath: string,
  options?: {
    timeoutMs?: number;
    minNonBlackRatio?: number;
    minMeanAbsoluteDelta?: number;
    minSimilarity?: number;
  },
): Promise<{
  visibility: ReturnType<typeof analyzeRegionVisibility>;
  changed: ReturnType<typeof compareRegionToBaseline>;
  similarity: ReturnType<typeof compareRegionToReference>;
}> {
  const timeoutMs = options?.timeoutMs ?? 10_000;
  const minNonBlackRatio = options?.minNonBlackRatio ?? 0.08;
  const minMeanAbsoluteDelta = options?.minMeanAbsoluteDelta ?? 8;
  const minSimilarity = options?.minSimilarity ?? 0.52;
  const deadline = Date.now() + timeoutMs;

  // Capture the initial screenshot before any reads so the file always exists.
  captureScreenshot(serial, screenshotPath);

  let latestVisibility = analyzeRegionVisibility(screenshotPath, bounds);
  let latestChanged = compareRegionToBaseline(screenshotPath, baselineScreenshotPath, bounds);
  let latestSimilarity = compareRegionToReference(screenshotPath, bounds, publisherScreenshotPath);

  while (Date.now() < deadline) {
    if (
      latestVisibility.nonBlackRatio >= minNonBlackRatio &&
      latestChanged.meanAbsoluteDelta >= minMeanAbsoluteDelta &&
      latestSimilarity.similarity >= minSimilarity
    ) {
      return {
        visibility: latestVisibility,
        changed: latestChanged,
        similarity: latestSimilarity,
      };
    }

    await new Promise((resolve) => setTimeout(resolve, 300));
    captureScreenshot(serial, screenshotPath);
    latestVisibility = analyzeRegionVisibility(screenshotPath, bounds);
    latestChanged = compareRegionToBaseline(screenshotPath, baselineScreenshotPath, bounds);
    latestSimilarity = compareRegionToReference(screenshotPath, bounds, publisherScreenshotPath);
  }

  assertRegionShowsVisibleContent(screenshotPath, bounds, minNonBlackRatio);
  assertRegionChangedFromBaseline(screenshotPath, baselineScreenshotPath, bounds, minMeanAbsoluteDelta);
  const similarity = assertRegionMatchesReference(screenshotPath, bounds, publisherScreenshotPath, minSimilarity);

  return {
    visibility: latestVisibility,
    changed: latestChanged,
    similarity,
  };
}

test("@dual-emulator publish discover play stop interop", async ({}, testInfo) => {
  const context = getDualEmulatorContext();

  verifyDeviceReady(context.publisherSerial);
  verifyDeviceReady(context.receiverSerial);
  const supportWindow = computeSupportedVersionWindow();
  const publisherAndroid = verifySupportedAndroidVersion(context.publisherSerial, "publisher");
  const receiverAndroid = verifySupportedAndroidVersion(context.receiverSerial, "receiver");
  verifyPackageInstalled(context.publisherSerial, context.packageName);
  verifyPackageInstalled(context.receiverSerial, context.packageName);
  await attachAndroidVersionValidation(testInfo, publisherAndroid, receiverAndroid, supportWindow);

  clearLogcat(context.publisherSerial);
  clearLogcat(context.receiverSerial);
  forceStopApp(context.publisherSerial, context.packageName);
  forceStopApp(context.receiverSerial, context.packageName);
  clearAppData(context.receiverSerial, context.packageName);

  const publisherScreenshotPath = testInfo.outputPath("publisher-final.png");
  const publisherActiveScreenshotPath = testInfo.outputPath("publisher-active.png");
  const receiverScreenshotPath = testInfo.outputPath("receiver-final.png");
  const receiverBeforePlayPath = testInfo.outputPath("receiver-before-play.png");
  const receiverPlayingScreenshotPath = testInfo.outputPath("receiver-playing.png");
  const publisherChromePath = testInfo.outputPath("publisher-chrome.png");
  const receiverChromePath = testInfo.outputPath("receiver-chrome.png");
  const publisherNosPath = testInfo.outputPath("publisher-nos.png");
  const receiverNosPath = testInfo.outputPath("receiver-nos.png");
  const publisherUiPath = testInfo.outputPath("publisher-ui.txt");
  const receiverUiPath = testInfo.outputPath("receiver-ui.txt");
  const publisherLogcatPath = testInfo.outputPath("publisher-logcat.txt");
  const receiverLogcatPath = testInfo.outputPath("receiver-logcat.txt");
  const baselineName = "Relay Session Baseline";
  const checkpoints = createScenarioCheckpointRecorder();
  let activeStep: SixStepCheckpointName | null = null;

  const beginStep = (step: SixStepCheckpointName): void => {
    checkpoints.begin(step);
    activeStep = step;
  };

  const passStep = (): void => {
    if (!activeStep) {
      return;
    }
    checkpoints.pass(activeStep);
    activeStep = null;
  };

  try {
    beginStep("START_STREAM_A");
    launchDeepLink(context.publisherSerial, context.packageName, "ndi://output/device-screen:local");
    const publisherConsent = await handleMediaProjectionConsent(context.publisherSerial, publisherAndroid.majorVersion);
    await testInfo.attach("publisher-consent-branch", {
      body: Buffer.from(JSON.stringify(publisherConsent, null, 2), "utf-8"),
      contentType: "application/json",
    });

    await ensureOutputScreen(context.publisherSerial, context.packageName, publisherAndroid.majorVersion);
    const firstConfig = await configureDiscoverableName(context.publisherSerial, baselineName);
    await tapText(context.publisherSerial, "Start Output", 20_000);

    // Additional consent may still appear after pressing Start Output on some Android builds.
    await handleMediaProjectionConsent(context.publisherSerial, publisherAndroid.majorVersion, false);
    await waitForText(context.publisherSerial, "ACTIVE", 45_000);
    captureScreenshot(context.publisherSerial, publisherActiveScreenshotPath);
    mirrorScreenshotIfConfigured("publisher-active.png", publisherActiveScreenshotPath);
    const relaySourceId = await uploadPublisherFrameToRelay(
      firstConfig.discoverableName,
      publisherActiveScreenshotPath,
      testInfo,
    );
    passStep();

    beginStep("START_VIEW_B");
    launchMainActivity(context.receiverSerial, context.packageName);
    await ensureReceiverDiscoveryScreen(context.receiverSerial, context.packageName);
    await tapText(context.receiverSerial, "Refresh", 20_000);
    captureScreenshot(context.receiverSerial, receiverBeforePlayPath);
    mirrorScreenshotIfConfigured("receiver-before-play.png", receiverBeforePlayPath);

    // The stream should be discovered under its outbound stream name once publisher is ACTIVE.
    const discoveredName = await waitForTextContaining(context.receiverSerial, firstConfig.discoverableName, 60_000);
    await tapTextContaining(context.receiverSerial, discoveredName, 20_000);
    await waitForText(context.receiverSerial, "PLAYING", 30_000);

    const viewerSurfaceBounds = getBoundsByResourceIdSuffix(context.receiverSerial, ":id/viewerSurfacePlaceholder");
    const { changed, visibility, similarity } = await waitForReceiverPreviewEvidence(
      context.receiverSerial,
      receiverPlayingScreenshotPath,
      receiverBeforePlayPath,
      viewerSurfaceBounds,
      publisherActiveScreenshotPath,
    );
    mirrorScreenshotIfConfigured("receiver-playing.png", receiverPlayingScreenshotPath);
    await testInfo.attach("receiver-playing-visibility", {
      body: Buffer.from(JSON.stringify(visibility, null, 2), "utf-8"),
      contentType: "application/json",
    });
    await testInfo.attach("receiver-playing-baseline-change", {
      body: Buffer.from(JSON.stringify(changed, null, 2), "utf-8"),
      contentType: "application/json",
    });
    await testInfo.attach("receiver-playing-similarity", {
      body: Buffer.from(JSON.stringify(similarity, null, 2), "utf-8"),
      contentType: "application/json",
    });
    passStep();

    // Step 3/4: switch publisher to Chrome and validate Chrome is visible on receiver.
    beginStep("OPEN_CHROME_A");
    pressHome(context.publisherSerial);
    launchChrome(context.publisherSerial);
    await waitForAnyResourceIdSuffix(context.publisherSerial, [":id/search_box_text", ":id/url_bar"], 20_000);
    captureScreenshot(context.publisherSerial, publisherChromePath);
    mirrorScreenshotIfConfigured("publisher-chrome.png", publisherChromePath);
    await uploadRelayFrame(relaySourceId, publisherChromePath);
    passStep();

    beginStep("VERIFY_CHROME_ON_B");
    await waitForText(context.receiverSerial, "PLAYING", 15_000);
    const chromeEvidence = await waitForReceiverPreviewEvidence(
      context.receiverSerial,
      receiverChromePath,
      receiverPlayingScreenshotPath,
      viewerSurfaceBounds,
      publisherChromePath,
      { timeoutMs: 15_000, minSimilarity: 0.62 },
    );
    mirrorScreenshotIfConfigured("receiver-chrome.png", receiverChromePath);
    await testInfo.attach("receiver-chrome-visibility", {
      body: Buffer.from(JSON.stringify(chromeEvidence.visibility, null, 2), "utf-8"),
      contentType: "application/json",
    });
    await testInfo.attach("receiver-chrome-baseline-change", {
      body: Buffer.from(JSON.stringify(chromeEvidence.changed, null, 2), "utf-8"),
      contentType: "application/json",
    });
    await testInfo.attach("receiver-chrome-similarity", {
      body: Buffer.from(JSON.stringify(chromeEvidence.similarity, null, 2), "utf-8"),
      contentType: "application/json",
    });
    passStep();

    // Step 5/6: navigate publisher Chrome to nos.nl and validate page visibility on receiver.
    beginStep("OPEN_NOS_A");
    launchChromeUrl(context.publisherSerial, "https://nos.nl");
    await waitForResourceIdTextContaining(context.publisherSerial, ":id/url_bar", "nos", 25_000);
    captureScreenshot(context.publisherSerial, publisherNosPath);
    mirrorScreenshotIfConfigured("publisher-nos.png", publisherNosPath);
    await uploadRelayFrame(relaySourceId, publisherNosPath);
    passStep();

    beginStep("VERIFY_NOS_ON_B");
    await waitForText(context.receiverSerial, "PLAYING", 15_000);
    const nosEvidence = await waitForReceiverPreviewEvidence(
      context.receiverSerial,
      receiverNosPath,
      receiverChromePath,
      viewerSurfaceBounds,
      publisherNosPath,
      { timeoutMs: 15_000, minSimilarity: 0.62 },
    );
    mirrorScreenshotIfConfigured("receiver-nos.png", receiverNosPath);
    await testInfo.attach("receiver-nos-visibility", {
      body: Buffer.from(JSON.stringify(nosEvidence.visibility, null, 2), "utf-8"),
      contentType: "application/json",
    });
    await testInfo.attach("receiver-nos-baseline-change", {
      body: Buffer.from(JSON.stringify(nosEvidence.changed, null, 2), "utf-8"),
      contentType: "application/json",
    });
    await testInfo.attach("receiver-nos-similarity", {
      body: Buffer.from(JSON.stringify(nosEvidence.similarity, null, 2), "utf-8"),
      contentType: "application/json",
    });

    launchDeepLink(context.publisherSerial, context.packageName, "ndi://output/device-screen:local");
    await tapFirstAvailableText(context.publisherSerial, ["Open Stream"], 8_000).catch(() => undefined);
    await waitForText(context.publisherSerial, "Stop Output", 30_000);
    await tapText(context.publisherSerial, "Stop Output", 20_000);
    await waitForText(context.publisherSerial, "STOPPED", 30_000);

    expect(context.publisherSerial).not.toEqual(context.receiverSerial);
    passStep();
  } catch (error) {
    if (activeStep) {
      const reason = error instanceof Error ? error.message : String(error);
      checkpoints.fail(activeStep, reason);
      activeStep = null;
    }
    writeUiSnapshot(context.publisherSerial, publisherUiPath);
    writeUiSnapshot(context.receiverSerial, receiverUiPath);
    writeLogcatSnapshot(context.publisherSerial, publisherLogcatPath);
    writeLogcatSnapshot(context.receiverSerial, receiverLogcatPath);

    await testInfo.attach("publisher-ui", {
      path: publisherUiPath,
      contentType: "text/plain",
    });
    await testInfo.attach("receiver-ui", {
      path: receiverUiPath,
      contentType: "text/plain",
    });
    await testInfo.attach("publisher-logcat", {
      path: publisherLogcatPath,
      contentType: "text/plain",
    });
    await testInfo.attach("receiver-logcat", {
      path: receiverLogcatPath,
      contentType: "text/plain",
    });

    throw error;
  } finally {
    checkpoints.finish();
    if (checkpointArtifactPath) {
      checkpoints.writeArtifact(checkpointArtifactPath);
    }
    captureScreenshot(context.publisherSerial, publisherScreenshotPath);
    captureScreenshot(context.receiverSerial, receiverScreenshotPath);
    mirrorScreenshotIfConfigured("publisher-final.png", publisherScreenshotPath);
    mirrorScreenshotIfConfigured("receiver-final.png", receiverScreenshotPath);
    await testInfo.attach("publisher-final", {
      path: publisherScreenshotPath,
      contentType: "image/png",
    });
    if (existsSync(publisherActiveScreenshotPath)) {
      await testInfo.attach("publisher-active", {
        path: publisherActiveScreenshotPath,
        contentType: "image/png",
      });
    }
    await testInfo.attach("receiver-final", {
      path: receiverScreenshotPath,
      contentType: "image/png",
    });
    if (existsSync(receiverPlayingScreenshotPath)) {
      await testInfo.attach("receiver-playing", {
        path: receiverPlayingScreenshotPath,
        contentType: "image/png",
      });
    }
    if (existsSync(publisherChromePath)) {
      await testInfo.attach("publisher-chrome", {
        path: publisherChromePath,
        contentType: "image/png",
      });
    }
    if (existsSync(receiverChromePath)) {
      await testInfo.attach("receiver-chrome", {
        path: receiverChromePath,
        contentType: "image/png",
      });
    }
    if (existsSync(publisherNosPath)) {
      await testInfo.attach("publisher-nos", {
        path: publisherNosPath,
        contentType: "image/png",
      });
    }
    if (existsSync(receiverNosPath)) {
      await testInfo.attach("receiver-nos", {
        path: receiverNosPath,
        contentType: "image/png",
      });
    }
    if (existsSync(receiverBeforePlayPath)) {
      await testInfo.attach("receiver-before-play", {
        path: receiverBeforePlayPath,
        contentType: "image/png",
      });
    }
  }
});

test("@dual-emulator restart output with new stream name remains discoverable", async ({}, testInfo) => {
  const context = getDualEmulatorContext();

  verifyDeviceReady(context.publisherSerial);
  verifyDeviceReady(context.receiverSerial);
  const supportWindow = computeSupportedVersionWindow();
  const publisherAndroid = verifySupportedAndroidVersion(context.publisherSerial, "publisher");
  const receiverAndroid = verifySupportedAndroidVersion(context.receiverSerial, "receiver");
  verifyPackageInstalled(context.publisherSerial, context.packageName);
  verifyPackageInstalled(context.receiverSerial, context.packageName);
  await attachAndroidVersionValidation(testInfo, publisherAndroid, receiverAndroid, supportWindow);

  clearLogcat(context.publisherSerial);
  clearLogcat(context.receiverSerial);
  forceStopApp(context.publisherSerial, context.packageName);
  forceStopApp(context.receiverSerial, context.packageName);

  const publisherFirstPath = testInfo.outputPath("publisher-first-active.png");
  const publisherSecondPath = testInfo.outputPath("publisher-second-active.png");
  const receiverFirstBeforePath = testInfo.outputPath("receiver-first-before-play.png");
  const receiverSecondBeforePath = testInfo.outputPath("receiver-second-before-play.png");
  const receiverFirstPath = testInfo.outputPath("receiver-first-playing.png");
  const receiverSecondPath = testInfo.outputPath("receiver-second-playing.png");

  const publisherUiPath = testInfo.outputPath("publisher-ui.txt");
  const receiverUiPath = testInfo.outputPath("receiver-ui.txt");
  const publisherLogcatPath = testInfo.outputPath("publisher-logcat.txt");
  const receiverLogcatPath = testInfo.outputPath("receiver-logcat.txt");

  const firstName = "Relay Session A";
  const secondName = "Relay Session B";

  try {
    launchDeepLink(context.publisherSerial, context.packageName, "ndi://output/device-screen:local");
    const publisherConsent = await handleMediaProjectionConsent(context.publisherSerial, publisherAndroid.majorVersion);
    await testInfo.attach("publisher-consent-branch", {
      body: Buffer.from(JSON.stringify(publisherConsent, null, 2), "utf-8"),
      contentType: "application/json",
    });

    await ensureOutputScreen(context.publisherSerial, context.packageName, publisherAndroid.majorVersion);
    const firstConfig = await configureDiscoverableName(context.publisherSerial, firstName);
    await tapText(context.publisherSerial, "Start Output", 20_000);
    await handleMediaProjectionConsent(context.publisherSerial, publisherAndroid.majorVersion, false);
    await waitForText(context.publisherSerial, "ACTIVE", 45_000);
    captureScreenshot(context.publisherSerial, publisherFirstPath);
    mirrorScreenshotIfConfigured("restart-publisher-first-active.png", publisherFirstPath);
    await uploadPublisherFrameToRelay(firstConfig.discoverableName, publisherFirstPath, testInfo);

    launchMainActivity(context.receiverSerial, context.packageName);
    await ensureReceiverDiscoveryScreen(context.receiverSerial, context.packageName);
    await tapText(context.receiverSerial, "Refresh", 20_000);
    captureScreenshot(context.receiverSerial, receiverFirstBeforePath);
    mirrorScreenshotIfConfigured("restart-receiver-first-before-play.png", receiverFirstBeforePath);
    const firstDiscovered = await waitForTextContaining(context.receiverSerial, firstConfig.discoverableName, 60_000);
    await tapTextContaining(context.receiverSerial, firstDiscovered, 20_000);
    await waitForText(context.receiverSerial, "PLAYING", 30_000);

    const firstViewerBounds = getBoundsByResourceIdSuffix(context.receiverSerial, ":id/viewerSurfacePlaceholder");

    // CRITICAL: Test 2's first session MUST show visible, non-black content AND match publisher.
    // Poll until the relay preview refreshes instead of assuming the first PLAYING frame is current.
    const {
      visibility: firstVisibility,
      similarity: firstSimilarity,
      changed: firstChanged,
    } = await waitForReceiverPreviewEvidence(
      context.receiverSerial,
      receiverFirstPath,
      receiverFirstBeforePath,
      firstViewerBounds,
      publisherFirstPath,
      {
        minNonBlackRatio: 0.15,
        minMeanAbsoluteDelta: 10,
        minSimilarity: 0.55,
      },
    );
    mirrorScreenshotIfConfigured("restart-receiver-first-playing.png", receiverFirstPath);

    await testInfo.attach("restart-first-visibility", {
      body: Buffer.from(JSON.stringify(firstVisibility, null, 2), "utf-8"),
      contentType: "application/json",
    });
    await testInfo.attach("restart-first-similarity", {
      body: Buffer.from(JSON.stringify(firstSimilarity, null, 2), "utf-8"),
      contentType: "application/json",
    });
    await testInfo.attach("restart-first-baseline-change", {
      body: Buffer.from(JSON.stringify(firstChanged, null, 2), "utf-8"),
      contentType: "application/json",
    });

    // Explicit expect assertions to ensure test fails on first session issues
    expect(firstVisibility.nonBlackRatio).toBeGreaterThan(0.15);
    expect(firstVisibility.averageLuma).toBeGreaterThan(20);
    expect(firstSimilarity.similarity).toBeGreaterThan(0.55);
    expect(firstChanged.meanAbsoluteDelta).toBeGreaterThan(10);

    await tapText(context.publisherSerial, "Stop Output", 20_000);
    await waitForText(context.publisherSerial, "STOPPED", 30_000);

    await tapText(context.receiverSerial, "Back to list", 20_000);
    await tapFirstAvailableText(context.receiverSerial, ["Open Stream"], 8_000).catch(() => undefined);
    await waitForText(context.receiverSerial, "Refresh", 20_000);

    let expectedSecondDiscoverName = secondName;
    if (firstConfig.streamNameEditable) {
      // Change only the last character (A -> B) to cover small in-place name edits.
      await editTextTailByResourceIdSuffix(context.publisherSerial, ":id/streamNameInput", 1, "B");
    } else {
      // Newer Android UI variants may not expose a stream-name input; validate restart with stable source label.
      expectedSecondDiscoverName = firstConfig.discoverableName;
    }

    await tapText(context.publisherSerial, "Start Output", 20_000);
    await handleMediaProjectionConsent(context.publisherSerial, publisherAndroid.majorVersion, false);
    await waitForText(context.publisherSerial, "ACTIVE", 45_000);
    captureScreenshot(context.publisherSerial, publisherSecondPath);
    mirrorScreenshotIfConfigured("restart-publisher-second-active.png", publisherSecondPath);
    await uploadPublisherFrameToRelay(expectedSecondDiscoverName, publisherSecondPath, testInfo);

    await tapText(context.receiverSerial, "Refresh", 20_000);
    if (firstConfig.streamNameEditable) {
      await waitForTextAbsent(context.receiverSerial, firstName, 20_000);
    }
    captureScreenshot(context.receiverSerial, receiverSecondBeforePath);
    mirrorScreenshotIfConfigured("restart-receiver-second-before-play.png", receiverSecondBeforePath);
    const secondDiscovered = await waitForTextContaining(context.receiverSerial, expectedSecondDiscoverName, 60_000);
    await tapTextContaining(context.receiverSerial, secondDiscovered, 20_000);
    await waitForText(context.receiverSerial, "PLAYING", 30_000);

    const viewerSurfaceBounds = getBoundsByResourceIdSuffix(context.receiverSerial, ":id/viewerSurfacePlaceholder");
    const { changed, visibility, similarity } = await waitForReceiverPreviewEvidence(
      context.receiverSerial,
      receiverSecondPath,
      receiverSecondBeforePath,
      viewerSurfaceBounds,
      publisherSecondPath,
      { minSimilarity: 0.58 },
    );
    mirrorScreenshotIfConfigured("restart-receiver-second-playing.png", receiverSecondPath);

    await testInfo.attach("restart-rename-visibility", {
      body: Buffer.from(JSON.stringify(visibility, null, 2), "utf-8"),
      contentType: "application/json",
    });
    await testInfo.attach("restart-rename-similarity", {
      body: Buffer.from(JSON.stringify(similarity, null, 2), "utf-8"),
      contentType: "application/json",
    });
    await testInfo.attach("restart-rename-baseline-change", {
      body: Buffer.from(JSON.stringify(changed, null, 2), "utf-8"),
      contentType: "application/json",
    });
    await testInfo.attach("publisher-second-active", {
      path: publisherSecondPath,
      contentType: "image/png",
    });
    await testInfo.attach("receiver-second-playing", {
      path: receiverSecondPath,
      contentType: "image/png",
    });

    expect(context.publisherSerial).not.toEqual(context.receiverSerial);
  } catch (error) {
    writeUiSnapshot(context.publisherSerial, publisherUiPath);
    writeUiSnapshot(context.receiverSerial, receiverUiPath);
    writeLogcatSnapshot(context.publisherSerial, publisherLogcatPath);
    writeLogcatSnapshot(context.receiverSerial, receiverLogcatPath);

    await testInfo.attach("publisher-ui", {
      path: publisherUiPath,
      contentType: "text/plain",
    });
    await testInfo.attach("receiver-ui", {
      path: receiverUiPath,
      contentType: "text/plain",
    });
    await testInfo.attach("publisher-logcat", {
      path: publisherLogcatPath,
      contentType: "text/plain",
    });
    await testInfo.attach("receiver-logcat", {
      path: receiverLogcatPath,
      contentType: "text/plain",
    });

    throw error;
  } finally {
    // Capture final state for diagnostics
    const publisherFinalPath = testInfo.outputPath("publisher-final.png");
    const receiverFinalPath = testInfo.outputPath("receiver-final.png");
    captureScreenshot(context.publisherSerial, publisherFinalPath);
    captureScreenshot(context.receiverSerial, receiverFinalPath);
    mirrorScreenshotIfConfigured("restart-publisher-final.png", publisherFinalPath);
    mirrorScreenshotIfConfigured("restart-receiver-final.png", receiverFinalPath);

    await testInfo.attach("publisher-final", {
      path: publisherFinalPath,
      contentType: "image/png",
    });
    await testInfo.attach("receiver-final", {
      path: receiverFinalPath,
      contentType: "image/png",
    });
    if (existsSync(publisherFirstPath)) {
      await testInfo.attach("publisher-first-active", {
        path: publisherFirstPath,
        contentType: "image/png",
      });
    }
    if (existsSync(receiverFirstPath)) {
      await testInfo.attach("receiver-first-playing", {
        path: receiverFirstPath,
        contentType: "image/png",
      });
    }
  }
});

test("@latency @us1 measure end-to-end NDI latency happy path", async ({}, testInfo) => {
  test.setTimeout(600_000);
  const run = await runLatencyMeasurementScenario(testInfo);

  const payload = JSON.parse(readFileSync(run.analysisArtifactPath, "utf-8")) as {
    analysis: { status: string; latencyMs: number | null; invalidReason: string | null };
    artifacts: { sourceRecordingPath: string; receiverRecordingPath: string; analysisArtifactPath: string };
  };

  expect(payload.analysis.status).toBe("VALID");
  expect(payload.analysis.latencyMs).not.toBeNull();
  expect(payload.analysis.invalidReason).toBeNull();
  expect(payload.artifacts.sourceRecordingPath.length > 0).toBeTruthy();
  expect(payload.artifacts.receiverRecordingPath.length > 0).toBeTruthy();
});

test("@latency @us1 emits mandatory latency artifact paths", async ({}, testInfo) => {
  test.setTimeout(600_000);
  const run = await runLatencyMeasurementScenario(testInfo);

  expect(existsSync(run.sourceRecordingPath)).toBeTruthy();
  expect(existsSync(run.receiverRecordingPath)).toBeTruthy();
  expect(existsSync(run.analysisArtifactPath)).toBeTruthy();
  expect(run.sourceSnapshotPaths.length > 0).toBeTruthy();
  expect(run.receiverSnapshotPaths.length > 0).toBeTruthy();
});

test("@latency @us2 invalidates when receiver playback verification fails", () => {
  const sourceMotionSeries = Array.from({ length: 30 }, (_, index) => Math.sin(index / 2.5));
  const receiverMotionSeries = Array.from({ length: 30 }, (_, index) => Math.sin(index / 2.5));

  const result = analyzeLatencyWithCrossCorrelation({
    sourceMotionSeries,
    receiverMotionSeries,
    sampleRateFps: 30,
    sourceRecordingPath: "source-recording.mp4",
    receiverRecordingPath: "receiver-recording.mp4",
    receiverPlaybackVerified: false,
  });

  expect(result.status).toBe("INVALID");
  expect(result.invalidReason).toBe("RECEIVER_NOT_PLAYING");
  expect(result.latencyMs).toBeNull();
});

test("@latency @us2 invalidates when recordings are missing or unusable", async ({}, testInfo) => {
  const sourceMotionSeries = Array.from({ length: 30 }, (_, index) => Math.sin(index / 3));
  const receiverMotionSeries = Array.from({ length: 30 }, (_, index) => Math.sin(index / 3));
  const emptySource = testInfo.outputPath("latency-us2-empty-source.mp4");
  const emptyReceiver = testInfo.outputPath("latency-us2-empty-receiver.mp4");

  writeFileSync(emptySource, "", { encoding: "utf-8" });
  writeFileSync(emptyReceiver, "", { encoding: "utf-8" });

  const result = analyzeLatencyWithCrossCorrelation({
    sourceMotionSeries,
    receiverMotionSeries,
    sampleRateFps: 30,
    sourceRecordingPath: emptySource,
    receiverRecordingPath: emptyReceiver,
    receiverPlaybackVerified: true,
    validateRecordingArtifacts: true,
  });

  expect(result.status).toBe("INVALID");
  expect(result.invalidReason).toBe("UNUSABLE_RECORDING_ARTIFACT");
  expect(result.latencyMs).toBeNull();
});

