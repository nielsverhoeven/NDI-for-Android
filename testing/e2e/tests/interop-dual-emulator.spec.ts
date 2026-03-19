import { expect, test, type TestInfo } from "@playwright/test";
import { copyFileSync, existsSync, mkdirSync } from "node:fs";
import { join } from "node:path";
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
  clearLogcat,
  completeScreenShareConsent,
  forceStopApp,
  editTextTailByResourceIdSuffix,
  getBoundsByResourceIdSuffix,
  getTextByResourceIdSuffix,
  launchDeepLink,
  launchMainActivity,
  replaceTextByResourceIdSuffix,
  tapFirstAvailableText,
  tapText,
  tapTextContaining,
  waitForText,
  waitForTextAbsent,
  waitForTextContaining,
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

const externalScreenshotDir = process.env.DUAL_EMULATOR_SCREENSHOT_DIR;

function mirrorScreenshotIfConfigured(fileName: string, sourcePath: string): void {
  if (!externalScreenshotDir || !existsSync(sourcePath)) {
    return;
  }

  mkdirSync(externalScreenshotDir, { recursive: true });
  copyFileSync(sourcePath, join(externalScreenshotDir, fileName));
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

async function ensureOutputScreen(
  serial: string,
  packageName: string,
): Promise<void> {
  await tapFirstAvailableText(serial, ["Open Stream"], 8_000).catch(() => undefined);

  try {
    await waitForText(serial, "Start Output", 20_000);
    return;
  } catch {
    // If navigation drifted, relaunch output deep-link and try once more.
    launchDeepLink(serial, packageName, "ndi://output/device-screen:local");
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

  const publisherScreenshotPath = testInfo.outputPath("publisher-final.png");
  const publisherActiveScreenshotPath = testInfo.outputPath("publisher-active.png");
  const receiverScreenshotPath = testInfo.outputPath("receiver-final.png");
  const receiverBeforePlayPath = testInfo.outputPath("receiver-before-play.png");
  const receiverPlayingScreenshotPath = testInfo.outputPath("receiver-playing.png");
  const publisherUiPath = testInfo.outputPath("publisher-ui.txt");
  const receiverUiPath = testInfo.outputPath("receiver-ui.txt");
  const publisherLogcatPath = testInfo.outputPath("publisher-logcat.txt");
  const receiverLogcatPath = testInfo.outputPath("receiver-logcat.txt");
  const baselineName = "Relay Session Baseline";

  try {
    launchDeepLink(context.publisherSerial, context.packageName, "ndi://output/device-screen:local");
    const publisherConsent = await handleMediaProjectionConsent(context.publisherSerial, publisherAndroid.majorVersion);
    await testInfo.attach("publisher-consent-branch", {
      body: Buffer.from(JSON.stringify(publisherConsent, null, 2), "utf-8"),
      contentType: "application/json",
    });

    await ensureOutputScreen(context.publisherSerial, context.packageName);
    const firstConfig = await configureDiscoverableName(context.publisherSerial, baselineName);
    await tapText(context.publisherSerial, "Start Output", 20_000);

    // Additional consent may still appear after pressing Start Output on some Android builds.
    await handleMediaProjectionConsent(context.publisherSerial, publisherAndroid.majorVersion, false);
    await waitForText(context.publisherSerial, "ACTIVE", 45_000);
    captureScreenshot(context.publisherSerial, publisherActiveScreenshotPath);
    mirrorScreenshotIfConfigured("publisher-active.png", publisherActiveScreenshotPath);
    await uploadPublisherFrameToRelay(firstConfig.discoverableName, publisherActiveScreenshotPath, testInfo);

    launchMainActivity(context.receiverSerial, context.packageName);
    await tapFirstAvailableText(context.receiverSerial, ["Open Stream"], 8_000).catch(() => undefined);
    await waitForText(context.receiverSerial, "Refresh", 20_000);
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

    await tapText(context.publisherSerial, "Stop Output", 20_000);
    await waitForText(context.publisherSerial, "STOPPED", 30_000);

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

    await ensureOutputScreen(context.publisherSerial, context.packageName);
    const firstConfig = await configureDiscoverableName(context.publisherSerial, firstName);
    await tapText(context.publisherSerial, "Start Output", 20_000);
    await handleMediaProjectionConsent(context.publisherSerial, publisherAndroid.majorVersion, false);
    await waitForText(context.publisherSerial, "ACTIVE", 45_000);
    captureScreenshot(context.publisherSerial, publisherFirstPath);
    mirrorScreenshotIfConfigured("restart-publisher-first-active.png", publisherFirstPath);
    await uploadPublisherFrameToRelay(firstConfig.discoverableName, publisherFirstPath, testInfo);

    launchMainActivity(context.receiverSerial, context.packageName);
    await tapFirstAvailableText(context.receiverSerial, ["Open Stream"], 8_000).catch(() => undefined);
    await waitForText(context.receiverSerial, "Refresh", 20_000);
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

