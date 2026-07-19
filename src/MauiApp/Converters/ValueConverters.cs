using System.Globalization;
using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Services;

namespace NdiForAndroid.Converters;

/// <summary>Returns <c>true</c> when the bound value is not null.</summary>
public sealed class IsNotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Negates a boolean value.</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}

/// <summary>
/// Maps a <see cref="DiscoveryServerConnectionState"/> to a status color, resolved through the
/// app resource dictionary at convert time so runtime theme switches pick up current values
/// (the binding re-evaluates on every probe-loop state change).
/// </summary>
public sealed class DiscoveryServerStateColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            DiscoveryServerConnectionState.Connected => ResolveColor("SuccessGreen", Colors.Green),
            DiscoveryServerConnectionState.Unreachable => ResolveColor("ErrorRed", Colors.Red),
            DiscoveryServerConnectionState.Checking => ResolveColor("TextSecondary", Colors.Gray),
            _ => ResolveColor("TextPlaceholder", Colors.Gray),
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Color ResolveColor(string resourceKey, Color fallback)
        => Application.Current?.Resources.TryGetValue(resourceKey, out var resolved) == true && resolved is Color color
            ? color
            : fallback;
}

/// <summary>Maps a <see cref="VideoInputKind"/> to a user-facing label (Picker display).</summary>
public sealed class VideoInputKindDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            VideoInputKind.Screen => "Screen",
            VideoInputKind.CameraFront => "Front camera",
            VideoInputKind.CameraRear => "Rear camera",
            _ => value?.ToString() ?? string.Empty,
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
