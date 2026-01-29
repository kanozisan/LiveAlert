using Android.App;
using Android.OS;
using System.IO;
using System.Text;
using AndroidWidget = global::Android.Widget;

namespace LiveAlert.Android.Tests;

[Activity(Label = "LiveAlert.Android.Tests", MainLauncher = true)]
public class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var summary = new StringBuilder();
        var passed = 0;
        var failed = 0;

        foreach (var test in TestRegistry.All())
        {
            try
            {
                test.Run();
                passed++;
                summary.AppendLine($"[PASS] {test.Name}");
            }
            catch (Exception ex)
            {
                failed++;
                summary.AppendLine($"[FAIL] {test.Name}");
                summary.AppendLine($"       {ex.GetType().Name}: {ex.Message}");
            }
        }

        var text = $"Result: Passed {passed}, Failed {failed}\n\n{summary}";

        var view = new AndroidWidget.ScrollView(this);
        var textView = new AndroidWidget.TextView(this)
        {
            Text = text,
            TextSize = 12
        };
        view.AddView(textView);
        SetContentView(view);
    }

}
