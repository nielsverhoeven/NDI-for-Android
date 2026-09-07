using System.Text.RegularExpressions;
using NdiForAndroid.Features.Settings.Models;
using Xunit;

namespace NdiForAndroid.Tests.Features.Settings;

/// <summary>
/// WCAG 2.2 AA guards for the palette the app actually applies (#344, #372, #373, #376, #329).
/// </summary>
public class AppearancePaletteContrastTests
{
    private const double NormalText = 4.5; // SC 1.4.3
    private const double NonText = 3.0;    // SC 1.4.11

    public static IEnumerable<object[]> Palettes() =>
    [
        ["Dark", AppearancePalette.Dark],
        ["Light", AppearancePalette.Light],
    ];

    public static IEnumerable<object[]> Accents() =>
        Enum.GetValues<AccentColorOption>().Select(a => new object[] { a });

    [Fact]
    public void WcagContrast_WhiteOnBlack_Is21() =>
        Assert.Equal(21.0, WcagContrast.Ratio("#FFFFFF", "#000000"), 3);

    [Fact]
    public void WcagContrast_ReproducesTheIssueFigure_ForOldPrimary() =>
        Assert.Equal(4.489, WcagContrast.Ratio("#FFFFFF", "#2B7CB8"), 3);

    [Theory]
    [MemberData(nameof(Accents))]
    public void Accent_WhiteButtonText_MeetsNormalText(AccentColorOption accent) =>
        AssertAtLeast(NormalText, AppearancePalette.OnPrimary, AppearancePalette.Accent(accent), $"OnPrimary on {accent} accent");

    [Theory]
    [MemberData(nameof(Accents))]
    public void Accent_AgainstDarkAndLightPage_MeetsNonText(AccentColorOption accent)
    {
        AssertAtLeast(NonText, AppearancePalette.Accent(accent), AppearancePalette.Dark.PageBackground, $"{accent} accent ring/track on dark page");
        AssertAtLeast(NonText, AppearancePalette.Accent(accent), AppearancePalette.Light.PageBackground, $"{accent} accent ring/track on light page");
    }

    [Theory]
    [MemberData(nameof(Accents))]
    public void AccentGraphic_AgainstPageAndCard_MeetsNonTextInBothThemes(AccentColorOption accent)
    {
        // Non-text accent uses (radio ring/dot, Switch track, spinners, sheet tab indicator) bind
        // AccentGraphic rather than the darkened Accent/Primary fill, because that fill drops as
        // low as 2.72:1 against the dark CardBackground — below the 3:1 SC 1.4.11 bar (#344/#373).
        var dark = AppearancePalette.AccentGraphic(isLight: false, accent);
        AssertAtLeast(NonText, dark, AppearancePalette.Dark.PageBackground, $"{accent} AccentGraphic on dark page");
        AssertAtLeast(NonText, dark, AppearancePalette.Dark.CardBackground, $"{accent} AccentGraphic on dark card");

        var light = AppearancePalette.AccentGraphic(isLight: true, accent);
        AssertAtLeast(NonText, light, AppearancePalette.Light.PageBackground, $"{accent} AccentGraphic on light page");
        AssertAtLeast(NonText, light, AppearancePalette.Light.CardBackground, $"{accent} AccentGraphic on light card");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void ShellTabUnselected_OnShellBackground_MeetsNormalText(string name, ThemePalette p) =>
        AssertAtLeast(NormalText, p.ShellTabUnselected, p.ShellBackground, $"{name} unselected tab/rail caption");

    [Theory]
    [MemberData(nameof(Palettes))]
    public void TextPlaceholder_OnInputBackground_MeetsNormalText(string name, ThemePalette p) =>
        AssertAtLeast(NormalText, p.TextPlaceholder, p.InputBackground, $"{name} placeholder");

    [Theory]
    [MemberData(nameof(Palettes))]
    public void TextSecondary_OnPageAndCard_MeetsNormalText(string name, ThemePalette p)
    {
        AssertAtLeast(NormalText, p.TextSecondary, p.PageBackground, $"{name} TextSecondary on page");
        AssertAtLeast(NormalText, p.TextSecondary, p.CardBackground, $"{name} TextSecondary on card");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void TextPrimary_OnPageCardAndInput_MeetsNormalText(string name, ThemePalette p)
    {
        AssertAtLeast(NormalText, p.TextPrimary, p.PageBackground, $"{name} TextPrimary on page");
        AssertAtLeast(NormalText, p.TextPrimary, p.CardBackground, $"{name} TextPrimary on card");
        AssertAtLeast(NormalText, p.TextPrimary, p.InputBackground, $"{name} TextPrimary on input");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void ErrorAndSuccessText_OnPageAndCard_MeetNormalText(string name, ThemePalette p)
    {
        AssertAtLeast(NormalText, p.ErrorText, p.PageBackground, $"{name} ErrorText on page");
        AssertAtLeast(NormalText, p.ErrorText, p.CardBackground, $"{name} ErrorText on card");
        AssertAtLeast(NormalText, p.SuccessText, p.PageBackground, $"{name} SuccessText on page");
        AssertAtLeast(NormalText, p.SuccessText, p.CardBackground, $"{name} SuccessText on card");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void WarningText_OnPageAndCard_MeetsNormalText(string name, ThemePalette p)
    {
        // #329: Diagnostic Log level colours must be theme-aware tokens with the same AA guarantee.
        AssertAtLeast(NormalText, p.WarningText, p.PageBackground, $"{name} WarningText on page");
        AssertAtLeast(NormalText, p.WarningText, p.CardBackground, $"{name} WarningText on card");
    }

    [Fact]
    public void ErrorAndSuccessFill_CarryWhiteText()
    {
        AssertAtLeast(NormalText, AppearancePalette.OnPrimary, AppearancePalette.ErrorFill, "OnPrimary on ErrorFill");
        AssertAtLeast(NormalText, AppearancePalette.OnPrimary, AppearancePalette.SuccessFill, "OnPrimary on SuccessFill");
    }

    [Fact]
    public void ErrorText_IsStillDistinguishableFromSuccessAndSecondary()
    {
        // VIEWER-6 acceptance: the error colour must not be confusable with success/neutral text.
        foreach (var p in new[] { AppearancePalette.Dark, AppearancePalette.Light })
        {
            Assert.NotEqual(p.ErrorText, p.SuccessText);
            Assert.NotEqual(p.ErrorText, p.TextSecondary);
            Assert.NotEqual(p.ErrorText, p.WarningText);
            Assert.True(RedDominance(p.ErrorText) > 0, $"{p.ErrorText} is not red-dominant");
        }
    }

    [Fact]
    public void WarningText_IsStillDistinguishableFromErrorAndSuccess()
    {
        // #329: a Warning row must not read as an Error (red) or a normal (secondary) log line.
        foreach (var p in new[] { AppearancePalette.Dark, AppearancePalette.Light })
        {
            Assert.NotEqual(p.WarningText, p.ErrorText);
            Assert.NotEqual(p.WarningText, p.SuccessText);
            Assert.NotEqual(p.WarningText, p.TextSecondary);
        }
    }

    [Fact]
    public void ColorsXaml_Baseline_MatchesDarkPaletteAndBlueAccent()
    {
        var xaml = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "MauiApp", "Resources", "Styles", "Colors.xaml"));
        var keys = Regex.Matches(xaml, "<Color x:Key=\"(\\w+)\">(#[0-9A-Fa-f]{6,8})</Color>")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.ToUpperInvariant());
        var d = AppearancePalette.Dark;

        Assert.Equal(AppearancePalette.Accent(AccentColorOption.Blue), keys["Primary"]);
        Assert.Equal(AppearancePalette.AccentGraphic(isLight: false, AccentColorOption.Blue), keys["AccentGraphic"]);
        Assert.Equal(d.PageBackground, keys["PageBackground"]);
        Assert.Equal(d.CardBackground, keys["CardBackground"]);
        Assert.Equal(d.InputBackground, keys["InputBackground"]);
        Assert.Equal(d.ShellBackground, keys["ShellBackground"]);
        Assert.Equal(d.ShellTabSelected, keys["ShellTabSelected"]);
        Assert.Equal(d.ShellTabUnselected, keys["ShellTabUnselected"]);
        Assert.Equal(d.TextPrimary, keys["TextPrimary"]);
        Assert.Equal(d.TextSecondary, keys["TextSecondary"]);
        Assert.Equal(d.TextPlaceholder, keys["TextPlaceholder"]);
        Assert.Equal(d.ErrorText, keys["ErrorText"]);
        Assert.Equal(d.SuccessText, keys["SuccessText"]);
        Assert.Equal(d.WarningText, keys["WarningText"]);
        Assert.Equal(AppearancePalette.ErrorFill, keys["ErrorFill"]);
        Assert.Equal(AppearancePalette.SuccessFill, keys["SuccessFill"]);
        Assert.Equal(d.BorderColor, keys["BorderColor"]);
        Assert.Equal(d.DividerColor, keys["DividerColor"]);
    }

    private static void AssertAtLeast(double min, string fg, string bg, string what)
    {
        var ratio = WcagContrast.Ratio(fg, bg);
        Assert.True(ratio >= min, $"{what}: {fg} on {bg} is {ratio:0.000}:1, below the {min}:1 WCAG AA bar.");
    }

    private static int RedDominance(string hex)
    {
        var (r, g, b) = WcagContrast.Rgb(hex);
        return r - Math.Max(g, b);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NdiForAndroid.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("NdiForAndroid.sln not found above " + AppContext.BaseDirectory);
    }
}

/// <summary>WCAG 2.x relative luminance + contrast, same maths as UITests' SampledColor.</summary>
internal static class WcagContrast
{
    public static (int R, int G, int B) Rgb(string hex)
    {
        var h = hex.TrimStart('#');
        if (h.Length == 8) h = h[2..]; // drop alpha (#AARRGGBB)
        return (System.Convert.ToInt32(h[..2], 16), System.Convert.ToInt32(h[2..4], 16), System.Convert.ToInt32(h[4..6], 16));
    }

    public static double Luminance(string hex)
    {
        static double Channel(int v) { var s = v / 255d; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        var (r, g, b) = Rgb(hex);
        return 0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);
    }

    public static double Ratio(string a, string b)
    {
        var (hi, lo) = Luminance(a) >= Luminance(b) ? (Luminance(a), Luminance(b)) : (Luminance(b), Luminance(a));
        return (hi + 0.05) / (lo + 0.05);
    }
}
