import { Page } from '@playwright/test';

/**
 * Navigate to Settings and open the Discovery Servers submenu.
 * Assumes the app is already launched and visible.
 */
export async function openDiscoveryServersSubmenu(page: Page): Promise<void> {
  // Open Settings via bottom nav or menu icon
  const settingsButton = page.getByContentDescription('Open settings').or(
    page.getByText('Settings'),
  );
  await settingsButton.first().click();

  // Tap the Discovery Servers row/button to open the submenu
  await page.getByText('Discovery Servers').click();
}

/**
 * Fill in and submit the add-server form.
 * Leave portInput as '' to use the default port (5959).
 */
export async function addDiscoveryServer(
  page: Page,
  hostInput: string,
  portInput: string,
): Promise<void> {
  // Tap the Add / + button to open the add form (or it may already be visible)
  const addButton = page.getByRole('button', { name: /add.*server|^\+$/i });
  if (await addButton.isVisible()) {
    await addButton.click();
  }

  // Fill hostname field
  const hostField = page.getByPlaceholder(/hostname|host or ip/i).or(
    page.getByLabel(/hostname|host or ip/i),
  );
  await hostField.fill(hostInput);

  // Fill port field if provided
  const portField = page.getByPlaceholder(/port/i).or(
    page.getByLabel(/port/i),
  );
  await portField.fill(portInput);

  // Submit
  const saveButton = page.getByRole('button', { name: /save|add/i }).last();
  await saveButton.click();
}

/**
 * Verify a server entry (e.g. "ndi-server.local:5959") appears in the server list.
 */
export async function verifyServerInList(page: Page, serverLabel: string): Promise<void> {
  await page.getByText(serverLabel).waitFor({ state: 'visible', timeout: 5000 });
}

/**
 * Toggle a server's enabled state.
 * @param serverLabel  The full label displayed in the row, e.g. "ndi-server.local:5959"
 * @param enabled      Target enabled state (true = on, false = off)
 */
export async function toggleServer(
  page: Page,
  serverLabel: string,
  enabled: boolean,
): Promise<void> {
  const row = page.getByText(serverLabel).locator('..');
  const toggle = row.getByRole('switch');
  const currentState = await toggle.isChecked();
  if (currentState !== enabled) {
    await toggle.click();
  }
}

/**
 * Tap the edit affordance on a server row and fill in new values.
 * @param currentLabel   Current displayed label, e.g. "old-host.local:5959"
 * @param newHost        New hostname or IP
 * @param newPort        New port ('' for default)
 */
export async function editServer(
  page: Page,
  currentLabel: string,
  newHost: string,
  newPort: string,
): Promise<void> {
  const row = page.getByText(currentLabel).locator('..');
  await row.getByRole('button', { name: /edit/i }).click();

  const hostField = page.getByPlaceholder(/hostname|host or ip/i).or(
    page.getByLabel(/hostname|host or ip/i),
  );
  await hostField.fill(newHost);

  const portField = page.getByPlaceholder(/port/i).or(
    page.getByLabel(/port/i),
  );
  await portField.fill(newPort);

  const saveButton = page.getByRole('button', { name: /save/i }).last();
  await saveButton.click();
}

/**
 * Remove a server by tapping its delete affordance and confirming.
 * @param serverLabel  The full label, e.g. "ndi-server.local:5959"
 */
export async function removeServer(page: Page, serverLabel: string): Promise<void> {
  const row = page.getByText(serverLabel).locator('..');
  await row.getByRole('button', { name: /delete|remove/i }).click();

  // Confirm deletion dialog if shown
  const confirmButton = page.getByRole('button', { name: /confirm|yes|delete/i });
  if (await confirmButton.isVisible({ timeout: 1000 })) {
    await confirmButton.click();
  }
}
