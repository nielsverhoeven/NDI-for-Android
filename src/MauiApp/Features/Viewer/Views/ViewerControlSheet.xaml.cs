using System.ComponentModel;
using NdiForAndroid.Features.Viewer.ViewModels;

namespace NdiForAndroid.Features.Viewer.Views;

/// <summary>
/// Hand-built draggable bottom sheet (no <c>CommunityToolkit.Maui</c> dependency) hosting the
/// same playback/camera content as <see cref="ViewerControlDeck"/>, tabbed instead of columned.
/// </summary>
public partial class ViewerControlSheet : ContentView
{
    private const double DefaultExpandedHeight = 440;
    private const double DefaultHalfHeight = 320;
    private const double ExpandedHeightRatio = 0.8;
    private const uint AnimationDurationMs = 200;

    private double _expandedHeight = DefaultExpandedHeight;
    private double _halfHeight = DefaultHalfHeight;
    private bool _isExpanded;
    private bool _isPtzTabSelected;
    private double _panStartTranslationY;
    private ViewerViewModel? _boundViewModel;

    public ViewerControlSheet()
    {
        InitializeComponent();

        SheetContainer.TranslationY = _expandedHeight - _halfHeight;
        PlaybackTabButton.Clicked += (_, _) => SelectTab(isPtz: false);
        PtzTabButton.Clicked += (_, _) => SelectTab(isPtz: true);

        SizeChanged += (_, _) => ApplySheetHeights();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (_boundViewModel is not null)
            _boundViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _boundViewModel = BindingContext as ViewerViewModel;

        if (_boundViewModel is not null)
            _boundViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewerViewModel.IsPtzControlActive)
            && _boundViewModel is { IsPtzControlActive: false } && _isPtzTabSelected)
            SelectTab(isPtz: false);
    }

    private void ApplySheetHeights()
    {
        var hostHeight = Height;
        if (hostHeight <= 0)
            return;

        var expanded = Math.Min(DefaultExpandedHeight, hostHeight * ExpandedHeightRatio);
        var half = Math.Min(DefaultHalfHeight, expanded);
        if (expanded == _expandedHeight && half == _halfHeight)
            return;

        _expandedHeight = expanded;
        _halfHeight = half;
        SheetContainer.HeightRequest = _expandedHeight;
        SheetContainer.TranslationY = _isExpanded ? 0 : _expandedHeight - _halfHeight;
    }

    private void SelectTab(bool isPtz)
    {
        _isPtzTabSelected = isPtz;
        PlaybackTabContent.IsVisible = !isPtz;
        PtzTabContent.IsVisible = isPtz;
        PlaybackTabIndicator.IsVisible = !isPtz;
        PtzTabIndicator.IsVisible = isPtz;
        SemanticProperties.SetDescription(PlaybackTabButton, isPtz ? "Playback tab" : "Playback tab, selected");
        SemanticProperties.SetDescription(PtzTabButton, isPtz ? "PTZ tab, selected" : "PTZ tab");
    }

    private async Task ToggleStateAsync()
    {
        _isExpanded = !_isExpanded;
        var targetY = _isExpanded ? 0 : _expandedHeight - _halfHeight;
        await SheetContainer.TranslateToAsync(0, targetY, AnimationDurationMs, Easing.CubicOut);
    }

    private void OnHandleTapped(object? sender, TappedEventArgs e) => _ = ToggleStateAsync();

    private void OnSheetPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panStartTranslationY = SheetContainer.TranslationY;
                break;
            case GestureStatus.Running:
                var collapsedY = _expandedHeight - _halfHeight;
                var proposed = _panStartTranslationY + e.TotalY;
                SheetContainer.TranslationY = Math.Clamp(proposed, 0, collapsedY);
                break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                var collapsed = _expandedHeight - _halfHeight;
                _isExpanded = SheetContainer.TranslationY < collapsed / 2;
                _ = SheetContainer.TranslateToAsync(0, _isExpanded ? 0 : collapsed, AnimationDurationMs, Easing.CubicOut);
                break;
        }
    }
}
