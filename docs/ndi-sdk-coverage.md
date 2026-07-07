<!-- Last updated: 2026-07-07 -->

# NDI SDK Coverage — What This App Implements

Per-capability status of the NDI SDK surface in NDI for Android, verified against the
live code on this branch. The app P/Invokes the **bundled NDI SDK ANDROID 6.3.1.0**
(`libndi.so`, `arm64-v8a` + `armeabi-v7a`). On non-ARM ABIs (x86/x86_64 emulators,
some Chromebooks) there is no native library; `NdiRuntime.EnsureInitialized()` catches
`DllNotFoundException` and the app runs with NDI features soft-disabled instead of crashing.

Research grounding: [docs/research/ndi-sdk-capability-matrix.md](research/ndi-sdk-capability-matrix.md)
(SDK-side analysis) and [docs/research/legacy-bridge-analysis.md](research/legacy-bridge-analysis.md).
Architecture detail: [docs/architecture.md](architecture.md).

**Status legend**: ✅ Implemented · 🟡 Partially implemented · ⛔ Out of scope (with explicit reason)

| Capability | Status | Where in the code | Notes |
|---|---|---|---|
| Initialize / lifecycle | ✅ Implemented | `src/MauiApp/NdiBridge/NdiRuntime.cs` (`EnsureInitialized`/`ReleaseHandle`), P/Invoke in `src/MauiApp/NdiBridge/Interop/NdiNativeMethods.cs` | Handle-counted init/teardown; `NDIlib_initialize` failure (non-NEON CPU) and missing `libndi.so` (non-ARM ABI) degrade gracefully — callers never crash. |
| Find / discovery (mDNS) | ✅ Implemented | `src/MauiApp/NdiBridge/NdiDiscoveryBridge.cs` | `NDIlib_find_create_v2` + `find_wait_for_sources` + `find_get_current_sources` with one long-lived finder per mode config. Android prerequisites held: `NsdManager` (`src/MauiApp/Platforms/Android/Services/AndroidNsdBootstrap.cs`) + `MulticastLock` (`AndroidMulticastLockService.cs`). |
| Find / discovery server | ✅ Implemented | `src/MauiApp/NdiBridge/NdiRuntime.cs` (`WriteConfigFileLocked`), `NdiDiscoveryBridge.SetDiscoveryMode` | Server list written to `ndi-config.v1.json` in app data and pointed at via `NDI_CONFIG_DIR` **before** `NDIlib_initialize` (the library reads config exactly once — config changes reinitialize when idle, or defer until the last handle drops). Server hosts also passed as `p_extra_ips`. TCP health probe: `src/MauiApp/NdiBridge/NetworkReachability.cs`. |
| Receive video | ✅ Implemented | `src/MauiApp/NdiBridge/NdiViewerBridge.cs` (video pump), rendering in `src/MauiApp/Features/Viewer/Views/ViewerView.xaml.cs` | `NDIlib_recv_create_v3` (BGRX/BGRA, fields off) + `recv_capture_v3` on a dedicated pump thread; latest-frame double buffer; SkiaSharp canvas pulls at ~30 fps. `QualityProfile.Smooth` maps to `bandwidth_lowest`, Balanced/High to `bandwidth_highest`. |
| Receive audio | ✅ Implemented | `NdiViewerBridge.cs` (audio pump), `src/MauiApp/Platforms/Android/Services/AndroidAudioPlaybackSink.cs` | Dedicated audio pump via `recv_capture_v3` (audio-only overload); FLTP planar → interleaved float PCM → Android `AudioTrack`. Mute toggle via `IsAudioEnabled`. |
| Receive metadata | 🟡 Partially implemented | `NdiViewerBridge.cs` (`HandleMetadata`) | Metadata frames are captured and freed correctly; only `<ndi_tally_echo …/>` is parsed (raised as `TallyEchoChanged`). Other metadata payloads are ignored — no current feature needs them. |
| Frame sync | 🟡 Partially implemented | `src/MauiApp/NdiBridge/Interop/NdiNativeMethods.cs` (`NDIlib_framesync_*`) | P/Invoke bindings are declared and verified against the binary, but no bridge uses them: display pacing is achieved with the latest-frame double buffer + pull-loop render, and audio is push-driven into `AudioTrack`. Available for future display-locked rendering. |
| Tally (both directions) | ✅ Implemented | Receiver: `NdiViewerBridge.SetTally` (`NDIlib_recv_set_tally`, re-applied after every reconnect) + tally-echo metadata → `TallyEchoChanged`. Sender: `NdiOutputBridge.PollSenderStatus` (`NDIlib_send_get_tally`, 1 s poll) → `IsOnProgramTally`. | |
| PTZ | ✅ Implemented | `NdiViewerBridge.cs` (PTZ passthroughs), contract in `src/Core/NdiBridge/INdiBridges.cs` | Gated on `NDIlib_recv_ptz_is_supported`, re-checked on `status_change` frames. Exposed: pan/tilt speed, zoom speed, preset store/recall (0–99), auto-focus. Manual focus is bound in interop but not surfaced; exposure/white-balance controls are not bound (niche for this app). |
| Connection metadata | ✅ Implemented | `src/MauiApp/NdiBridge/NdiViewerBridge.cs`, `src/MauiApp/NdiBridge/NdiOutputBridge.cs` | Product/version identification attached to receiver and sender connections (added on this branch alongside the coverage work). |
| Send video | ✅ Implemented | `src/MauiApp/NdiBridge/NdiOutputBridge.cs`; capture: `AndroidVideoCaptureSource.cs` (screen via MediaProjection, front/rear camera via Camera2 → NV12), foreground service `ScreenShareForegroundService.cs` (`mediaProjection\|camera\|microphone` types) | `NDIlib_send_create` + synchronous `send_send_video_v2` from the capture callback (producer owns/reuses the frame buffer — async ping-pong send is a documented later optimization). RGBA/BGRA/NV12 accepted natively, no CPU conversion. |
| Send audio | ✅ Implemented | `NdiOutputBridge.cs` (`OnAudioChunkReady`), capture: `AndroidMicrophoneCaptureSource.cs` (AudioRecord float PCM) | `NDIlib_util_send_send_audio_interleaved_32f` — interleaved float in, SDK converts to planar FLTP. Opt-in via the microphone toggle. |
| Re-stream (receive → send) | ✅ Implemented | `NdiOutputBridge.StartReStreamFromSourceAsync` / `ReStreamPumpLoop` | Dedicated receiver + dedicated sender; the recv-owned native buffer is forwarded zero-copy to the synchronous send, then freed. Independent of the viewer bridge's connection. |
| Routing (`NDIlib_routing_*`) | ⛔ Out of scope | — | Deliberate: the user-facing re-stream case is covered by the zero-copy recv→send pump above, which also works when receivers cannot reach the original source directly. The routing API is not bound in `NdiNativeMethods.cs`. |
| Recording (`NDIlib_recv_recording_*`) | ⛔ Out of scope | — | Niche capability with no planned feature; documented decision from the capability matrix (#280). Not bound in interop. |
| NDI HX decode | 🟡 Partially implemented (unverified) | Standard receive path in `NdiViewerBridge.cs` | The SDK documents transparent HX decode for Windows/macOS only. If the bundled Android lib decodes HX transparently, the existing receive path handles it unchanged — **this must still be verified against a real HX source on an ARM device** (pending on-device validation for #277/#278). Fallback design if it does not: Advanced SDK compressed passthrough + MediaCodec. |
| NDI HX encode | ⛔ Out of scope | — | Licensing/platform limitation: HX encode (`send_send_video_scatter` etc.) requires the vendor-licensed **NDI Advanced SDK**. The bundled standard SDK sends full-bandwidth SpeedHQ, which this app uses. Documented, not silently skipped. |
| SDK version reporting | ✅ Implemented | `NdiRuntime.NativeVersion` (`NDIlib_version`), surfaced in Settings → About (`src/MauiApp/Features/Settings/Views/SettingsPage.xaml`) | Version string read once per initialize (e.g. "NDI SDK ANDROID … 6.3.1.0"); Settings display added on this branch. |

## Known follow-ups

- **On-device NDI network validation** for the receive (#277) and send (#278) paths on a
  physical ARM device is pending — includes the HX-decode verification above.
- **Camera orientation compensation + `SessionConfiguration` migration** — tracked as issue #284.
- **Async video send with ping-pong buffers** (`NDIlib_send_send_video_async_v2`) — bound in
  interop, documented optimization; the synchronous send is intentional while capture sources
  own their frame buffers.
