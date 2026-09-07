using NdiForAndroid.Features.Home.ViewModels;

namespace NdiForAndroid.Features.Home.Views;

/// <summary>Home tab root. Singleton (matches <see cref="HomeViewModel"/>, #352/#359).</summary>
/// <remarks>
/// Hosted by two <c>ShellContent</c>s (<c>home-tab</c> and <c>home-rail</c>); Shell re-parents this single
/// instance between them. Never dispose the ViewModel from page lifecycle — per-visit work is
/// <see cref="OnAppearing"/> re-running <c>RefreshCommand</c>.
/// </remarks>
public partial class HomePage : ContentPage
{
    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        (BindingContext as HomeViewModel)?.RefreshCommand.Execute(null);
    }
}
