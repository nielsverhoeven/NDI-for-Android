using NdiForAndroid.Features.Navigation.Services;
using Xunit;

namespace NdiForAndroid.Tests.Features.Navigation;

public sealed class WindowSizeClassServiceTests
{
    [Fact]
    public void Current_DefaultsToCompact()
    {
        var sut = new WindowSizeClassService();

        Assert.Equal(WindowSizeClass.Compact, sut.Current);
    }

    // Material breakpoints: Compact < 600dp, Medium 600–840dp (inclusive), Expanded > 840dp.
    [Theory]
    [InlineData(320d, WindowSizeClass.Compact)]
    [InlineData(599d, WindowSizeClass.Compact)]
    [InlineData(600d, WindowSizeClass.Medium)]
    [InlineData(840d, WindowSizeClass.Medium)]
    [InlineData(841d, WindowSizeClass.Expanded)]
    [InlineData(1280d, WindowSizeClass.Expanded)]
    public void UpdateFromWidth_MapsBreakpointsToExpectedClass(double widthDp, WindowSizeClass expected)
    {
        var sut = new WindowSizeClassService();

        sut.UpdateFromWidth(widthDp);

        Assert.Equal(expected, sut.Current);
    }

    [Fact]
    public void UpdateFromWidth_RaisesChanged_OnlyOnClassTransitions()
    {
        var sut = new WindowSizeClassService();
        var raisedClasses = new List<WindowSizeClass>();
        sut.Changed += (_, sizeClass) => raisedClasses.Add(sizeClass);

        sut.UpdateFromWidth(400);  // Compact → Compact: no event (initial class is Compact)
        sut.UpdateFromWidth(700);  // Compact → Medium: event
        sut.UpdateFromWidth(800);  // Medium → Medium: no event
        sut.UpdateFromWidth(900);  // Medium → Expanded: event
        sut.UpdateFromWidth(500);  // Expanded → Compact: event

        Assert.Equal(
            new[] { WindowSizeClass.Medium, WindowSizeClass.Expanded, WindowSizeClass.Compact },
            raisedClasses);
    }

    [Fact]
    public void UpdateFromWidth_IsIdempotent_ForRepeatedSameWidth()
    {
        var sut = new WindowSizeClassService();
        var eventCount = 0;
        sut.Changed += (_, _) => eventCount++;

        sut.UpdateFromWidth(900);
        sut.UpdateFromWidth(900);
        sut.UpdateFromWidth(900);

        Assert.Equal(1, eventCount);
        Assert.Equal(WindowSizeClass.Expanded, sut.Current);
    }
}
