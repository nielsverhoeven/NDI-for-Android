using NdiForAndroid.Features.Navigation.Models;

namespace NdiForAndroid.Features.Navigation.Services;

/// <summary>
/// Pure mapping from the navigation placement and the full-screen suppression flag to the Shell
/// chrome <c>AppShell.ApplyPlacement</c> applies (#395).
/// </summary>
/// <remarks>
/// <para>
/// There is one <c>TabBar</c> route family. The left rail is <c>FlyoutBehavior.Locked</c> plus the
/// custom <c>Shell.FlyoutContent</c>; the bottom bar is hidden at <b>ShellItem scope</b>
/// (<c>Shell.SetTabBarIsVisible(PrimaryTabBar, …)</c>). A placement change therefore swaps chrome only
/// and never changes <c>Shell.CurrentItem</c>, the current page, or any navigation stack.
/// </para>
/// <para>
/// Suppression (a host page showing full screen) hides the rail only. It never changes
/// <see cref="Resolve"/>'s <c>BottomBarVisibleAtItemScope</c>: the bottom bar is hidden for full
/// screen at <b>page scope</b> by <c>ViewerFullScreenChromeController</c>, and page scope wins over
/// item scope. Folding suppression into the item-scope value would give that property a second
/// writer, and the controller's exit <c>ClearValue</c> would then fall back to a stale value.
/// </para>
/// </remarks>
public static class NavigationChromePolicy
{
    /// <param name="placement">The placement the adaptive navigation policy resolved.</param>
    /// <param name="isChromeSuppressed">
    /// <see cref="ViewModels.AdaptiveShellStateViewModel.IsChromeSuppressed"/>.
    /// </param>
    /// <returns>
    /// <c>RailVisible</c>: whether the Shell flyout (the rail) is <c>Locked</c> open rather than
    /// <c>Disabled</c>. <c>BottomBarVisibleAtItemScope</c>: the value written to
    /// <c>Shell.TabBarIsVisible</c> on the <c>TabBar</c> item.
    /// </returns>
    public static (bool RailVisible, bool BottomBarVisibleAtItemScope) Resolve(
        NavigationPlacementMode placement,
        bool isChromeSuppressed)
    {
        var rail = placement == NavigationPlacementMode.LeftRail;
        return (RailVisible: rail && !isChromeSuppressed, BottomBarVisibleAtItemScope: !rail);
    }
}
