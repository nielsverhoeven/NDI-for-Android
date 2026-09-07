using CommunityToolkit.Mvvm.ComponentModel;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Testing;

namespace NdiForAndroid.Features.Viewer.Models;

/// <summary>
/// One selectable entry of the viewer's quality-profile strip. Built from the
/// <see cref="QualityProfile"/> enum by <c>ViewerViewModel.AvailableProfiles</c>, so adding or
/// renaming an enum value can no longer desynchronise the UI (#330).
/// </summary>
public sealed partial class QualityProfileOption : ObservableObject
{
    public QualityProfileOption(QualityProfile profile)
    {
        Profile = profile;
        Label = profile.ToString();
        Description = DescribeProfile(profile);
        AutomationId = AutomationIdFor(profile);
    }

    public QualityProfile Profile { get; }

    /// <summary>Button text.</summary>
    public string Label { get; }

    /// <summary>Accessibility text (SemanticProperties.Description): what the tier actually does.</summary>
    public string Description { get; }

    /// <summary>
    /// Appium locator, sourced from <see cref="TestIds"/> (the three shipped values) — bound to
    /// the templated Button's AutomationId so the page object keeps working.
    /// </summary>
    public string AutomationId { get; }

    /// <summary>True for the profile currently applied to the receiver; drives the selected style.</summary>
    [ObservableProperty]
    private bool _isSelected;

    public static string AutomationIdFor(QualityProfile profile) => profile switch
    {
        QualityProfile.Smooth   => TestIds.ViewerQualitySmooth,
        QualityProfile.Balanced => TestIds.ViewerQualityBalanced,
        QualityProfile.High     => TestIds.ViewerQualityHigh,
        // A future enum value still gets a stable, unique id ("viewer.quality.<name>"); add a
        // TestIds constant for it when a page object needs to tap it.
        _ => $"viewer.quality.{profile.ToString().ToLowerInvariant()}",
    };

    /// <summary>Honest descriptions: only Smooth changes the NDI bandwidth tier (NdiViewerBridge.MapBandwidth).</summary>
    public static string DescribeProfile(QualityProfile profile) => profile switch
    {
        QualityProfile.Smooth   => "Smooth: low-bandwidth preview stream, smoothest on a weak connection",
        QualityProfile.Balanced => "Balanced: full-quality stream (default)",
        QualityProfile.High     => "High: full-quality stream, prioritises picture over frame rate",
        _ => profile.ToString(),
    };
}
