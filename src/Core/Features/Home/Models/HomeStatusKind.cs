namespace NdiForAndroid.Features.Home.Models;

/// <summary>Semantic state of a Home status card, so the view can colour it without parsing the
/// displayed text (#370 home-nav-08).</summary>
public enum HomeStatusKind
{
    Idle,
    Active,
    Failure,
}
