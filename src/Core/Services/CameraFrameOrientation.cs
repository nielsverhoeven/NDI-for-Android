namespace NdiForAndroid.Services;

/// <summary>
/// Pure Camera2 orientation policy (Core, MAUI-free): how many degrees clockwise a
/// sensor-native camera frame must be rotated so it is upright for the current display
/// rotation. Mirrors the CameraX <c>getRelativeImageRotation</c> / Camera2
/// <c>JPEG_ORIENTATION</c> formulas.
/// </summary>
public static class CameraFrameOrientation
{
    /// <summary>
    /// <paramref name="sensorOrientationDegrees"/> = CameraCharacteristics.SENSOR_ORIENTATION
    /// (0/90/180/270; clockwise rotation that makes the sensor image upright in the device's
    /// natural orientation). <paramref name="displayRotationDegrees"/> = Surface.ROTATION_*
    /// expressed in degrees (0/90/180/270; counter-clockwise rotation of the display from natural).
    /// Returns the clockwise rotation (0/90/180/270) to apply to the frame.
    /// </summary>
    public static int ComputeRotationDegrees(int sensorOrientationDegrees, int displayRotationDegrees, bool isFrontFacing)
    {
        var sensor = Normalize(sensorOrientationDegrees);
        var display = Normalize(displayRotationDegrees);
        return isFrontFacing
            ? (sensor + display) % 360
            : (sensor - display + 360) % 360;
    }

    private static int Normalize(int degrees)
    {
        var d = ((degrees % 360) + 360) % 360;
        return (d + 45) / 90 % 4 * 90; // snap to the nearest multiple of 90
    }
}
