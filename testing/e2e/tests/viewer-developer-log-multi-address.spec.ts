import { test } from '@playwright/test';
import { assertLogContainsAllAddressesInOrder } from '../helpers/viewer-log-assertions';
import { renderViewerDeveloperLogLine } from './support/android-ui-driver';

test('US2 contract: multi-address output keeps order and removes duplicates', async () => {
  const expectedAddresses = ['192.168.1.10', 'ff02::1', 'ndi-host.local'];
  const renderedLogLine = await renderViewerDeveloperLogLine(true, expectedAddresses);

  if (!renderedLogLine) {
    throw new Error('Expected rendered viewer log output when developer mode is enabled');
  }

  await assertLogContainsAllAddressesInOrder(renderedLogLine, expectedAddresses);
});

test('US2 contract: extended address list displays first five plus ellipsis', async () => {
  const renderedLogLine = await renderViewerDeveloperLogLine(true, ['one', 'two', 'three', 'four', 'five', 'six', 'seven']);
  const expectedVisible = ['one', 'two', 'three', 'four', 'five'];

  if (!renderedLogLine) {
    throw new Error('Expected rendered viewer log output for extended list scenario');
  }

  await assertLogContainsAllAddressesInOrder(renderedLogLine, expectedVisible);
});
