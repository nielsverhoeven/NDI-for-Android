namespace NdiForAndroid.Features.Ptz.Services;

/// <summary>Encodes VISCA commands as raw byte frames. VISCA device address is fixed at 1 (0x81).</summary>
public static class ViscaCommandEncoder
{
    private const byte Address = 0x81;
    private const byte MaxPanSpeed = 0x18;
    private const byte MaxTiltSpeed = 0x14;
    private const byte MaxZoomSpeed = 0x07;

    /// <summary>81 01 06 01 VV WW 0p 0t FF. Pan dir: 1=left,2=right. Tilt dir: 1=up,2=down.</summary>
    public static byte[] PanTiltDrive(float panSpeed, float tiltSpeed)
    {
        var (panDirection, panByte) = EncodeAxis(panSpeed, positiveDirection: 0x02, negativeDirection: 0x01, maxSpeed: MaxPanSpeed);
        var (tiltDirection, tiltByte) = EncodeAxis(tiltSpeed, positiveDirection: 0x01, negativeDirection: 0x02, maxSpeed: MaxTiltSpeed);
        return new byte[] { Address, 0x01, 0x06, 0x01, panByte, tiltByte, panDirection, tiltDirection, 0xFF };
    }

    /// <summary>value==0 => stop (dir=3, speed=0x00); else speed = clamp(round(|value|*(maxSpeed-1))+1, 1, maxSpeed).</summary>
    private static (byte direction, byte speed) EncodeAxis(float value, byte positiveDirection, byte negativeDirection, byte maxSpeed)
    {
        if (value == 0f)
            return (0x03, 0x00);

        var direction = value > 0 ? positiveDirection : negativeDirection;
        var magnitude = Math.Clamp(Math.Abs(value), 0f, 1f);
        var speed = (byte)Math.Clamp((int)Math.Round(magnitude * (maxSpeed - 1), MidpointRounding.AwayFromZero) + 1, 1, maxSpeed);
        return (direction, speed);
    }

    /// <summary>81 01 04 07 2p FF (tele/in) | 3p FF (wide/out) | 00 FF (stop). p = speed 1-7.</summary>
    public static byte[] ZoomSpeed(float zoomSpeed)
    {
        if (zoomSpeed == 0f)
            return new byte[] { Address, 0x01, 0x04, 0x07, 0x00, 0xFF };

        var magnitude = Math.Clamp(Math.Abs(zoomSpeed), 0f, 1f);
        var speed = (byte)Math.Clamp((int)Math.Round(magnitude * MaxZoomSpeed, MidpointRounding.AwayFromZero), 1, MaxZoomSpeed);
        var prefix = zoomSpeed > 0 ? (byte)0x20 : (byte)0x30;
        return new byte[] { Address, 0x01, 0x04, 0x07, (byte)(prefix | speed), 0xFF };
    }

    /// <summary>One-push auto-focus: 81 01 04 18 01 FF.</summary>
    public static byte[] AutoFocus() => new byte[] { Address, 0x01, 0x04, 0x18, 0x01, 0xFF };

    /// <summary>81 01 04 3F 01 pp FF, pp = 0-99 clamped.</summary>
    public static byte[] StorePreset(int presetNumber) =>
        new byte[] { Address, 0x01, 0x04, 0x3F, 0x01, ClampPreset(presetNumber), 0xFF };

    /// <summary>81 01 04 3F 02 pp FF, pp = 0-99 clamped.</summary>
    public static byte[] RecallPreset(int presetNumber) =>
        new byte[] { Address, 0x01, 0x04, 0x3F, 0x02, ClampPreset(presetNumber), 0xFF };

    private static byte ClampPreset(int presetNumber) => (byte)Math.Clamp(presetNumber, 0, 99);
}
