using Android.App;
using Android.Runtime;

namespace NdiForAndroid;

[Application(
    Label = "NDI for Android",
    Icon = "@drawable/ndi_logo",
    RoundIcon = "@drawable/ndi_logo")]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
