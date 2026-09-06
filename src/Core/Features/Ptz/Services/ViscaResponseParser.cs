namespace NdiForAndroid.Features.Ptz.Services;

public enum ViscaResponseKind { Ack, Completion, Error, Unknown }

/// <summary>A parsed VISCA reply. <see cref="ErrorCode"/> is only set for <see cref="ViscaResponseKind.Error"/>.</summary>
public sealed record ViscaResponse(ViscaResponseKind Kind, int Socket, byte? ErrorCode);

/// <summary>
/// Parses VISCA reply frames: ACK (90 4y FF), Completion (90 5y FF), Error (90 6y xx FF). Frames
/// always terminate with 0xFF, so a raw buffer may hold zero, one, or several concatenated
/// replies; any bytes after the last 0xFF are an incomplete reply and are not returned.
/// </summary>
public static class ViscaResponseParser
{
    /// <summary>Parses a single, already-delimited reply frame (including its terminating 0xFF).</summary>
    public static ViscaResponse Parse(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 3 || frame[^1] != 0xFF)
            return new ViscaResponse(ViscaResponseKind.Unknown, 0, null);

        var header = frame[1];
        var socket = header & 0x0F;
        return (byte)(header & 0xF0) switch
        {
            0x40 => new ViscaResponse(ViscaResponseKind.Ack, socket, null),
            0x50 => new ViscaResponse(ViscaResponseKind.Completion, socket, null),
            0x60 when frame.Length >= 4 => new ViscaResponse(ViscaResponseKind.Error, socket, frame[2]),
            _ => new ViscaResponse(ViscaResponseKind.Unknown, socket, null),
        };
    }

    /// <summary>Splits a raw buffer into every complete reply it contains, dropping a trailing incomplete reply.</summary>
    public static IReadOnlyList<ViscaResponse> ParseAll(ReadOnlySpan<byte> buffer)
    {
        var responses = new List<ViscaResponse>();
        var start = 0;
        for (var i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] != 0xFF)
                continue;

            responses.Add(Parse(buffer[start..(i + 1)]));
            start = i + 1;
        }

        return responses;
    }
}
