using System.Runtime.InteropServices;

namespace NdiForAndroid.Services;

/// <summary>
/// Rotates tightly packed NV12 frames (Y plane, then interleaved UV) by multiples of 90°
/// clockwise. Pure managed code so it is unit-testable in Core; the Android capture source
/// calls it on the capture thread with producer-owned buffers.
/// </summary>
public static class Nv12FrameRotator
{
    /// <summary>Bytes needed for a tight NV12 frame of the given (even) dimensions.</summary>
    public static int RequiredLength(int width, int height) => width * height * 3 / 2;

    /// <summary>Output (width, height) after rotating a width×height frame by <paramref name="rotationDegrees"/>.</summary>
    public static (int Width, int Height) RotatedSize(int width, int height, int rotationDegrees) =>
        rotationDegrees is 90 or 270 ? (height, width) : (width, height);

    /// <summary>
    /// Rotates <paramref name="source"/> (tight NV12, <paramref name="width"/>×<paramref name="height"/>,
    /// both even) clockwise by <paramref name="rotationDegrees"/> (0/90/180/270) into
    /// <paramref name="destination"/>. Returns the rotated (width, height). 0° is a straight copy.
    /// </summary>
    public static (int Width, int Height) Rotate(ReadOnlySpan<byte> source, int width, int height, int rotationDegrees, Span<byte> destination)
    {
        if (width <= 0 || height <= 0 || (width & 1) != 0 || (height & 1) != 0)
            throw new ArgumentException("NV12 dimensions must be positive and even.");
        if (rotationDegrees is not (0 or 90 or 180 or 270))
            throw new ArgumentOutOfRangeException(nameof(rotationDegrees), "Rotation must be 0, 90, 180 or 270.");

        var length = RequiredLength(width, height);
        if (source.Length < length)
            throw new ArgumentException("Source buffer is smaller than the NV12 frame.", nameof(source));
        if (destination.Length < length)
            throw new ArgumentException("Destination buffer is smaller than the NV12 frame.", nameof(destination));

        var (dstWidth, dstHeight) = RotatedSize(width, height, rotationDegrees);

        // Luma: width×height single bytes → dstWidth×dstHeight.
        RotatePlane(source[..(width * height)], width, height, rotationDegrees,
            destination[..(dstWidth * dstHeight)], dstWidth, dstHeight);

        // Chroma: (width/2)×(height/2) UV pairs; move each 2-byte pair as one ushort
        // (byte order inside the pair is preserved, so U stays before V).
        var srcChroma = MemoryMarshal.Cast<byte, ushort>(source.Slice(width * height, width * height / 2));
        var dstChroma = MemoryMarshal.Cast<byte, ushort>(destination.Slice(dstWidth * dstHeight, dstWidth * dstHeight / 2));
        RotatePlane(srcChroma, width / 2, height / 2, rotationDegrees, dstChroma, dstWidth / 2, dstHeight / 2);

        return (dstWidth, dstHeight);
    }

    private static void RotatePlane<T>(ReadOnlySpan<T> src, int srcW, int srcH, int rotationDegrees, Span<T> dst, int dstW, int dstH)
        where T : struct
    {
        switch (rotationDegrees)
        {
            case 0:
                src[..(srcW * srcH)].CopyTo(dst);
                return;

            case 180:
                // dst(x, y) = src(srcW-1-x, srcH-1-y): reverse every row, rows in reverse order.
                for (var y = 0; y < srcH; y++)
                {
                    var srcRow = src.Slice(y * srcW, srcW);
                    var dstRow = dst.Slice((srcH - 1 - y) * srcW, srcW);
                    for (var x = 0; x < srcW; x++)
                        dstRow[srcW - 1 - x] = srcRow[x];
                }
                return;

            case 90:
                // Clockwise: dst(x, y) = src(y, srcH-1-x)  (dstW = srcH, dstH = srcW).
                for (var dy = 0; dy < dstH; dy++)
                {
                    var dstRow = dst.Slice(dy * dstW, dstW);
                    for (var dx = 0; dx < dstW; dx++)
                        dstRow[dx] = src[(srcH - 1 - dx) * srcW + dy];
                }
                return;

            case 270:
                // Counter-clockwise: dst(x, y) = src(srcW-1-y, x)  (dstW = srcH, dstH = srcW).
                for (var dy = 0; dy < dstH; dy++)
                {
                    var dstRow = dst.Slice(dy * dstW, dstW);
                    var srcX = srcW - 1 - dy;
                    for (var dx = 0; dx < dstW; dx++)
                        dstRow[dx] = src[dx * srcW + srcX];
                }
                return;
        }
    }
}
