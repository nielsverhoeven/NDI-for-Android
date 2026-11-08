# #467 — native Entry/Picker fields at the 48 dp minimum: device evidence

Screenshots for acceptance item 4 of issue #467 ("fields reachable with one tap in both
orientations, no clipped text at font scale 1.3"), taken on 2026-11-08 from the branch head with
the implicit Entry/Picker `MinimumHeightRequest="48"` styles (`Styles.xaml`) and the scrolling
edit-server dialog. Half-size JPEGs; the CI runs linked from the PR hold the measured heights.

| Device (local AVD, API 35) | Window | Shots |
|---|---|---|
| Pixel 6 (compact, 420 dpi, 411×914 dp) | portrait + landscape | `phone-*.jpg` |
| Pixel C (tablet, 320 dpi, 1280×900 dp) | landscape + portrait | `tablet-*.jpg` |

- `*-discovery-*` — Settings > Discovery with the add-server form (display name / host / port
  Entries) and one server row.
- `*-edit-server-dialog-*` — the edit-server dialog. `phone-04b-…-landscape-scrolled` shows the
  dialog scrolled to Cancel/Save in phone landscape, where the 48 dp Entries made the card taller
  than the 411 dp window; before this change the buttons were unreachable there.
- `*-output-*` — the Stream tab's Stream Name Entry and Video Input Picker (inside its #375
  input-panel Border, which grows with the Picker and keeps the chevron centred).
- `*-fs1.3-*` — the same screens at system font scale 1.3: no clipped text.

The Galaxy Tab A9+ / S21 confirmation named in the issue remains for the owner.
