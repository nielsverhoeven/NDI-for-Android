using NdiForAndroid.Features.DeepLinking.Services;
using Xunit;

namespace NdiForAndroid.Tests.Features.DeepLinking;

public class DeepLinkRouteResolverTests
{
    private readonly DeepLinkRouteResolver _sut = new();

    [Theory]
    [InlineData("ndi://view?sourceId=PC%20(Cam)", DeepLinkType.View, "PC (Cam)")]
    [InlineData("ndi://stream?sourceId=PC%20(Cam)", DeepLinkType.Stream, "PC (Cam)")]
    public void Resolve_QueryForm_NormalizesDestinationAndSourceId(string uri, DeepLinkType expectedType, string expectedSourceId)
    {
        var route = _sut.Resolve(uri, out var error);

        Assert.NotNull(route);
        Assert.Null(error);
        Assert.Equal(expectedType, route!.Type);
        Assert.Equal(expectedSourceId, route.SourceId);
    }

    [Theory]
    [InlineData("ndi://view/192.168.1.10:5960", DeepLinkType.View, "192.168.1.10:5960")]
    [InlineData("ndi://stream/192.168.1.10:5960", DeepLinkType.Stream, "192.168.1.10:5960")]
    public void Resolve_PathForm_NormalizesDestinationAndSourceId(string uri, DeepLinkType expectedType, string expectedSourceId)
    {
        var route = _sut.Resolve(uri, out var error);

        Assert.NotNull(route);
        Assert.Null(error);
        Assert.Equal(expectedType, route!.Type);
        Assert.Equal(expectedSourceId, route.SourceId);
    }

    [Fact]
    public void Resolve_EmptyUri_FailsWithMessage()
    {
        var route = _sut.Resolve("", out var error);

        Assert.Null(route);
        Assert.Equal("Invalid deep link: empty URI.", error);
    }

    [Fact]
    public void Resolve_MalformedUri_FailsWithMessage()
    {
        var route = _sut.Resolve("not a uri at all::::", out var error);

        Assert.Null(route);
        Assert.Equal("Invalid deep link: malformed URI.", error);
    }

    [Fact]
    public void Resolve_WrongScheme_Fails()
    {
        var route = _sut.Resolve("https://view?sourceId=x", out var error);

        Assert.Null(route);
        Assert.Contains("Unsupported scheme", error);
    }

    [Fact]
    public void Resolve_UnknownAction_Fails()
    {
        var route = _sut.Resolve("ndi://frobnicate?sourceId=x", out var error);

        Assert.Null(route);
        Assert.Contains("Unknown action", error);
    }

    [Fact]
    public void Resolve_ViewWithoutSourceIdOrPath_FailsWithMissingSourceIdMessage()
    {
        var route = _sut.Resolve("ndi://view", out var error);

        Assert.Null(route);
        Assert.Equal("Invalid deep link: missing 'sourceId' parameter.", error);
    }

    [Fact]
    public void Resolve_QueryFormTakesPrecedence_OverPathForm()
    {
        var route = _sut.Resolve("ndi://view/ignored-path?sourceId=from-query", out var error);

        Assert.NotNull(route);
        Assert.Null(error);
        Assert.Equal("from-query", route!.SourceId);
    }
}
