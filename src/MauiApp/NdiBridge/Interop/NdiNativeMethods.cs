using System.Runtime.InteropServices;

namespace NdiForAndroid.NdiBridge.Interop;

/// <summary>
/// P/Invoke surface for libndi.so (NDI SDK 6.3.1, Android). All symbols below were
/// verified present in the bundled binary. Instance handles are opaque IntPtrs.
/// Every captured frame MUST be freed with the matching NDIlib_recv_free_* /
/// NDIlib_framesync_free_* call or native memory leaks at video rates.
/// </summary>
internal static partial class NdiNativeMethods
{
    private const string Lib = "ndi";

    // ── Lifecycle ────────────────────────────────────────────────────────────

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool NDIlib_initialize();

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_destroy();

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr NDIlib_version();

    // ── Find (discovery) ─────────────────────────────────────────────────────

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr NDIlib_find_create_v2(ref NdiFindCreateNative create);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_find_destroy(IntPtr instance);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool NDIlib_find_wait_for_sources(IntPtr instance, uint timeoutMs);

    /// <summary>Returned pointer is owned by the finder — copy strings before the next call.</summary>
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr NDIlib_find_get_current_sources(IntPtr instance, out uint count);

    // ── Receive ──────────────────────────────────────────────────────────────

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr NDIlib_recv_create_v3(ref NdiRecvCreateV3Native create);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_recv_destroy(IntPtr instance);

    /// <summary>Pass IntPtr.Zero as source to disconnect without destroying.</summary>
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_recv_connect(IntPtr instance, ref NdiSourceNative source);

    [DllImport(Lib, EntryPoint = "NDIlib_recv_connect", CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_recv_disconnect(IntPtr instance, IntPtr nullSource);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern NdiFrameType NDIlib_recv_capture_v3(
        IntPtr instance,
        ref NdiVideoFrameV2Native video,
        IntPtr noAudio,
        ref NdiMetadataFrameNative metadata,
        uint timeoutMs);

    /// <summary>Audio-only capture overload (video/metadata ignored).</summary>
    [DllImport(Lib, EntryPoint = "NDIlib_recv_capture_v3", CallingConvention = CallingConvention.Cdecl)]
    public static extern NdiFrameType NDIlib_recv_capture_audio_v3(
        IntPtr instance,
        IntPtr noVideo,
        ref NdiAudioFrameV3Native audio,
        IntPtr noMetadata,
        uint timeoutMs);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_recv_free_video_v2(IntPtr instance, ref NdiVideoFrameV2Native video);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_recv_free_audio_v3(IntPtr instance, ref NdiAudioFrameV3Native audio);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_recv_free_metadata(IntPtr instance, ref NdiMetadataFrameNative metadata);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_recv_get_performance(
        IntPtr instance,
        out NdiRecvPerformanceNative total,
        out NdiRecvPerformanceNative dropped);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int NDIlib_recv_get_no_connections(IntPtr instance);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_recv_set_tally(IntPtr instance, ref NdiTallyNative tally);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_recv_add_connection_metadata(IntPtr instance, ref NdiMetadataFrameNative metadata);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_send_add_connection_metadata(IntPtr instance, ref NdiMetadataFrameNative metadata);

    // ── PTZ (receiver-side control of a remote camera) ───────────────────────

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool NDIlib_recv_ptz_is_supported(IntPtr instance);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool NDIlib_recv_ptz_zoom(IntPtr instance, float zoomValue);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool NDIlib_recv_ptz_zoom_speed(IntPtr instance, float zoomSpeed);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool NDIlib_recv_ptz_pan_tilt(IntPtr instance, float pan, float tilt);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool NDIlib_recv_ptz_pan_tilt_speed(IntPtr instance, float panSpeed, float tiltSpeed);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool NDIlib_recv_ptz_store_preset(IntPtr instance, int presetNo);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool NDIlib_recv_ptz_recall_preset(IntPtr instance, int presetNo, float speed);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool NDIlib_recv_ptz_auto_focus(IntPtr instance);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool NDIlib_recv_ptz_focus(IntPtr instance, float focusValue);

    // ── Send ─────────────────────────────────────────────────────────────────

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr NDIlib_send_create(ref NdiSendCreateNative create);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_send_destroy(IntPtr instance);

    /// <summary>Synchronous send — buffer is fully consumed on return.</summary>
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_send_send_video_v2(IntPtr instance, ref NdiVideoFrameV2Native video);

    /// <summary>
    /// Async send — the SDK owns the buffer until the NEXT async send (or flush).
    /// Use ping-pong buffers and flush with <see cref="NDIlib_send_send_video_async_v2_flush"/>.
    /// </summary>
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_send_send_video_async_v2(IntPtr instance, ref NdiVideoFrameV2Native video);

    [DllImport(Lib, EntryPoint = "NDIlib_send_send_video_async_v2", CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_send_send_video_async_v2_flush(IntPtr instance, IntPtr nullFrame);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_send_send_audio_v3(IntPtr instance, ref NdiAudioFrameV3Native audio);

    /// <summary>Convenience: interleaved float PCM in, SDK converts to planar FLTP.</summary>
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_util_send_send_audio_interleaved_32f(
        IntPtr instance, ref NdiAudioFrameInterleaved32fNative audio);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool NDIlib_send_get_tally(IntPtr instance, ref NdiTallyNative tally, uint timeoutMs);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern int NDIlib_send_get_no_connections(IntPtr instance, uint timeoutMs);

    // ── Frame sync (pull-clocked receive on top of a recv instance) ──────────

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr NDIlib_framesync_create(IntPtr recvInstance);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_framesync_destroy(IntPtr instance);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_framesync_capture_video(
        IntPtr instance, ref NdiVideoFrameV2Native video, int fieldType);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_framesync_free_video(IntPtr instance, ref NdiVideoFrameV2Native video);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_framesync_capture_audio(
        IntPtr instance, ref NdiAudioFrameV3Native audio, int sampleRate, int noChannels, int noSamples);

    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
    public static extern void NDIlib_framesync_free_audio(IntPtr instance, ref NdiAudioFrameV3Native audio);
}
