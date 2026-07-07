# #279 Responsive Layouts — Device Validation Evidence

Emulator: `maui_test_35` (Pixel-6-class, x86_64, API 36 image), 1080×2400 @ 420dpi.
Profiles simulated via `wm size`/`wm density`. Debug APK from `feature/279-phase4-responsive-layouts`.

## Matrix

| Check | Screenshot | Result |
|---|---|---|
| ~6.5" portrait (411dp, Compact) → bottom tabs, single-pane | `phone65-portrait.png` | ✅ |
| Landscape 914dp (Expanded) → left rail | `phone65-landscape-check.png` | ✅ |
| Expanded View tab → **two-pane** (source list 2* + viewer pane 3*, SKCanvas + tally border) | `expanded-view-twopane-v3.png` | ✅ |
| Rotate back to portrait → tabs restored, **View tab selection preserved**, pane collapsed | `compact-portrait-view.png` | ✅ |
| ~5" phone (720×1280 @ 320 = 360dp Compact) → tabs, no clipping | `phone5-portrait.png` | ✅ |
| Install / launch / rotate / resize stability | crash buffer empty throughout; PID stable | ✅ |

## Defects found by this validation (fixed in the same branch)

1. **Manifest never consumed (latent, pre-existing)**: the csproj lacked `<AndroidManifest>`, so
   `Platforms/Android/AndroidManifest.xml` was silently ignored and the APK shipped with only
   INTERNET. All discovery (multicast), foreground-service, camera, mic and notification
   permissions were missing on every previous build. Fixed via explicit `<AndroidManifest>`
   property + manifest modernization (removed deprecated `package` attr / `<uses-sdk>`).
   Verified live: multicast error gone ("Connected to NDI network"), POST_NOTIFICATIONS prompt
   appears, `dumpsys package` shows all 11 permissions.
2. **Size-class feed unreliable**: `Shell.OnSizeAllocated` did not fire on-device, so the
   Expanded two-pane never engaged. Fixed by feeding `IWindowSizeClassService` from
   `DeviceDisplay.MainDisplayInfo` (px→dp via density) in `AndroidOrientationBridge` on launch
   and every configuration change.
3. **NDI on non-ARM ABIs**: `libndi.so` ships arm64/armeabi only; on x86_64 the first P/Invoke
   threw `DllNotFoundException`. `NdiRuntime.EnsureInitialized` now catches it and soft-disables
   NDI (app fully usable otherwise).

## Not covered here (separate gates)
- Live NDI receive/send against a real network source (#277/#278 acceptance; requires an ARM
  device or an NDI sender + ARM emulator image) — including the HX-decode verification.
