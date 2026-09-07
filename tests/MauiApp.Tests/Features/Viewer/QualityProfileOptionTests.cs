using NdiForAndroid.Features.Viewer.Models;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Testing;
using Xunit;

namespace NdiForAndroid.Tests.Features.Viewer;

public class QualityProfileOptionTests
{
    [Theory]
    [InlineData(QualityProfile.Smooth, TestIds.ViewerQualitySmooth)]
    [InlineData(QualityProfile.Balanced, TestIds.ViewerQualityBalanced)]
    [InlineData(QualityProfile.High, TestIds.ViewerQualityHigh)]
    public void AutomationIdFor_KnownProfiles_MapToTestIds(QualityProfile profile, string expected)
    {
        Assert.Equal(expected, QualityProfileOption.AutomationIdFor(profile));
    }

    [Fact]
    public void AutomationIdFor_EveryEnumValue_IsDistinct()
    {
        var ids = Enum.GetValues<QualityProfile>().Select(QualityProfileOption.AutomationIdFor).ToList();

        Assert.All(ids, id => Assert.False(string.IsNullOrEmpty(id)));
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Constructor_SetsLabelDescriptionAndId()
    {
        var option = new QualityProfileOption(QualityProfile.Smooth);

        Assert.Equal("Smooth", option.Label);
        Assert.Equal(QualityProfileOption.DescribeProfile(QualityProfile.Smooth), option.Description);
        Assert.False(option.IsSelected);
    }

    [Fact]
    public void DescribeProfile_EveryEnumValue_IsNonEmpty()
    {
        foreach (var profile in Enum.GetValues<QualityProfile>())
            Assert.False(string.IsNullOrWhiteSpace(QualityProfileOption.DescribeProfile(profile)));
    }
}
