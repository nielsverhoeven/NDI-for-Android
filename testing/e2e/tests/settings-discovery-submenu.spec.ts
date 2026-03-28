import { test, expect, Page } from '@playwright/test';
import {
  openDiscoveryServersSubmenu,
  addDiscoveryServer,
  verifyServerInList,
  toggleServer,
  removeServer,
  editServer,
} from './support/discovery-server-helpers';

// ---------------------------------------------------------------------------
// US1 — Add Discovery Servers from Settings
// ---------------------------------------------------------------------------

test.describe('Discovery Server Settings — US1: Add', () => {
  test('opens discovery server submenu from Settings', async ({ page }) => {
    // T019: failing Playwright e2e — opens submenu
    await openDiscoveryServersSubmenu(page);
    await expect(page.getByText('Discovery Servers')).toBeVisible();
  });

  test('adds host-only server, port defaults to 5959', async ({ page }) => {
    // T019: failing Playwright e2e — host-only save defaults to port 5959
    await openDiscoveryServersSubmenu(page);
    await addDiscoveryServer(page, 'ndi-server.local', '');
    await verifyServerInList(page, 'ndi-server.local:5959');
  });

  test('adds server with explicit port', async ({ page }) => {
    await openDiscoveryServersSubmenu(page);
    await addDiscoveryServer(page, 'ndi-server.local', '5961');
    await verifyServerInList(page, 'ndi-server.local:5961');
  });

  test('blocks save with no hostname, shows validation message', async ({ page }) => {
    await openDiscoveryServersSubmenu(page);
    await addDiscoveryServer(page, '', '5959');
    await expect(page.getByText(/host.*required|must.*hostname/i)).toBeVisible();
  });
});

// ---------------------------------------------------------------------------
// US2 — Manage Multiple Discovery Servers
// ---------------------------------------------------------------------------

test.describe('Discovery Server Settings — US2: Multiple Servers', () => {
  test('multiple servers persist after app relaunch', async ({ page }) => {
    // T031: failing Playwright e2e — multiple server persistence
    await openDiscoveryServersSubmenu(page);
    await addDiscoveryServer(page, 'alpha.local', '');
    await addDiscoveryServer(page, 'beta.local', '5960');
    await addDiscoveryServer(page, 'gamma.local', '6000');
    // Simulate relaunch
    await page.reload();
    await openDiscoveryServersSubmenu(page);
    await verifyServerInList(page, 'alpha.local:5959');
    await verifyServerInList(page, 'beta.local:5960');
    await verifyServerInList(page, 'gamma.local:6000');
  });

  test('rejects duplicate server with clear feedback', async ({ page }) => {
    await openDiscoveryServersSubmenu(page);
    await addDiscoveryServer(page, 'alpha.local', '');
    await addDiscoveryServer(page, 'alpha.local', '5959');
    await expect(page.getByText(/already exists|duplicate/i)).toBeVisible();
  });
});

// ---------------------------------------------------------------------------
// US4 — Edit and Remove Discovery Servers
// ---------------------------------------------------------------------------

test.describe('Discovery Server Settings — US4: Edit/Delete/Reorder', () => {
  test('edits server port and persists after relaunch', async ({ page }) => {
    // T058: failing Playwright e2e — edit flow
    await openDiscoveryServersSubmenu(page);
    await addDiscoveryServer(page, 'edit-me.local', '5959');
    await editServer(page, 'edit-me.local:5959', 'edit-me.local', '7000');
    await verifyServerInList(page, 'edit-me.local:7000');
    await page.reload();
    await openDiscoveryServersSubmenu(page);
    await verifyServerInList(page, 'edit-me.local:7000');
  });

  test('removes server and does not reappear after relaunch', async ({ page }) => {
    // T058: failing Playwright e2e — delete flow
    await openDiscoveryServersSubmenu(page);
    await addDiscoveryServer(page, 'remove-me.local', '');
    await removeServer(page, 'remove-me.local:5959');
    await expect(page.getByText('remove-me.local:5959')).not.toBeVisible();
    await page.reload();
    await openDiscoveryServersSubmenu(page);
    await expect(page.getByText('remove-me.local:5959')).not.toBeVisible();
  });

  test('blocks duplicate update with clear validation message', async ({ page }) => {
    await openDiscoveryServersSubmenu(page);
    await addDiscoveryServer(page, 'original.local', '5959');
    await addDiscoveryServer(page, 'other.local', '5959');
    await editServer(page, 'other.local:5959', 'original.local', '5959');
    await expect(page.getByText(/already exists|duplicate/i)).toBeVisible();
  });
});

// ---------------------------------------------------------------------------
// US3 — Enable and Disable Individual Servers
// ---------------------------------------------------------------------------

test.describe('Discovery Server Settings — US3: Toggle', () => {
  test('per-server toggle state persists after app restart', async ({ page }) => {
    // T041: failing Playwright e2e — toggle persistence
    await openDiscoveryServersSubmenu(page);
    await addDiscoveryServer(page, 'toggle-me.local', '');
    await toggleServer(page, 'toggle-me.local:5959', false);
    await page.reload();
    await openDiscoveryServersSubmenu(page);
    // Verify the server is still disabled after restart
    const serverRow = page.getByTestId('discovery-server-toggle-me.local:5959');
    await expect(serverRow.getByRole('switch')).not.toBeChecked();
  });

  test('disabled server is excluded from failover, enabled servers are used in order', async ({
    page,
  }) => {
    await openDiscoveryServersSubmenu(page);
    await addDiscoveryServer(page, 'first.local', '');
    await addDiscoveryServer(page, 'second.local', '');
    await toggleServer(page, 'first.local:5959', false);
    // Runtime will only see second.local — this is validated by unit/instrumentation tests
    // Playwright validates the enabled state is correctly reflected in the list
    const firstRow = page.getByText('first.local:5959').locator('..');
    await expect(firstRow.getByRole('switch')).not.toBeChecked();
    const secondRow = page.getByText('second.local:5959').locator('..');
    await expect(secondRow.getByRole('switch')).toBeChecked();
  });
});
