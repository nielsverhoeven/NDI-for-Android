import { expect, test } from "@playwright/test";
import { existsSync } from "node:fs";
import {
  getDualEmulatorContext,
  verifyDeviceReady,
  verifyPackageInstalled,
} from "./support/android-device-fixtures";
import {
  captureScreenshot,
  clearLogcat,
  forceStopApp,
  editTextTailByResourceIdSuffix,
  getBoundsByResourceIdSuffix,
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
  assertRegionChangedFromBaseline,
  assertRegionMatchesReference,
  assertRegionShowsVisibleContent,
} from "./support/visual-assertions";

test("@dual-emulator publish discover play stop interop", async ({}, testInfo) => {
  const context = getDualEmulatorContext();

  verifyDeviceReady(context.publisherSerial);
  verifyDeviceReady(context.receiverSerial);
  verifyPackageInstalled(context.publisherSerial, context.packageName);
  verifyPackageInstalled(context.receiverSerial, context.packageName);

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
    await waitForText(context.publisherSerial, "Start Output", 20_000);
    await replaceTextByResourceIdSuffix(context.publisherSerial, ":id/streamNameInput", baselineName);
    await tapText(context.publisherSerial, "Start Output", 20_000);

    // Consent may be skipped if already granted by the system from a previous run.
    await tapFirstAvailableText(context.publisherSerial, ["Start now", "Allow", "Start", "Continue"], 6_000).catch(() => undefined);
    await waitForText(context.publisherSerial, "ACTIVE", 45_000);
    captureScreenshot(context.publisherSerial, publisherActiveScreenshotPath);

    launchMainActivity(context.receiverSerial, context.packageName);
    await waitForText(context.receiverSerial, "Refresh", 20_000);
    await tapText(context.receiverSerial, "Refresh", 20_000);
    captureScreenshot(context.receiverSerial, receiverBeforePlayPath);

    // The stream should be discovered under its outbound stream name once publisher is ACTIVE.
    const discoveredName = await waitForTextContaining(context.receiverSerial, baselineName, 60_000);
    await tapTextContaining(context.receiverSerial, discoveredName, 20_000);
    await waitForText(context.receiverSerial, "PLAYING", 30_000);

    captureScreenshot(context.receiverSerial, receiverPlayingScreenshotPath);
    const viewerSurfaceBounds = getBoundsByResourceIdSuffix(context.receiverSerial, ":id/viewerSurfacePlaceholder");
    const changed = assertRegionChangedFromBaseline(receiverPlayingScreenshotPath, receiverBeforePlayPath, viewerSurfaceBounds);
    const visibility = assertRegionShowsVisibleContent(receiverPlayingScreenshotPath, viewerSurfaceBounds);
    const similarity = assertRegionMatchesReference(
      receiverPlayingScreenshotPath,
      viewerSurfaceBounds,
      publisherActiveScreenshotPath,
    );
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
  verifyPackageInstalled(context.publisherSerial, context.packageName);
  verifyPackageInstalled(context.receiverSerial, context.packageName);

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
    await waitForText(context.publisherSerial, "Start Output", 20_000);
    await replaceTextByResourceIdSuffix(context.publisherSerial, ":id/streamNameInput", firstName);
    await tapText(context.publisherSerial, "Start Output", 20_000);
    await tapFirstAvailableText(context.publisherSerial, ["Start now", "Allow", "Start", "Continue"], 6_000).catch(() => undefined);
    await waitForText(context.publisherSerial, "ACTIVE", 45_000);
    captureScreenshot(context.publisherSerial, publisherFirstPath);

    launchMainActivity(context.receiverSerial, context.packageName);
    await waitForText(context.receiverSerial, "Refresh", 20_000);
    await tapText(context.receiverSerial, "Refresh", 20_000);
    captureScreenshot(context.receiverSerial, receiverFirstBeforePath);
    const firstDiscovered = await waitForTextContaining(context.receiverSerial, firstName, 60_000);
    await tapTextContaining(context.receiverSerial, firstDiscovered, 20_000);
    await waitForText(context.receiverSerial, "PLAYING", 30_000);
    captureScreenshot(context.receiverSerial, receiverFirstPath);

    const firstViewerBounds = getBoundsByResourceIdSuffix(context.receiverSerial, ":id/viewerSurfacePlaceholder");

    // CRITICAL: Test 2's first session MUST show visible, non-black content AND match publisher.
    // If either fails, the test must fail - do not proceed to session 2.
    const firstVisibility = assertRegionShowsVisibleContent(receiverFirstPath, firstViewerBounds, 0.15);
    const firstSimilarity = assertRegionMatchesReference(receiverFirstPath, firstViewerBounds, publisherFirstPath, 0.55);
    const firstChanged = assertRegionChangedFromBaseline(receiverFirstPath, receiverFirstBeforePath, firstViewerBounds, 10);

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
    await waitForText(context.receiverSerial, "Refresh", 20_000);

    // Change only the last character (A -> B) to cover small in-place name edits.
    await editTextTailByResourceIdSuffix(context.publisherSerial, ":id/streamNameInput", 1, "B");
    await tapText(context.publisherSerial, "Start Output", 20_000);
    await waitForText(context.publisherSerial, "ACTIVE", 45_000);
    captureScreenshot(context.publisherSerial, publisherSecondPath);

    await tapText(context.receiverSerial, "Refresh", 20_000);
    await waitForTextAbsent(context.receiverSerial, firstName, 20_000);
    captureScreenshot(context.receiverSerial, receiverSecondBeforePath);
    const secondDiscovered = await waitForTextContaining(context.receiverSerial, secondName, 60_000);
    await tapTextContaining(context.receiverSerial, secondDiscovered, 20_000);
    await waitForText(context.receiverSerial, "PLAYING", 30_000);

    captureScreenshot(context.receiverSerial, receiverSecondPath);
    const viewerSurfaceBounds = getBoundsByResourceIdSuffix(context.receiverSerial, ":id/viewerSurfacePlaceholder");
    const changed = assertRegionChangedFromBaseline(receiverSecondPath, receiverSecondBeforePath, viewerSurfaceBounds);
    const visibility = assertRegionShowsVisibleContent(receiverSecondPath, viewerSurfaceBounds);
    const similarity = assertRegionMatchesReference(receiverSecondPath, viewerSurfaceBounds, publisherSecondPath, 0.58);

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

