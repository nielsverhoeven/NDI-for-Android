using NdiForAndroid.Features.Navigation.Services;
using NdiForAndroid.Features.Sources.ViewModels;

namespace NdiForAndroid.Features.Sources.Views;

public partial class SourceListPage : ContentPage
{
    private readonly IWindowSizeClassService _windowSizeClassService;

    private bool _isPageVisible;

    public SourceListPage(SourceListViewModel viewModel, IWindowSizeClassService windowSizeClassService)
    {
        InitializeComponent();
        BindingContext = viewModel;

        _windowSizeClassService = windowSizeClassService;
        _windowSizeClassService.Changed += OnWindowSizeClassChanged;
        ApplySizeClass(_windowSizeClassService.Current);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _isPageVisible = true;

        if (BindingContext is SourceListViewModel vm && vm.RefreshCommand.CanExecute(null))
            vm.RefreshCommand.Execute(null);

        // Resume the embedded pane's render loop only when it is actually shown.
        if (_windowSizeClassService.Current == WindowSizeClass.Expanded)
            ViewerPane.StartRendering();
    }

    protected override void OnDisappearing()
    {
        _isPageVisible = false;
        // Always stop the pane's render loop when the page (or the app) goes away.
        ViewerPane.StopRendering();
        base.OnDisappearing();
    }

    private void OnWindowSizeClassChanged(object? sender, WindowSizeClass sizeClass)
        => ApplySizeClass(sizeClass);

    /// <summary>Layout plumbing only: 2*/3* two-pane split on Expanded, single column otherwise.</summary>
    private void ApplySizeClass(WindowSizeClass sizeClass)
    {
        if (sizeClass == WindowSizeClass.Expanded)
        {
            ListColumn.Width = new GridLength(2, GridUnitType.Star);
            PaneColumn.Width = new GridLength(3, GridUnitType.Star);
            ViewerPane.IsVisible = true;

            if (_isPageVisible)
                ViewerPane.StartRendering();
        }
        else
        {
            ListColumn.Width = new GridLength(1, GridUnitType.Star);
            PaneColumn.Width = new GridLength(0);
            ViewerPane.IsVisible = false;
            ViewerPane.StopRendering();
        }
    }
}
