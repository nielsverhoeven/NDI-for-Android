namespace NdiForAndroid.Features.Viewer;

public enum ViewerControlLayoutKind
{
    Deck,
    Sheet,
}

/// <summary>
/// Pure layout policy for the viewer's playback/camera controls: a fixed-height two-column
/// deck when the host has enough width AND height, otherwise a draggable bottom sheet.
/// </summary>
public static class ViewerControlLayout
{
    public const double MinDeckWidthDp = 640;
    public const double MinDeckHeightDp = 470;

    public static ViewerControlLayoutKind Choose(double widthDp, double heightDp) =>
        widthDp >= MinDeckWidthDp && heightDp >= MinDeckHeightDp
            ? ViewerControlLayoutKind.Deck
            : ViewerControlLayoutKind.Sheet;
}
