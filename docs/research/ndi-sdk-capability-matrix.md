# NDI SDK Capability Matrix — Android (.NET MAUI)

> Research artifact for #275 (production readiness). Grounded in the official NDI 6.3 docs
> (https://docs.ndi.video/all/developing-with-ndi/sdk) and validated against the **bundled
> `libndi.so`: NDI SDK ANDROID 6.3.1.0** (arm64-v8a + armeabi-v7a, symbol surface verified).
> Implementation reference: [legacy-bridge-analysis.md](legacy-bridge-analysis.md).

## Capability → plan

| Capability | SDK surface | In bundled lib? | Plan | Issue |
|---|---|---|---|---|
| Init / version | `NDIlib_initialize` (false on non-NEON CPU), `NDIlib_version` | ✅ | Already P/Invoked; surface version in Settings/About | #277/#280 |
| Discovery (mDNS) | `NDIlib_find_create_v2` (`show_local_sources`, `p_groups`, `p_extra_ips`), `find_wait_for_sources`, `find_get_current_sources` | ✅ | Implement; **requires Android `NsdManager` held before any NDI object + `MulticastLock`** | #277 |
| Discovery server | `ndi-config.v1.json` → `ndi.networks.discovery` (`host:5959,host2`), pointed at via **`NDI_CONFIG_DIR` env var set before init**; `p_extra_ips` = direct-host query (different mechanism) | ✅ | Implement; config change ⇒ recreate NDI objects | #277 |
| Receive video/audio/metadata | `NDIlib_recv_create_v3` (color_format, bandwidth, `recv_connect` to switch), `recv_capture_v3` (multi-thread safe), `recv_free_*`, `recv_get_performance/queue/no_connections` | ✅ | Implement; `bandwidth_lowest` = preview / `highest` = focused viewer maps to QualityProfile | #277 |
| Frame sync | `NDIlib_framesync_create/destroy`, `capture_video/free_video`, `capture_audio/free_audio` (pull-clocked; audio resampled; video dup/drop) | ✅ | Use for display-locked rendering + AudioTrack-paced audio | #277 |
| Tally | `NDIlib_recv_set_tally` (retained across reconnect); sender: `NDIlib_send_get_tally`; echo metadata `<ndi_tally_echo …/>` to all receivers | ✅ | Implement both directions; viewer tally border from echo | #277/#278 |
| PTZ | `NDIlib_recv_ptz_is_supported` + zoom/zoom_speed/pan_tilt(_speed)/store_recall_preset(0-99)/focus/auto_focus/white_balance_*/exposure_* | ✅ (full set) | Implement; gate UI on `is_supported`, re-check on `status_change` frames (support arrives with connection metadata) | #277 |
| Send video/audio | `NDIlib_send_create` (`p_ndi_name` <253 chars, `clock_video/audio`), `send_send_video_v2` / **`_async` (preferred + ping-pong buffers)**, `send_send_audio_v3` (FLTP planar, ±1.0 = +4 dBU), `NDIlib_util_*interleaved*` helpers | ✅ | Implement; async send + `clock_video=true`; NV12/I420 accepted directly (camera-friendly); 720p30 Wi-Fi default, 1080p opt-in | #278 |
| Connection metadata | `recv/send_add_connection_metadata`, `clear_connection_metadata` | ✅ | Implement (product/version identification) | #277/#278 |
| Routing | `NDIlib_routing_create/change/clear/destroy` — virtual source redirect, zero data through host | ✅ | Consider as cheap alternative to pixel-pumping re-stream | #278 |
| Recording | `NDIlib_recv_recording_*` | ✅ | Out of scope v1 — niche; document only | #280 |
| **NDI HX decode** | Transparent decode documented for **Windows/macOS only**; Advanced SDK offers `color_format_compressed` passthrough | ⚠️ unverified | **Must verify empirically** against an HX source early in #277; fallback design = Advanced SDK + MediaCodec | #277 |
| **NDI HX encode** | `NDIlib_send_send_video_scatter` etc. — **Advanced SDK only**, vendor-licensed | ❌ | **Out of scope** — platform/licensing limitation; standard lib sends full-bandwidth SpeedHQ | #280 (documented) |

## Android platform requirements

- **`NsdManager` must be acquired** (`GetSystemService(NsdService)`) before creating any NDI object and kept alive (docs: "no way for a third-party library to do this on its own").
- `MulticastLock` (`CHANGE_WIFI_MULTICAST_STATE`) held during discovery; `INTERNET`, `ACCESS_WIFI_STATE` permissions.
- Foreground service for sending: `foregroundServiceType="mediaProjection|camera|microphone"` + `FOREGROUND_SERVICE_*` permissions + `POST_NOTIFICATIONS` (API 33+).
- Known Android 14 mDNS discovery regression — fixed in current libs; keep manual-IP fallback (`p_extra_ips`).

## Performance guidance (docs)

- Memory-bandwidth bound on ARM; prefer UYVY end-to-end; `color_format_fastest` on receive; let the SDK do CPU conversion rather than pre-converting.
- Separate audio/video capture threads; sane `capture_v3` timeouts (50–1000 ms), never tight-poll.
- Every captured frame **must** be freed (`recv_free_*`) — leak = native OOM within seconds at video rates.
- Async send buffer stays SDK-owned until next sync point — ping-pong two buffers; flush with `send_send_video_v2_async(pSend, NULL)`.
- NDI 5+ uses reliable UDP with congestion control; leave multicast send off (Wi-Fi).
