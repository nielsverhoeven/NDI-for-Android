using NdiForAndroid.Features.Navigation.Services;
using NdiForAndroid.Features.Settings.ViewModels;

namespace NdiForAndroid.Features.Settings.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is not SettingsViewModel vm)
            return;

        if (vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);

        vm.StartConnectionMonitoring();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is SettingsViewModel vm)
            vm.StopConnectionMonitoring();
    }

    /// <summary>
    /// Layout plumbing only: collapses the 220dp section rail into a wrapping selector above
    /// the panel at Compact width, where a fixed rail would leave too little room for the detail
    /// panel.
    /// </summary>
    /// <remarks>
    /// Reads the width MAUI hands this override rather than injecting
    /// <see cref="IWindowSizeClassService"/>: this page is transient (recreated on every Settings
    /// visit), so subscribing to that singleton's <c>Changed</c> event in the constructor would
    /// leak one subscription per visit.
    /// </remarks>
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width <= 0)
            return;

        var isCompact = WindowSizeClassService.Classify(width) == WindowSizeClass.Compact;

        RailColumn.Width = isCompact ? new GridLength(0) : new GridLength(220);
        VerticalRail.IsVisible = !isCompact;
        CompactRail.IsVisible = isCompact;
        SectionsGrid.ColumnSpacing = isCompact ? 0 : 16;
        SectionsGrid.RowSpacing = isCompact ? 12 : 0;
    }
}
