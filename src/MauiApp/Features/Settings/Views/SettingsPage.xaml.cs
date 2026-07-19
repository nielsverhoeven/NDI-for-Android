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
}
