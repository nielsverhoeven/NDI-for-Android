namespace ViscaMockCamera;

internal static class ViscaCommandProcessor
{
    private static readonly byte[] Ack = { 0x90, 0x41, 0xFF };
    private static readonly byte[] Completion = { 0x90, 0x51, 0xFF };
    private static readonly byte[] SyntaxError = { 0x90, 0x60, 0x02, 0xFF };

    public static ViscaResult Process(byte[] frame, CameraState state)
    {
        var payload = frame.Length == 0 ? Array.Empty<byte>() : frame[..^1];

        if (payload.Length < 2 || (payload[0] & 0xF0) != 0x80 || (payload[1] != 0x01 && payload[1] != 0x09))
        {
            return new ViscaResult("malformed frame", SyntaxError);
        }

        return payload[1] == 0x09 ? ProcessInquiry(payload, state) : ProcessCommand(payload, state);
    }

    private static ViscaResult ProcessCommand(byte[] p, CameraState state)
    {
        if (p.Length == 8 && p[2] == 0x06 && p[3] == 0x01)
        {
            return ProcessPanTiltDrive(p, state);
        }

        if (p.Length == 4 && p[2] == 0x06 && p[3] == 0x04)
        {
            state.GoHome();
            return new ViscaResult("pan/tilt home", Ack, Completion);
        }

        if (p.Length == 5 && p[2] == 0x04 && p[3] == 0x07)
        {
            return ProcessZoom(p, state);
        }

        if (p.Length == 5 && p[2] == 0x04 && p[3] == 0x18 && p[4] == 0x01)
        {
            return new ViscaResult("focus: one-push trigger", Ack, Completion);
        }

        if (p.Length == 5 && p[2] == 0x04 && p[3] == 0x38 && (p[4] == 0x02 || p[4] == 0x03))
        {
            var mode = p[4] == 0x02 ? "auto" : "manual";
            return new ViscaResult($"focus mode: {mode}", Ack, Completion);
        }

        if (p.Length == 6 && p[2] == 0x04 && p[3] == 0x3F)
        {
            return ProcessPreset(p, state);
        }

        return new ViscaResult($"unknown command: {ToHex(p)}", SyntaxError);
    }

    private static ViscaResult ProcessPanTiltDrive(byte[] p, CameraState state)
    {
        var panSpeed = p[4];
        var tiltSpeed = p[5];
        var panDirection = p[6] switch { 0x01 => -1, 0x02 => 1, _ => 0 };
        var tiltDirection = p[7] switch { 0x01 => 1, 0x02 => -1, _ => 0 };

        if (panDirection == 0 && tiltDirection == 0)
        {
            state.StopPanTilt();
            return new ViscaResult("pan/tilt drive: stop", Ack, Completion);
        }

        state.SetPanTiltDrive(panSpeed, tiltSpeed, panDirection, tiltDirection);
        var panText = panDirection switch { < 0 => "left", > 0 => "right", _ => "none" };
        var tiltText = tiltDirection switch { > 0 => "up", < 0 => "down", _ => "none" };
        return new ViscaResult($"pan/tilt drive: pan={panText} speed={panSpeed} tilt={tiltText} speed={tiltSpeed}", Ack, Completion);
    }

    private static ViscaResult ProcessZoom(byte[] p, CameraState state)
    {
        var b = p[4];
        if (b == 0x00)
        {
            state.StopZoom();
            return new ViscaResult("zoom: stop", Ack, Completion);
        }

        var speed = b & 0x0F;
        switch (b >> 4)
        {
            case 0x02:
                state.SetZoomDrive(1, speed);
                return new ViscaResult($"zoom: tele speed={speed}", Ack, Completion);
            case 0x03:
                state.SetZoomDrive(-1, speed);
                return new ViscaResult($"zoom: wide speed={speed}", Ack, Completion);
            default:
                return new ViscaResult($"unknown command: {ToHex(p)}", SyntaxError);
        }
    }

    private static ViscaResult ProcessPreset(byte[] p, CameraState state)
    {
        int presetIndex = p[5];
        if (presetIndex > 15)
        {
            return new ViscaResult($"preset: index {presetIndex} out of range", SyntaxError);
        }

        switch (p[4])
        {
            case 0x00:
                state.ResetPreset(presetIndex);
                return new ViscaResult($"preset: reset {presetIndex}", Ack, Completion);
            case 0x01:
                state.SetPreset(presetIndex);
                return new ViscaResult($"preset: set {presetIndex}", Ack, Completion);
            case 0x02:
                var recalled = state.RecallPreset(presetIndex);
                return new ViscaResult($"preset: recall {presetIndex}{(recalled ? string.Empty : " (empty)")}", Ack, Completion);
            default:
                return new ViscaResult($"unknown command: {ToHex(p)}", SyntaxError);
        }
    }

    private static ViscaResult ProcessInquiry(byte[] p, CameraState state)
    {
        if (p.Length == 4 && p[2] == 0x04 && p[3] == 0x00)
        {
            return new ViscaResult("power inquiry", BuildReply(0x02));
        }

        if (p.Length == 4 && p[2] == 0x06 && p[3] == 0x12)
        {
            var (pan, tilt) = state.GetPanTiltPosition();
            var payload = new byte[8];
            WriteNibbles(payload, 0, (ushort)pan);
            WriteNibbles(payload, 4, (ushort)tilt);
            return new ViscaResult($"pan/tilt position inquiry: pan={pan} tilt={tilt}", BuildReply(payload));
        }

        if (p.Length == 4 && p[2] == 0x04 && p[3] == 0x47)
        {
            var zoom = state.GetZoomPosition();
            var payload = new byte[4];
            WriteNibbles(payload, 0, (ushort)zoom);
            return new ViscaResult($"zoom position inquiry: zoom={zoom}", BuildReply(payload));
        }

        return new ViscaResult($"unknown inquiry: {ToHex(p)}", SyntaxError);
    }

    private static void WriteNibbles(byte[] destination, int offset, ushort value)
    {
        destination[offset] = (byte)((value >> 12) & 0x0F);
        destination[offset + 1] = (byte)((value >> 8) & 0x0F);
        destination[offset + 2] = (byte)((value >> 4) & 0x0F);
        destination[offset + 3] = (byte)(value & 0x0F);
    }

    private static byte[] BuildReply(params byte[] payload)
    {
        var reply = new byte[payload.Length + 3];
        reply[0] = 0x90;
        reply[1] = 0x50;
        Array.Copy(payload, 0, reply, 2, payload.Length);
        reply[^1] = 0xFF;
        return reply;
    }

    private static string ToHex(byte[] bytes) => Convert.ToHexString(bytes);
}
