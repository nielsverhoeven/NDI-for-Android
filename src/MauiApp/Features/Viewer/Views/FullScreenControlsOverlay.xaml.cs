namespace NdiForAndroid.Features.Viewer.Views;

/// <summary>
/// Wireframe-A full-screen overlay: preset chips, d-pad, zoom rocker, and a slim bottom toolbar.
/// Reuses the existing auto-hide (<c>AreControlsVisible</c>) and tally/gesture wiring on the
/// donor <c>ViewerViewModel</c> — no view-specific state.
/// </summary>
public partial class FullScreenControlsOverlay : ContentView
{
    public FullScreenControlsOverlay()
    {
        InitializeComponent();
    }
}
