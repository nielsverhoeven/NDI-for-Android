# Feature Spec — Viewer Control Deck (fixed layout, no scrolling)

**Issue:** #342
**Branch:** `feature/342-viewer-control-deck`
**Status:** Decided (owner comment 2026-09-04) — ready for implementation planning
**Wireframes:** https://claude.ai/code/artifact/e7f93acd-b3c9-458f-93cc-683a564b596a

---

## Problem

With VISCA PTZ controls (#339) merged, the viewer's control stack no longer fits under the
video on the Galaxy Tab A9+ (1280×800 dp, landscape, two-pane): ~600 dp of controls need to fit
in ~430 dp available, so the PTZ pad, presets, and Stop button require scrolling. Scrolling to
reach camera controls during an operator PTZ session is unacceptable.

## Decision (Niels, 2026-09-04) — wireframe **B, with A in full-screen**

Four layouts were sketched (A overlay, B fixed deck, C tabbed strip, D side strip). Decision:
**B as the default (normal) viewer layout, with A's overlay reserved for full-screen only.**
Rationale: an operator driving PTZ wants quality/audio/Stop and the camera pad visible
*simultaneously*, without covering the video — exactly what a fixed, non-scrolling two-column
deck gives. Full screen already has an auto-hiding overlay from #338; A's d-pad-on-video belongs
there, not in the normal layout.

### Normal viewer (tablet two-pane, and the pushed phone/tablet-portrait `ViewerPage`) — wireframe B

- Video stays on top (unchanged — existing fixed 240 dp `SKCanvasView`, tally border kept).
- Below it: a **control deck of fixed height ≈200 dp that never scrolls**.
- On hosts where `IWindowSizeClassService.Current` is **Medium or Expanded** (≥600 dp): deck is a
  **two-column Grid** — **Playback column** (status line; Smooth/Balanced/High quality segmented
  buttons; audio toggle; full-screen toggle; primary **Stop**; Retry/Cancel/Reconnect replace the
  quality row while reconnecting) and **Camera column** (d-pad ▲◄AF►▼ 3×3 grid of 48 dp buttons;
  vertical zoom rocker T/W of two 48 dp buttons beside it; preset grid of 8 buttons (2 rows × 4
  columns) at 48 dp each — tap recalls, long-press (≥600 ms) stores with a short confirmation;
  endpoint status chip opening the existing `PtzEndpointPanel` dialog).
- **Camera column only shown when `IsPtzControlActive`**; otherwise Playback spans the full deck
  width — no empty column.
- On hosts where `IWindowSizeClassService.Current` is **Compact** (<600 dp: phones in portrait,
  and phones in landscape below the Medium threshold): the deck becomes a **bottom sheet**
  overlay with a drag handle and two MD3-style secondary tabs, **"Weergave" / "PTZ"**,
  half-expanded by default, draggable/tappable to expand. The "PTZ" tab is hidden when
  `!IsPtzControlActive` (auto-falls back to "Weergave").
- The tablet **in portrait** sits at the Medium window-width class (~800 dp) and uses the pushed
  `ViewerPage` (single column, no source list pane) — per the rule above this is **Medium → deck**,
  not the bottom sheet, since the full window width is available to the pushed page.
- The preset number `Entry` + separate Store/Recall buttons are **removed**. Presets are now
  addressed by a fixed 1–8 button grid: tap = recall, long-press = store.
- All touch targets ≥48×48 dp, 8 dp spacing (tightened locally in the camera cluster where noted
  in plan.md), `DynamicResource` colors only.

### Full screen (existing modal from #338) — wireframe A

- Replace the current bottom control stack with an **overlay**: d-pad card bottom-left (16 dp
  from the edges, semi-transparent), zoom rocker card bottom-right, preset chips top-left
  (1–8, tap = recall only — store stays a deck/sheet action), and a slim **48 dp bottom toolbar**
  (endpoint status chip, quality segmented buttons, audio toggle, full-screen toggle, Stop, **⋮**
  overflow that opens the endpoint dialog).
- Reuses the existing auto-hide/tap-to-show/double-tap-to-toggle behavior (`AreControlsVisible`,
  `IsControlsOverlayVisible`, `ShowControlsOverlayCommand`, `NotifyControlInteraction()` — all
  already implemented by #338, unchanged here).
- Video center stays free; overlay pieces use `{DynamicResource ScrimBackground}`; tally border
  remains visible (unchanged full-bleed video border from #338).

## Acceptance Criteria (from issue #342, binding)

- [ ] Tablet landscape two-pane: with a VISCA endpoint saved, every control (status, quality,
      audio, full-screen, Stop, d-pad, zoom, 8 presets, endpoint chip) is visible without
      scrolling at 1280×800 dp; no control overlaps the video.
- [ ] Without PTZ (NDI source without PTZ, no endpoint): the deck shows only the playback column,
      no empty camera column.
- [ ] Compact width (phone portrait, or tablet portrait pushed `ViewerPage` — see breakpoint rule
      above, this only applies below 600 dp): bottom sheet with tabs; PTZ tab shows d-pad, zoom,
      presets, endpoint chip without scrolling.
- [ ] Full screen: overlay per wireframe A; controls hide after 3 s and return on tap; d-pad and
      zoom remain operable while visible; tally border visible.
- [ ] Presets: tap recalls, long-press stores (with a short confirmation); the preset entry +
      Store/Recall buttons are removed.
- [ ] All existing unit tests pass; new tests for ViewModel changes (preset recall/store
      parameterized commands, camera column visibility, store confirmation status text).
- [ ] Device verification on the Galaxy Tab A9+ in landscape and portrait against
      `tools/ViscaMockCamera`.

## Scope

### In scope
- `ViewerView.xaml(.cs)` layout rework: video unchanged, deck/sheet/full-screen-overlay as three
  mutually exclusive regions.
- Four new `ContentView`s: `PlaybackControlsView`, `CameraControlsView`, `ViewerControlDeck`,
  `ViewerControlSheet`, `FullScreenControlsOverlay` (five — see plan.md §1 for the exact split).
- `ViewerViewModel.Ptz.cs`: parameterized preset commands, preset status confirmation message.
- Deck-vs-sheet selection wired from the existing `IWindowSizeClassService` (no new service).
- Removal of `PtzPanelView.xaml(.cs)` (superseded) and the old preset `Entry`/Store/Recall UI.

### Out of scope / explicit non-goals
- Changing the video's own size/aspect behavior (still a fixed 240 dp `SKCanvasView`, unchanged
  from #338/#339) — this feature only reworks what sits *below or over* the video.
- A new width-measurement service. The deck-vs-sheet rule uses the existing **window** size
  class (`IWindowSizeClassService`), not the `ViewerView`'s own measured width. **Known
  limitation**: the embedded two-pane on `SourceListPage` only appears at the Expanded window
  class (>840 dp) with a 2\*/3\* column split (`SourceListPage.xaml.cs`) — at the low end of
  Expanded (just over 840 dp) the pane could be as narrow as ~500 dp, tighter than the reference
  1280×800 dp tablet this spec is verified against. This is accepted per the issue's explicit
  breakpoint instruction; not a regression to fix in this feature.
- CommunityToolkit.Maui's `BottomSheet` control — not referenced in `NdiForAndroid.csproj`, and
  the issue instructs against adding new NuGet packages. The sheet is a hand-built overlay (see
  plan.md §5).
- Any change to `INdiViewerBridge`, `IPtzController`, VISCA transport/encoding, or NDI bridge
  contracts.

## Companion Docs

- Depends on / builds on: `docs/features/ptz-visca-endpoint/` (#339), `docs/features/viewer-fullscreen/` (#338).
- Technical plan: `docs/features/viewer-control-deck/plan.md`
- Task breakdown: `docs/features/viewer-control-deck/tasks.md`
