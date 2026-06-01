using NdiForAndroid.Features.Sources.ViewModels;

namespace NdiForAndroid.Features.Sources.Views;

public partial class SourceListPage : ContentPage
{
    public SourceListPage(SourceListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is SourceListViewModel vm && vm.RefreshCommand.CanExecute(null))
            vm.RefreshCommand.Execute(null);
    }
}
