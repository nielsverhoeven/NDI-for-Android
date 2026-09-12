using System.ComponentModel;
using NdiForAndroid.Features.Navigation.Services;
using NdiForAndroid.Features.Sources.ViewModels;
using NdiForAndroid.Features.Viewer.Services;
using NdiForAndroid.Features.Viewer.ViewModels;

namespace NdiForAndroid.Features.Sources.Views;

public partial class SourceListPage : ContentPage
{
    private readonly IWindowSizeClassService _windowSizeClassService;
    private readonly ViewerFullScreenChromeController _fullScreenChromeController;

    private bool _isPageVisible;
    private bool _isPaneFullScreen;
    private ViewerViewModel? _attachedPaneViewModel;

    public SourceListPage(
        SourceListViewModel viewModel,
        IWindowSizeClassService windowSizeClassService,
        ViewerFullScreenChromeController fullScreenChromeController)
    {
        InitializeComponent();
        BindingContext = viewModel;

        _windowSizeClassService = windowSizeClassService;
        _fullScreenChromeController = fullScreenChromeController;
        _windowSizeClassService.Changed += OnWindowSizeClassChanged;
        viewModel.PropertyChanged += OnSourceListViewModelPropertyChanged;
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

        AttachPaneIfReady();
    }

    protected override void OnDisappearing()
    {
        _isPageVisible = false;
        // Always stop the pane's render loop when the page (or the app) goes away.
        ViewerPane.StopRendering();
        _fullScreenChromeController.Detach();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
        => _fullScreenChromeController.HandleBackButton() || base.OnBackButtonPressed();

    private void OnWindowSizeClassChanged(object? sender, WindowSizeClass sizeClass)
        => ApplySizeClass(sizeClass);

    private void OnSourceListViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SourceListViewModel.PaneViewer))
            AttachPaneIfReady();
    }

    /// <summary>
    /// PaneViewer is null until the user's first Expanded-window "Watch" tap (lazily created,
    /// singleton-lifetime once created — see SourceListViewModel). Attach as soon as it exists
    /// and this page is visible; re-entrant and safe to call from both OnAppearing and the
    /// PaneViewer PropertyChanged handler.
    /// </summary>
    private void AttachPaneIfReady()
    {
        if (BindingContext is not SourceListViewModel { PaneViewer: { } pane })
            return;

        if (!ReferenceEquals(_attachedPaneViewModel, pane))
        {
            _attachedPaneViewModel = pane;
            pane.PropertyChanged += OnPaneViewModelPropertyChanged;
        }

        if (_isPageVisible)
            _fullScreenChromeController.Attach(this, pane);
    }

    private void OnPaneViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewerViewModel.IsFullScreen) && sender is ViewerViewModel vm)
            ApplyPaneFullScreen(vm.IsFullScreen);
    }

    /// <summary>Layout plumbing only: 2*/3* two-pane split on Expanded, single column otherwise.</summary>
    private void ApplySizeClass(WindowSizeClass sizeClass)
    {
        // A size-class change mid-full-screen must never restore the list column under the
        // video — full screen covers the whole window until the user exits it.
        if (_isPaneFullScreen)
            return;

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

    /// <summary>Full screen covers the whole window, not just the pane: collapses the header row
    /// and the list column to 0, and restores the size-class-appropriate widths (via
    /// <see cref="ApplySizeClass"/>) on exit.</summary>
    private void ApplyPaneFullScreen(bool isFullScreen)
    {
        if (_isPaneFullScreen == isFullScreen)
            return;

        _isPaneFullScreen = isFullScreen;
        ListHeader.IsVisible = !isFullScreen;

        if (isFullScreen)
        {
            ListColumn.Width = new GridLength(0);
            PaneColumn.Width = new GridLength(1, GridUnitType.Star);
        }
        else
        {
            ApplySizeClass(_windowSizeClassService.Current);
        }
    }
}
