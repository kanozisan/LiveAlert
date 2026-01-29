using Android.App;
using Android.Content;
using Android.Content.PM;
using Microsoft.Maui.ApplicationModel;

namespace LiveAlert;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int SaveLogRequestCode = 4097;
    private static TaskCompletionSource<Android.Net.Uri?>? _saveLogTcs;

    internal static Task<Android.Net.Uri?> CreateDocumentAsync(string fileName, string mimeType)
    {
        var activity = Platform.CurrentActivity;
        if (activity == null)
        {
            return Task.FromResult<Android.Net.Uri?>(null);
        }

        var tcs = new TaskCompletionSource<Android.Net.Uri?>();
        _saveLogTcs = tcs;
        var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType(mimeType);
        intent.PutExtra(Intent.ExtraTitle, fileName);
        activity.StartActivityForResult(intent, SaveLogRequestCode);
        return tcs.Task;
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != SaveLogRequestCode)
        {
            return;
        }

        var tcs = _saveLogTcs;
        _saveLogTcs = null;
        if (tcs == null)
        {
            return;
        }

        if (resultCode == Result.Ok)
        {
            tcs.TrySetResult(data?.Data);
        }
        else
        {
            tcs.TrySetResult(null);
        }
    }
}
