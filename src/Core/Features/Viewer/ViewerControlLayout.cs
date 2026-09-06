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

    public const double MinCameraControlsRowWidthDp = 440;   // 160 d-pad + 8 + 48 zoom + 8 + 216 presets
    public const double ViewerContentPaddingDp = 16;         // mirrors ViewerView.xaml root Grid Padding
    public const double DeckVideoHeightDp = 240;
    public const double MinVideoHeightDp = 96;
    public const double VideoBorderStrokeDp = 6;             // StrokeThickness 3, top + bottom
    public const double SheetChromeDp = 104;                 // 48 handle + 40 tab strip + 16 content padding
    public const double MinSheetContentDp = 208;             // chip 24 + 4 + 160 d-pad row + 4 + 16 status line
    public const double DefaultSheetExpandedDp = 440;
    public const double DefaultSheetPeekDp = 320;
    public const double MinSheetPeekDp = 136;                // chrome 88 + one 48 dp row
    public const double SheetExpandedRatio = 0.8;
    public const double SheetPeekRatio = 0.55;

    public static ViewerControlLayoutKind Choose(double widthDp, double heightDp) =>
        widthDp >= MinDeckWidthDp && heightDp >= MinDeckHeightDp
            ? ViewerControlLayoutKind.Deck
            : ViewerControlLayoutKind.Sheet;

    public static bool ShouldStackCameraPresets(double cameraControlsWidthDp) =>
        cameraControlsWidthDp > 0 && cameraControlsWidthDp < MinCameraControlsRowWidthDp;

    public static double ChooseSheetExpandedHeightDp(double sheetHostHeightDp)
    {
        if (sheetHostHeightDp <= 0) return DefaultSheetExpandedDp;
        var preferred = Math.Min(DefaultSheetExpandedDp, sheetHostHeightDp * SheetExpandedRatio);
        return Math.Min(sheetHostHeightDp, Math.Max(preferred, SheetChromeDp + MinSheetContentDp));
    }

    public static double ChooseSheetPeekHeightDp(double sheetHostHeightDp)
    {
        var expanded = ChooseSheetExpandedHeightDp(sheetHostHeightDp);
        var max = Math.Min(DefaultSheetPeekDp, expanded);
        if (sheetHostHeightDp <= 0) return max;
        var min = Math.Min(MinSheetPeekDp, max);          // keeps Math.Clamp's min <= max invariant
        return Math.Clamp(sheetHostHeightDp * SheetPeekRatio, min, max);
    }

    public static double ChooseVideoHeightDp(double contentHeightDp, ViewerControlLayoutKind layout, bool isFullScreen)
    {
        if (isFullScreen) return -1;                       // fill
        if (layout == ViewerControlLayoutKind.Deck || contentHeightDp <= 0) return DeckVideoHeightDp;
        var available = contentHeightDp - ChooseSheetPeekHeightDp(contentHeightDp) - VideoBorderStrokeDp;
        var floor = Math.Min(MinVideoHeightDp, Math.Max(0, available));
        return Math.Clamp(available, floor, DeckVideoHeightDp);
    }
}
