using Xunit;

namespace NdiForAndroid.Tests.Composition;

/// <summary>
/// Tab-root lifetime rule (#352/#359): a ShellContent-hosted page whose ViewModel subscribes to a
/// singleton event must be registered Singleton together with that ViewModel, because MAUI's Android
/// Shell re-resolves the page on every tab entry and the page never disposes its ViewModel. The test
/// project cannot load the MAUI composition root, so the rule is asserted against its source text.
/// </summary>
public sealed class TabRootLifetimeRegistrationTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NdiForAndroid.sln")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new InvalidOperationException($"NdiForAndroid.sln not found above {AppContext.BaseDirectory}");
    }

    private static string MauiProgramSource() =>
        File.ReadAllText(Path.Combine(RepoRoot(), "src", "MauiApp", "MauiProgram.cs"));

    [Theory]
    [InlineData("HomeViewModel")]
    [InlineData("OutputViewModel")]
    [InlineData("SourceListViewModel")]
    [InlineData("Features.Home.Views.HomePage")]
    [InlineData("Features.Output.Views.OutputPage")]
    [InlineData("Features.Sources.Views.SourceListPage")]
    public void TabRootPagesAndTheirViewModels_AreRegisteredAsSingletons(string type)
    {
        var source = MauiProgramSource();

        Assert.Contains($"AddSingleton<{type}>()", source);
        Assert.DoesNotContain($"AddTransient<{type}>()", source);
    }
}
