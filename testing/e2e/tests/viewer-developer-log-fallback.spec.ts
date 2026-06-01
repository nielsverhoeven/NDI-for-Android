import { test } from '@playwright/test';
import { assertLogShowsFallbackMessage } from '../helpers/viewer-log-assertions';
import { renderViewerDeveloperLogLine } from './support/android-ui-driver';

test('US3 contract: fallback text shown when no valid configured addresses remain', async () => {
  const renderedLogLine = await renderViewerDeveloperLogLine(true, []);

  if (!renderedLogLine) {
    throw new Error('Expected rendered viewer log output for fallback scenario');
  }

  await assertLogShowsFallbackMessage(renderedLogLine);
});
