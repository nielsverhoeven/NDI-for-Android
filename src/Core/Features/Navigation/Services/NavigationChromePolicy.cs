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
/// (<c>Shell.SetTabBarIsVisible(PrimaryTabBar, …)</c>), and <c>AppShell</c> mirrors a value onto every
/// section, <c>ShellContent</c> and page under it (<see cref="MirrorValue"/>). A placement change
/// therefore swaps chrome only and never changes <c>Shell.CurrentItem</c>, the current page, or any
/// navigation stack.
/// </para>
/// <para>
/// Suppression (a host page showing full screen) hides the rail, and never changes
/// <see cref="Resolve"/>'s <c>BottomBarVisibleAtItemScope</c>: folding it into the item-scope value
/// would give that property a second writer. For the bottom bar it is a <b>content-scope</b> input
/// instead (#449): on MAUI ≥ 10.0.70 <c>Shell.TabBarIsVisible</c> on a <c>ShellContent</c> is the
/// effective bottom-bar scope — <c>ShellItem.ShowTabs</c> starts its walk there when it is set, and
/// Android re-evaluates on its change — so <see cref="MirrorValue"/> maps suppression to content
/// <c>false</c> for the one section hosting full screen, whose pages stay
/// <c>ViewerFullScreenChromeController</c>'s (page scope: the nav bar, and the page-first contract).
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

    /// <summary>
    /// What <c>AppShell.MirrorTabBarVisibilityToDescendants</c> writes under one <c>ShellSection</c>
    /// of <c>PrimaryTabBar</c> (#395/#449).
    /// </summary>
    /// <remarks>
    /// Content scope is the effective bottom-bar scope on MAUI ≥ 10.0.70 (see the class remarks), and
    /// the content value is load-bearing in both directions: without it the bar stays up on the rail
    /// after a cold launch or a rotation (Nexus 6, run 38040750641), and with the placement's
    /// <c>true</c> on it a page-scoped <c>false</c> is never consulted (#449). So every section and
    /// content is written on every placement change, and the section hosting full screen gets
    /// <c>false</c> while chrome is suppressed — the one mechanism this MAUI demonstrably honours. Its
    /// pages are never written from the shell: page scope belongs to the full-screen controller, and
    /// a shell write there would outrank the controller's <c>ClearValue</c> fallback. On exit
    /// <c>IsChromeSuppressed</c> flips first, so the placement value is back on the content before the
    /// controller clears the page.
    /// </remarks>
    /// <param name="bottomBarVisibleAtItemScope"><see cref="Resolve"/>'s item-scope value.</param>
    /// <param name="isChromeSuppressed">
    /// <see cref="ViewModels.AdaptiveShellStateViewModel.IsChromeSuppressed"/>.
    /// </param>
    /// <param name="isFullScreenSection">Whether this section hosts the page showing full screen.</param>
    /// <returns>
    /// <c>SectionAndContentValue</c>: the value written to <c>Shell.TabBarIsVisible</c> on the section
    /// and on each of its <c>ShellContent</c>s. <c>WritePages</c>: whether the section's root and
    /// pushed pages are written too (never for the full-screen section while suppressed).
    /// </returns>
    public static (bool SectionAndContentValue, bool WritePages) MirrorValue(
        bool bottomBarVisibleAtItemScope,
        bool isChromeSuppressed,
        bool isFullScreenSection)
    {
        if (isChromeSuppressed && isFullScreenSection)
            return (SectionAndContentValue: false, WritePages: false);

        return (SectionAndContentValue: bottomBarVisibleAtItemScope, WritePages: true);
    }
}
