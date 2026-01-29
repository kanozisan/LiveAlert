using Android.App;
using Android.Runtime;

namespace LiveAlert;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    public override void OnCreate()
    {
        base.OnCreate();
        AppLog.Init(this);
        AppLog.Info("Application OnCreate");
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
