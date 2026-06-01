import { test } from '@playwright/test';
import { assertLogContainsAddress } from '../helpers/viewer-log-assertions';
import { renderViewerConnectionLogWithAddress } from './support/android-ui-driver';

test('US1 contract: single configured address replaces redacted token in viewer logs', async () => {
  const configuredAddress = '192.168.1.10';
  const renderedLogLine = await renderViewerConnectionLogWithAddress(true, [configuredAddress]);

  if (!renderedLogLine) {
    throw new Error('Expected rendered viewer log line when developer mode is enabled');
  }

  await assertLogContainsAddress(renderedLogLine, configuredAddress);
});

test('US1 contract: developer mode OFF suppresses configured-address output', async ({}) => {
  const renderedLogLine = await renderViewerConnectionLogWithAddress(false, ['192.168.1.10']);

  if (renderedLogLine !== null) {
    throw new Error('Expected no configured-address output when developer mode is OFF');
  }
});
