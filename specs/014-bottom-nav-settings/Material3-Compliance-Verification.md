# Material 3 Compliance Verification - Bottom Nav Settings

## Scope
- Feature: 014-bottom-nav-settings
- Surfaces: Home, Stream, View, Settings
- Components: BottomNavigationView item for `Settings`, Settings app bar title/header

## Visual System Checks
- [ ] Colors: selected and unselected icon/text colors are from Material 3 color roles and match app theme.
- [ ] Dark mode: settings bottom-nav icon/text contrast meets Material guidance and remains readable.
- [ ] Typography: bottom-nav labels use Material 3 type scale defaults without ad-hoc overrides.
- [ ] Iconography: settings tab icon style and optical weight match existing home/stream/view icons.
- [ ] Ripple/interaction: tab press feedback is visible and consistent with Material 3 state layers.

## Layout and Spacing Checks
- [ ] Bottom-nav item spacing is unchanged across all 4 tabs on phone width.
- [ ] Nav rail spacing/alignment is preserved on tablet width.
- [ ] Settings header/title remains visible and readable while Settings is active.
- [ ] No clipping/truncation for `Settings` label in portrait or landscape.

## Behavior Checks
- [ ] Selecting Settings highlights Settings and opens the settings screen.
- [ ] Selecting Home/Stream/View from Settings leaves Settings and updates selected state.
- [ ] Rapid tab switching keeps selected-state synchronized.
- [ ] Rotation while in Settings does not crash and retains selected-state synchronization.
- [ ] No top-right settings affordance is visible on source-list, viewer, output, or settings screens.

## Evidence Links
- Validation log: `test-results/bottom-nav-settings-validation.md`
- Playwright specs:
  - `testing/e2e/tests/settings-navigation-source-list.spec.ts`
  - `testing/e2e/tests/settings-navigation-viewer.spec.ts`
  - `testing/e2e/tests/settings-navigation-output.spec.ts`
