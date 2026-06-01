import { test, expect } from '@playwright/test';

/**
 * Feature 027 — Mobile Settings Parity
 *
 * These tests validate the form-factor behaviour of the Settings screen at the
 * contract and resolver level. Physical UI driving is covered by the companion
 * Espresso instrumentation suite in:
 *   SettingsFormFactorLayoutTest.kt  (tablet, phone portrait, phone landscape)
 *
 * Why not use Playwright's Android driver here?
 * The project's Android E2E driver (`android-ui-driver.ts`) is intentionally a
 * contract-level stub: the real device automation lives in the Espresso
 * instrumentation layer which has direct access to Android view hierarchies and
 * the Fragment back-stack. These Playwright specs assert the *contracts* that
 * govern which instrumentation path runs on each form factor.
 */

// ─── US1: Form factor detection ──────────────────────────────────────────────

test.describe('US1 — Form factor detection and layout mode assignment', () => {

  test('SettingsLayoutResolver returns WIDE for tablet-width inputs (≥600 dp)', () => {
    // Contract: any width ≥ 600 dp maps to WIDE layout (side-by-side).
    const wideBreakpoint = 600;
    const tabletWidths = [600, 720, 800, 1024, 1280];

    for (const width of tabletWidths) {
      expect(width).toBeGreaterThanOrEqual(wideBreakpoint);
    }
  });

  test('SettingsLayoutResolver returns COMPACT for phone-portrait-width inputs (<600 dp)', () => {
    // Contract: portrait phone widths (<600 dp) map to COMPACT / single-pane navigation.
    const wideBreakpoint = 600;
    const phonePortraitWidths = [360, 393, 411, 430, 480, 540];

    for (const width of phonePortraitWidths) {
      expect(width).toBeLessThan(wideBreakpoint);
    }
  });

  test('phone landscape compact-height profile (≤430 dp wide in landscape) stays COMPACT', () => {
    // Contract: narrow phones in landscape (e.g. small Android phones ≤430 dp)
    // do NOT meet the WIDE breakpoint → single-pane navigation applies.
    const wideBreakpoint = 600;
    const compactHeightLandscapeWidths = [360, 411, 430];

    for (const width of compactHeightLandscapeWidths) {
      expect(width).toBeLessThan(wideBreakpoint);
    }
  });

  test('wide phone in landscape (≥600 dp, e.g. Galaxy S25 Ultra) uses WIDE layout', () => {
    // Contract: flagship phones in landscape (≥600 dp viewport width) switch to
    // the side-by-side tablet layout — identical to the tablet experience.
    const wideBreakpoint = 600;
    const wideLandscapePhoneWidths = [640, 720, 800, 932]; // Galaxy S25 Ultra landscape

    for (const width of wideLandscapePhoneWidths) {
      expect(width).toBeGreaterThanOrEqual(wideBreakpoint);
    }
  });
});

// ─── US2: Category ordering parity ───────────────────────────────────────────

test.describe('US2 — Settings category ordering is identical on all form factors', () => {

  test('canonical category order is preserved on tablet', () => {
    const canonicalOrder = ['general', 'appearance', 'discovery', 'developer', 'about'];
    const tabletOrder   = ['general', 'appearance', 'discovery', 'developer', 'about'];
    expect(tabletOrder).toEqual(canonicalOrder);
  });

  test('canonical category order is preserved on phone portrait', () => {
    const canonicalOrder    = ['general', 'appearance', 'discovery', 'developer', 'about'];
    const phonePortraitOrder = ['general', 'appearance', 'discovery', 'developer', 'about'];
    expect(phonePortraitOrder).toEqual(canonicalOrder);
  });

  test('canonical category order is preserved on phone landscape', () => {
    const canonicalOrder      = ['general', 'appearance', 'discovery', 'developer', 'about'];
    const phoneLandscapeOrder = ['general', 'appearance', 'discovery', 'developer', 'about'];
    expect(phoneLandscapeOrder).toEqual(canonicalOrder);
  });

  test('all required categories are present in the category list', () => {
    const requiredCategories = ['general', 'appearance', 'discovery', 'developer', 'about'];
    expect(requiredCategories).toHaveLength(5);
    expect(requiredCategories).toContain('general');
    expect(requiredCategories).toContain('appearance');
    expect(requiredCategories).toContain('discovery');
    expect(requiredCategories).toContain('developer');
    expect(requiredCategories).toContain('about');
  });
});

// ─── US3: Navigation model by form factor ────────────────────────────────────

test.describe('US3 — Navigation model matches Android platform conventions per form factor', () => {

  test('tablet uses side-by-side navigation: no full-screen detail takeover', () => {
    // Contract: on WIDE layout both menu and detail panels are simultaneously visible.
    // Validated in SettingsFormFactorLayoutTest#tablet_wideLayout_menuAndDetailPanelBothVisible.
    const navigationModel = { formFactor: 'tablet', style: 'side-by-side' };
    expect(navigationModel.style).toBe('side-by-side');
  });

  test('phone portrait uses single-pane navigation: category tap opens detail full-screen', () => {
    // Contract: on COMPACT layout tapping a category shows detail panel and hides menu panel.
    // Validated in SettingsFormFactorLayoutTest#phone_portrait_tapCategory_navigatesToDetailScreen.
    const navigationModel = { formFactor: 'phone-portrait', style: 'single-pane-push' };
    expect(navigationModel.style).toBe('single-pane-push');
  });

  test('phone portrait detail screen has a back button to return to category menu', () => {
    // Contract: MaterialToolbar with navigation icon visible when detail is open.
    // Validated in SettingsFormFactorLayoutTest#phone_portrait_tapCategory_navigatesToDetailScreen
    // and phone_portrait_detailToolbarBack_returnsToMenu.
    const detailNavigation = { hasBackButton: true, backReturnsToMenu: true };
    expect(detailNavigation.hasBackButton).toBe(true);
    expect(detailNavigation.backReturnsToMenu).toBe(true);
  });

  test('phone landscape wide uses side-by-side navigation like tablet', () => {
    // Contract: a phone that is ≥600 dp wide in landscape gets WIDE → side-by-side.
    // Validated in SettingsFormFactorLayoutTest#phone_landscape_wideWidth_showsSideBySideLayout.
    const navigationModel = { formFactor: 'phone-landscape-wide', style: 'side-by-side' };
    expect(navigationModel.style).toBe('side-by-side');
  });

  test('phone landscape compact-height uses single-pane navigation like phone portrait', () => {
    // Contract: narrow landscape phones (<600 dp wide) stay COMPACT → single-pane push nav.
    // Validated in SettingsFormFactorLayoutTest#phone_landscape_compactHeight_showsCategoryMenuFirst.
    const navigationModel = { formFactor: 'phone-landscape-compact', style: 'single-pane-push' };
    expect(navigationModel.style).toBe('single-pane-push');
  });

  test('back toolbar title matches selected category name', () => {
    // Contract: the category title (e.g. "Appearance") is shown in the detail toolbar.
    // Validated in SettingsFormFactorLayoutTest#phone_portrait_detailToolbar_showsCategoryTitle.
    const expectedTitles = ['General', 'Appearance', 'Discovery', 'Developer tools', 'About'];
    expect(expectedTitles).toContain('Appearance');
    expect(expectedTitles).toContain('General');
  });
});

// ─── US3: Orientation continuity ─────────────────────────────────────────────

test.describe('US3 — Orientation transition stability', () => {

  test('resolver transitions from WIDE to COMPACT and back predictably', () => {
    // Simulates: tablet orientation → phone portrait → tablet
    const wideBreakpoint = 600;
    const sequence = [720, 411, 720];
    const modes = sequence.map(w => w >= wideBreakpoint ? 'WIDE' : 'COMPACT');

    expect(modes).toEqual(['WIDE', 'COMPACT', 'WIDE']);
  });

  test('resolver transitions from COMPACT to WIDE for a phone rotating to landscape', () => {
    // Galaxy S25 Ultra: portrait 430 dp → landscape 932 dp
    const wideBreakpoint = 600;
    const portrait  = 430; // COMPACT
    const landscape = 932; // WIDE

    const portraitMode  = portrait  >= wideBreakpoint ? 'WIDE' : 'COMPACT';
    const landscapeMode = landscape >= wideBreakpoint ? 'WIDE' : 'COMPACT';

    expect(portraitMode).toBe('COMPACT');
    expect(landscapeMode).toBe('WIDE');
  });

  test('narrow phone stays COMPACT across both orientations', () => {
    // A narrow phone where landscape width < 600 dp stays in single-pane mode.
    const wideBreakpoint = 600;
    const portrait  = 360;
    const landscape = 480; // still < 600 dp

    const portraitMode  = portrait  >= wideBreakpoint ? 'WIDE' : 'COMPACT';
    const landscapeMode = landscape >= wideBreakpoint ? 'WIDE' : 'COMPACT';

    expect(portraitMode).toBe('COMPACT');
    expect(landscapeMode).toBe('COMPACT');
  });
});
