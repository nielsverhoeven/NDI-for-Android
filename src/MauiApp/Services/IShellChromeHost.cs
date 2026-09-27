namespace NdiForAndroid.Services;

/// <summary>
/// Implemented by every page Shell can display (the four tab roots and the pushed
/// <c>ViewerPage</c>/<c>DiagnosticLogPage</c>) so <c>AppShell.ApplyPlacement</c> can make Android
/// re-evaluate the bottom bar after a placement change (#395).
/// </summary>
/// <remarks>
/// <para>
/// <c>AppShell</c> hides the bottom bar at ShellItem scope, with
/// <c>Shell.SetTabBarIsVisible(PrimaryTabBar, …)</c>. Android's <c>ShellItemRenderer</c> only
/// re-evaluates the bar's visibility when the displayed page changes, or when the displayed page
/// raises <c>PropertyChanged("TabBarIsVisible")</c>. A write on the <c>TabBar</c> item raises
/// neither, so a rotation would leave the bar as it was until the next navigation.
/// </para>
/// <para>
/// <see cref="RefreshShellChrome"/> raises that notification on the page itself without writing
/// the property: page scope belongs to <c>ViewerFullScreenChromeController</c>, and a value written
/// here would outrank the placement value for good. Implementations are code-behind only:
/// <c>OnPropertyChanged(Shell.TabBarIsVisibleProperty.PropertyName)</c>.
/// </para>
/// </remarks>
public interface IShellChromeHost
{
    /// <summary>Raises <c>PropertyChanged</c> for <c>Shell.TabBarIsVisible</c> on this page.</summary>
    void RefreshShellChrome();
}
