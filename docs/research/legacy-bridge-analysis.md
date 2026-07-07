# Legacy NDI Bridge Analysis — P/Invoke Reference

> Research artifact for #275/#277/#278. Extracted from the **working** legacy Kotlin app's
> native bridge: `legacy_app/ndi/sdk-bridge/src/main/cpp/ndi_bridge.cpp` and
> `ndi_screen_share.cpp`. Treat the `.cpp` files as ground truth for exact struct layouts;
> verify every symbol against the bundled lib before binding (all listed below were
> string-verified present in `src/MauiApp/Platforms/Android/libs/arm64-v8a/libndi.so`, 6.3.1.0).

## Critical call sequences

### Initialization (order matters)
1. Write `ndi-config.v1.json` (`{"ndi":{"networks":{"discovery":"host:5959"}}}`) into app-private dir; set `NDI_CONFIG_DIR` (legacy also used `NDI_DISCOVERY_SERVER` env var) **before** `NDIlib_initialize()`.
2. `NDIlib_initialize()` once (SDK refcounts); `NDIlib_destroy()` only when reconfiguring discovery, not on exit.
3. Changing the discovery server ⇒ destroy all NDI objects, re-init.

### Receive
```
finder: NDIlib_find_create_v2({show_local_sources=true, p_groups=null, p_extra_ips="ip1,ip2"})
        NDIlib_find_wait_for_sources(finder, 1500ms)  // keep finder ALIVE across polls
        NDIlib_find_get_current_sources(finder, &count) // strings owned by finder — copy immediately
recv:   NDIlib_recv_create_v3({source, color_format=BGRX_BGRA(0), bandwidth=highest(10), allow_video_fields})
        NDIlib_recv_connect(recv, &source)
loop:   NDIlib_recv_capture_v3(recv, &video, &audio, &meta, 1000ms) → frame_type enum
        … use frame (respect line_stride_in_bytes — NEVER assume width*4) …
        NDIlib_recv_free_video_v2(recv, &video)   // mandatory per frame
stats:  NDIlib_recv_get_performance(recv, &total, &dropped) // cumulative — diff for percent
stop:   set atomic running=false → join thread (NO mutex held during join — legacy deadlock lesson)
        → NDIlib_recv_destroy(recv)
```
- Legacy pixel path: BGRA/BGRX → ARGB int[] conversion in native code; alpha forced 0xFF.
- FPS measured as frames in a rolling 1 s window.
- Source identity: prefer `p_url_address` as canonical ID, fall back to `p_ndi_name` (mDNS).

### Send (screen share)
```
NDIlib_send_create({p_ndi_name=streamName, p_groups=null, clock_video=true, clock_audio=false})
per frame: NDIlib_video_frame_v2_t {
  xres, yres, FourCC=0x41524742 /*BGRA*/, frame_rate_N=30, frame_rate_D=1,
  picture_aspect_ratio=w/h, frame_format_type=progressive(0),
  timecode=NDIlib_send_timecode_synthesize, p_data, line_stride_in_bytes=w*4 }
NDIlib_send_send_video_v2(send, &frame)      // legacy used sync send; NEW code should use _async + ping-pong
NDIlib_send_destroy(send)
```

### Android capture pipeline (screen)
`MediaProjectionManager.createScreenCaptureIntent()` → activity result → **foreground service
(`foregroundServiceType="mediaProjection"`) started before capture** → `MediaProjection.createVirtualDisplay`
(RGBA_8888, `VIRTUAL_DISPLAY_FLAG_AUTO_MIRROR`) → `ImageReader` (2-frame buffer, dedicated
`HandlerThread`) → per-image: honor `planes[0]` pixelStride/rowStride → convert → NDI send.

### Discovery specifics
- **MulticastLock is mandatory** for mDNS: without it `find_wait_for_sources` times out silently.
- Legacy fallback chain: native finder → `NDIlib_send_listener_*` (direct TCP registry query of a
  discovery server; symbols exist in 6.3.1 — note: not in public docs, verify structs carefully
  before binding) → jmDNS (`_ndi._tcp.local.`) as last resort.
- Legacy wrote the NDI config to both `$HOME/.ndi/` and `/sdcard/.ndi/` for reliability.

## Struct layouts (as used by legacy — verify against 6.3 headers before P/Invoke)

```c
NDIlib_find_create_t   { bool show_local_sources; const char* p_groups; const char* p_extra_ips; }
NDIlib_source_t        { const char* p_ndi_name; const char* p_url_address; }
NDIlib_recv_create_v3_t{ NDIlib_source_t source; int color_format; int bandwidth;
                         bool allow_video_fields; const char* p_ndi_recv_name; }
NDIlib_video_frame_v2_t{ int xres, yres; uint32 FourCC; int frame_rate_N, frame_rate_D;
                         float picture_aspect_ratio; int frame_format_type;
                         int64 timecode; uint8* p_data; int line_stride_in_bytes;
                         const char* p_metadata; int64 timestamp; }
```
C# marshaling notes: use blittable `LayoutKind.Sequential` structs, `IntPtr` for strings/data
(manual `Marshal.PtrToStringAnsi` copies), **no `bool`/`string` marshaling inside frame structs**;
native 1-byte bools in create structs need `[MarshalAs(UnmanagedType.I1)]`.

## Threading model (proven in legacy)
- One pump thread per receiver; latest-frame swap under a mutex; UI polls the managed copy.
- Atomic `running` flag; release locks before `thread.join()`.
- Sender: create/destroy under lock; frame submission from the capture callback thread.
