using NdiForAndroid.Features.Viewer.Views;
using NdiForAndroid.Features.Output.Views;

namespace NdiForAndroid;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("viewer", typeof(ViewerPage));
        Routing.RegisterRoute("output", typeof(OutputPage));
    }
}
