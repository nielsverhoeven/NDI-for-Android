using System.Runtime.InteropServices;

namespace NdiForAndroid.NdiBridge.Interop;

// Blittable mirrors of the Processing.NDI v6 structs (ABI stable since v4).
// Layout rules (arm64/arm32): pointers are IntPtr, C 'bool' is 1 byte, natural
// alignment — Sequential layout reproduces the C padding exactly as long as the
// field order below matches the headers. Do NOT reorder fields.
// These types never leave the NdiBridge layer (repo constitution).

/// <summary>NDIlib_frame_type_e — result of NDIlib_recv_capture_v3.</summary>
internal enum NdiFrameType
{
    None = 0,
    Video = 1,
    Audio = 2,
    Metadata = 3,
    Error = 4,
    StatusChange = 100,
}

/// <summary>NDIlib_recv_color_format_e.</summary>
internal enum NdiRecvColorFormat
{
    BGRX_BGRA = 0,
    UYVY_BGRA = 1,
    RGBX_RGBA = 2,
    UYVY_RGBA = 3,
    Fastest = 100,
    Best = 101,
}

/// <summary>NDIlib_recv_bandwidth_e.</summary>
internal enum NdiRecvBandwidth
{
    MetadataOnly = -10,
    AudioOnly = 10,
    Lowest = 0,
    Highest = 100,
}

/// <summary>NDIlib_frame_format_type_e. NOTE: progressive is 1, interleaved is 0.</summary>
internal enum NdiFrameFormatType
{
    Interleaved = 0,
    Progressive = 1,
    Field0 = 2,
    Field1 = 3,
}

/// <summary>Video FourCC codes (little-endian ASCII).</summary>
internal static class NdiFourCC
{
    public const uint UYVY = 0x59565955; // 'UYVY'
    public const uint UYVA = 0x41565955; // 'UYVA'
    public const uint BGRA = 0x41524742; // 'BGRA'
    public const uint BGRX = 0x58524742; // 'BGRX'
    public const uint RGBA = 0x41424752; // 'RGBA'
    public const uint RGBX = 0x58424752; // 'RGBX'
    public const uint NV12 = 0x3231564E; // 'NV12'
    public const uint I420 = 0x30323449; // 'I420'
    public const uint FLTP = 0x70544C46; // 'FLTp' — planar float audio
}

/// <summary>NDIlib_source_t — two C strings; list memory is owned by the finder.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NdiSourceNative
{
    public IntPtr p_ndi_name;    // const char*
    public IntPtr p_url_address; // const char* (union with p_ip_address)
}

/// <summary>NDIlib_find_create_t.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NdiFindCreateNative
{
    [MarshalAs(UnmanagedType.I1)] public bool show_local_sources;
    public IntPtr p_groups;    // const char* or NULL
    public IntPtr p_extra_ips; // comma-separated host list or NULL
}

/// <summary>NDIlib_recv_create_v3_t.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NdiRecvCreateV3Native
{
    public NdiSourceNative source_to_connect_to;
    public int color_format;  // NdiRecvColorFormat
    public int bandwidth;     // NdiRecvBandwidth
    [MarshalAs(UnmanagedType.I1)] public bool allow_video_fields;
    public IntPtr p_ndi_recv_name; // const char* or NULL
}

/// <summary>NDIlib_video_frame_v2_t. Field order per Processing.NDI.structs.h.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NdiVideoFrameV2Native
{
    public int xres;
    public int yres;
    public uint FourCC;
    public int frame_rate_N;
    public int frame_rate_D;
    public float picture_aspect_ratio;
    public int frame_format_type;      // NdiFrameFormatType
    public long timecode;              // 100 ns units; senders pass Synthesize
    public IntPtr p_data;              // const uint8_t*
    public int line_stride_in_bytes;   // union with data_size_in_bytes
    public IntPtr p_metadata;          // per-frame XML or NULL
    public long timestamp;             // UTC, filled by receiver

    /// <summary>NDIlib_send_timecode_synthesize (INT64_MAX).</summary>
    public const long TimecodeSynthesize = long.MaxValue;
}

/// <summary>NDIlib_audio_frame_v3_t (FLTP planar float).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NdiAudioFrameV3Native
{
    public int sample_rate;
    public int no_channels;
    public int no_samples;
    public long timecode;
    public uint FourCC;                  // NdiFourCC.FLTP
    public IntPtr p_data;                // planar float: channel-major
    public int channel_stride_in_bytes;  // union with data_size_in_bytes
    public IntPtr p_metadata;
    public long timestamp;
}

/// <summary>NDIlib_metadata_frame_t.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NdiMetadataFrameNative
{
    public int length;   // bytes, including terminating NUL
    public long timecode;
    public IntPtr p_data; // UTF-8 XML
}

/// <summary>NDIlib_tally_t.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NdiTallyNative
{
    [MarshalAs(UnmanagedType.I1)] public bool on_program;
    [MarshalAs(UnmanagedType.I1)] public bool on_preview;
}

/// <summary>NDIlib_recv_performance_t — cumulative counters; diff to get drops.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NdiRecvPerformanceNative
{
    public long video_frames;
    public long audio_frames;
    public long metadata_frames;
}
