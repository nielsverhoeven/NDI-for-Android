using NdiForAndroid.Features.Settings.Models;

namespace NdiForAndroid.Features.Settings.Services;

public interface IAppearanceService
{
    void Apply(ThemeMode theme, AccentColorOption accentColor);
}
