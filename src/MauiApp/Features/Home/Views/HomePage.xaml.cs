using NdiForAndroid.Features.Home.ViewModels;

namespace NdiForAndroid.Features.Home.Views;

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
