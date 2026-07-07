using System.Runtime.InteropServices;
using NdiForAndroid.NdiBridge.Interop;

namespace NdiForAndroid.NdiBridge;

/// <summary>
/// Applies product-identification connection metadata (<c>ndi_product</c>) to a
/// receiver or sender instance so remote peers can identify this app.
/// </summary>
internal static class NdiConnectionMetadata
{
    /// <summary>Adds the ndi_product connection metadata to a live recv/send instance.</summary>
    public static void Apply(IntPtr instance, bool isSender, string sessionName)
    {
        if (instance == IntPtr.Zero)
            return;

        var xml =
            $"<ndi_product long_name=\"NDI for Android\" short_name=\"NDI-Android\" " +
            $"manufacturer=\"nielsverhoeven\" version=\"1.0.0\" model_name=\"MAUI\" " +
            $"session_name=\"{sessionName}\"/>";

        var ptr = Marshal.StringToHGlobalAnsi(xml);
        try
        {
            var metadata = new NdiMetadataFrameNative
            {
                // length includes the terminating NUL that StringToHGlobalAnsi appends.
                length = xml.Length + 1,
                timecode = 0,
                p_data = ptr,
            };

            if (isSender)
                NdiNativeMethods.NDIlib_send_add_connection_metadata(instance, ref metadata);
            else
                NdiNativeMethods.NDIlib_recv_add_connection_metadata(instance, ref metadata);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}
