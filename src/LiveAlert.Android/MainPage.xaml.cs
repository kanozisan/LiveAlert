using Android;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using Android.Provider;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using LiveAlert.Core;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using MauiButton = Microsoft.Maui.Controls.Button;
using MauiScrollView = Microsoft.Maui.Controls.ScrollView;

namespace LiveAlert;

public partial class MainPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;
    private bool _ignoreServiceToggle;
    private MediaPlayer? _previewPlayer;

    public MainPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        TestAlertButton.IsVisible = true;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _ignoreServiceToggle = true;
        await _viewModel.InitializeAsync();
        RefreshPermissionWarning();
        _ignoreServiceToggle = false;
        ServiceController.WarningChanged += OnWarningChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ServiceController.WarningChanged -= OnWarningChanged;
        StopPreviewPlayer();
    }

    private void RefreshPermissionWarning()
    {
#if ANDROID
        var context = Android.App.Application.Context;
        if (context == null)
        {
            _viewModel.UpdatePermissionWarning(false);
            return;
        }

        var overlayAllowed = Settings.CanDrawOverlays(context);
        var notificationAllowed = NotificationManagerCompat.From(context).AreNotificationsEnabled();
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
#pragma warning disable CA1416
            notificationAllowed &= ContextCompat.CheckSelfPermission(context, Manifest.Permission.PostNotifications) == Permission.Granted;
#pragma warning restore CA1416
        }

        var listenerAllowed = IsNotificationListenerEnabled(context);
        _viewModel.UpdatePermissionWarning(!(overlayAllowed && notificationAllowed && listenerAllowed));
#endif
    }

    private static bool IsNotificationListenerEnabled(Context context)
    {
#if ANDROID
        try
        {
            var enabled = Settings.Secure.GetString(context.ContentResolver, "enabled_notification_listeners");
            if (string.IsNullOrWhiteSpace(enabled))
            {
                return false;
            }

            var packageName = context.PackageName ?? string.Empty;
            return enabled.Split(':').Any(entry => entry.Contains(packageName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
#else
        return false;
#endif
    }

    private void OnServiceToggled(object sender, ToggledEventArgs e)
    {
        if (_ignoreServiceToggle)
        {
            return;
        }

        if (e.Value)
        {
            _viewModel.StartService();
        }
        else
        {
            _viewModel.StopService();
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        await _viewModel.SaveAsync();
        await DisplayAlert("保存", "設定を保存しました", "OK");
    }

    private async void OnReloadClicked(object sender, EventArgs e)
    {
        await _viewModel.ReloadAsync();
        await DisplayAlert("再読み込み", "config.json を再読み込みしました", "OK");
    }

    private void OnTestAlertClicked(object sender, EventArgs e)
    {
        ServiceController.TriggerTestAlert();
    }

    private void OnBandHeightDragStarted(object sender, EventArgs e)
    {
        _viewModel.BeginBandHeightDrag();
    }

    private void OnBandHeightDragCompleted(object sender, EventArgs e)
    {
        _viewModel.EndBandHeightDrag();
    }

    private async void OnPickVoiceClicked(object sender, EventArgs e)
    {
        if (sender is not MauiButton button || button.CommandParameter is not AlertEditor alert)
        {
            return;
        }

        var path = await PickAudioAsync("音声ファイルを選択").ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(path))
        {
            alert.Voice = path;
        }
    }

    private async void OnVoicePathTapped(object sender, EventArgs e)
    {
        var alert = (sender as BindableObject)?.BindingContext as AlertEditor;
        if (alert == null && sender is TapGestureRecognizer tap)
        {
            alert = tap.CommandParameter as AlertEditor;
        }
        if (alert == null) return;

        var action = await DisplayActionSheet("音声ファイル", "キャンセル", null, "デフォルト");
        if (action == "デフォルト")
        {
            alert.Voice = string.Empty;
        }
    }

    private async void OnPickBgmClicked(object sender, EventArgs e)
    {
        if (sender is not MauiButton button || button.CommandParameter is not AlertEditor alert)
        {
            return;
        }

        var path = await PickAudioAsync("BGMファイルを選択").ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(path))
        {
            alert.Bgm = path;
        }
    }

    private async void OnBgmPathTapped(object sender, EventArgs e)
    {
        var alert = (sender as BindableObject)?.BindingContext as AlertEditor;
        if (alert == null && sender is TapGestureRecognizer tap)
        {
            alert = tap.CommandParameter as AlertEditor;
        }
        if (alert == null) return;

        var action = await DisplayActionSheet("BGMファイル", "キャンセル", null, "デフォルト");
        if (action == "デフォルト")
        {
            alert.Bgm = string.Empty;
        }
    }

    private void OnAlertHeaderTapped(object sender, EventArgs e)
    {
        var alert = (sender as BindableObject)?.BindingContext as AlertEditor;
        if (alert == null && sender is TapGestureRecognizer tap)
        {
            alert = tap.CommandParameter as AlertEditor;
        }

        if (alert == null) return;
        _viewModel.ToggleExpanded(alert);
    }

    private void OnTestVoiceClicked(object sender, EventArgs e)
    {
        if (sender is not MauiButton button || button.CommandParameter is not AlertEditor alert)
        {
            return;
        }

        var source = string.IsNullOrWhiteSpace(alert.Voice)
            ? AlertAudioPlayer.BuildAssetSource(GetDefaultVoiceAsset(alert))
            : alert.Voice;
        PlayPreview(source, alert.VoiceVolume);
    }

    private void OnTestBgmClicked(object sender, EventArgs e)
    {
        if (sender is not MauiButton button || button.CommandParameter is not AlertEditor alert)
        {
            return;
        }

        var source = string.IsNullOrWhiteSpace(alert.Bgm)
            ? AlertAudioPlayer.BuildAssetSource(AlertAudioPlayer.DefaultBgmAsset)
            : alert.Bgm;
        PlayPreview(source, alert.BgmVolume);
    }

    private async void OnAboutProgramClicked(object sender, EventArgs e)
    {
        var content = await LoadReadmeAndroidAsync();
        if (string.IsNullOrWhiteSpace(content))
        {
            await DisplayAlert("このプログラムについて", "readme_android.txt が見つかりませんでした。", "OK");
            return;
        }

        var label = new Label
        {
            Text = content,
            FontSize = 14,
            LineBreakMode = LineBreakMode.WordWrap
        };

        var page = new ContentPage
        {
            Title = "このプログラムについて",
            Content = new MauiScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(16),
                    Children = { label }
                }
            }
        };

        await Navigation.PushModalAsync(new NavigationPage(page));
    }

    private async void OnFontLicenseClicked(object sender, EventArgs e)
    {
        var content = await LoadFontReadmeAsync();
        if (string.IsNullOrWhiteSpace(content))
        {
            await DisplayAlert("フォントのライセンス", "assets_readme.txt が見つかりませんでした。", "OK");
            return;
        }

        var label = new Label
        {
            Text = content,
            FontSize = 14,
            LineBreakMode = LineBreakMode.WordWrap
        };

        var page = new ContentPage
        {
            Title = "フォントのライセンス",
            Content = new MauiScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(16),
                    Children = { label }
                }
            }
        };

        await Navigation.PushModalAsync(new NavigationPage(page));
    }

    private static string GetDefaultVoiceAsset(AlertEditor alert)
    {
        if (string.Equals(alert.Service?.Trim(), "x_space", StringComparison.OrdinalIgnoreCase))
        {
            return AlertAudioPlayer.DefaultVoiceSpaceAsset;
        }

        return AlertAudioPlayer.DefaultVoiceLiveAsset;
    }

    private async void OnShowLogClicked(object sender, EventArgs e)
    {
        var path = AppLog.LogFilePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            await DisplayAlert("ログ出力", "ログファイルが見つかりませんでした。", "OK");
            return;
        }

        var uri = await PickSaveUriAsync("livealert.log").ConfigureAwait(true);
        if (uri == null)
        {
            return;
        }

        try
        {
#if ANDROID
            var resolver = Android.App.Application.Context.ContentResolver;
            if (resolver == null)
            {
                await DisplayAlert("ログ出力", "保存先にアクセスできませんでした。", "OK");
                return;
            }

            using var output = resolver.OpenOutputStream(uri);
            if (output == null)
            {
                await DisplayAlert("ログ出力", "保存先に書き込めませんでした。", "OK");
                return;
            }
            var oldPath = path + ".old";
            if (File.Exists(oldPath))
            {
                using var oldInput = File.OpenRead(oldPath);
                await oldInput.CopyToAsync(output).ConfigureAwait(true);
                var separator = System.Text.Encoding.UTF8.GetBytes(System.Environment.NewLine);
                await output.WriteAsync(separator, 0, separator.Length).ConfigureAwait(true);
            }

            if (File.Exists(path))
            {
                using var input = File.OpenRead(path);
                await input.CopyToAsync(output).ConfigureAwait(true);
            }
#endif
        }
        catch
        {
            await DisplayAlert("ログ出力", "ログファイルの保存に失敗しました。", "OK");
            return;
        }

        await DisplayAlert("ログ出力", "ログファイルを保存しました。", "OK");
    }

    private static async Task<Android.Net.Uri?> PickSaveUriAsync(string defaultFileName)
    {
#if ANDROID
        try
        {
            return await MainActivity.CreateDocumentAsync(defaultFileName, "text/plain").ConfigureAwait(true);
        }
        catch
        {
            return null;
        }
#else
        await Task.CompletedTask.ConfigureAwait(false);
        return null;
#endif
    }

    private async void OnPermissionSettingsClicked(object sender, EventArgs e)
    {
        var content = await LoadPermissionTextAsync();
        if (string.IsNullOrWhiteSpace(content))
        {
            await DisplayAlert("権限設定", "permission.txt が見つかりませんでした。", "OK");
            return;
        }

        var description = new Label
        {
            Text = content,
            FontSize = 14,
            LineBreakMode = LineBreakMode.WordWrap
        };

        var appInfoButton = new MauiButton { Text = "アプリ情報" };
        appInfoButton.Clicked += (_, _) => OpenAppInfoSettings();

        var notificationsButton = new MauiButton { Text = "通知" };
        notificationsButton.Clicked += (_, _) => OpenNotificationSettings();

        var overlayButton = new MauiButton { Text = "他のアプリの上に表示" };
        overlayButton.Clicked += (_, _) => OpenOverlaySettings();

        var listenerButton = new MauiButton { Text = "通知アクセス" };
        listenerButton.Clicked += (_, _) => OpenNotificationListenerSettings();

        var page = new ContentPage
        {
            Title = "権限設定",
            Content = new MauiScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(16),
                    Spacing = 12,
                    Children =
                    {
                        description,
                        new BoxView { HeightRequest = 1, Color = Colors.Gray },
                        appInfoButton,
                        notificationsButton,
                        listenerButton,
                        overlayButton
                    }
                }
            }
        };

        await Navigation.PushModalAsync(new NavigationPage(page));
    }

    private static Task<string> LoadFontReadmeAsync()
    {
#if ANDROID
        return Task.Run(() =>
        {
            try
            {
                var context = Android.App.Application.Context;
                if (context == null)
                {
                    return string.Empty;
                }
                var assets = context.Assets;
                if (assets == null)
                {
                    return string.Empty;
                }
                using var stream = assets.Open("assets_readme.txt");
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch
            {
                return string.Empty;
            }
        });
#else
        return Task.FromResult(string.Empty);
#endif
    }

    private static Task<string> LoadReadmeAndroidAsync()
    {
#if ANDROID
        return Task.Run(() =>
        {
            try
            {
                var context = Android.App.Application.Context;
                if (context == null)
                {
                    return string.Empty;
                }
                var assets = context.Assets;
                if (assets == null)
                {
                    return string.Empty;
                }
                using var stream = assets.Open("readme_android.txt");
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch
            {
                return string.Empty;
            }
        });
#else
        return Task.FromResult(string.Empty);
#endif
    }

    private static Task<string> LoadPermissionTextAsync()
    {
#if ANDROID
        return Task.Run(() =>
        {
            try
            {
                var context = Android.App.Application.Context;
                if (context == null)
                {
                    return string.Empty;
                }
                var assets = context.Assets;
                if (assets == null)
                {
                    return string.Empty;
                }
                using var stream = assets.Open("permission.txt");
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch
            {
                return string.Empty;
            }
        });
#else
        return Task.FromResult(string.Empty);
#endif
    }

    private void OnAddAlertClicked(object sender, EventArgs e)
    {
        _viewModel.AddAlert();
    }

    private void OnRemoveAlertClicked(object sender, EventArgs e)
    {
        if (sender is MauiButton button && button.CommandParameter is AlertEditor alert)
        {
            _viewModel.RemoveAlert(alert);
        }
    }

    private async void OnWarningChanged(bool active, string message)
    {
        if (!active) return;
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await DisplayAlert("警告", message, "OK");
        });
    }

    private static readonly FilePickerFileType AudioFileTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.Android, new[] { "audio/*" } },
        { DevicePlatform.WinUI, new[] { ".mp3", ".wav", ".m4a", ".aac", ".ogg", ".flac" } },
        { DevicePlatform.iOS, new[] { "public.audio" } },
        { DevicePlatform.MacCatalyst, new[] { "public.audio" } }
    });

    private static async Task<string?> PickAudioAsync(string title)
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = title,
                FileTypes = AudioFileTypes
            };
            var result = await FilePicker.PickAsync(options).ConfigureAwait(true);
            return result?.FullPath;
        }
        catch
        {
            return null;
        }
    }

    private static void OpenAppInfoSettings()
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            if (context == null) return;
            var intent = new Intent(Settings.ActionApplicationDetailsSettings);
            var uri = Android.Net.Uri.FromParts("package", context.PackageName, null);
            intent.SetData(uri);
            intent.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(intent);
        }
        catch
        {
        }
#endif
    }

    private static void OpenNotificationSettings()
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            if (context == null)
            {
                return;
            }
            var intent = new Intent(Settings.ActionAppNotificationSettings);
            intent.PutExtra("android.provider.extra.APP_PACKAGE", context.PackageName);
            intent.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(intent);
        }
        catch
        {
            OpenAppInfoSettings();
        }
#endif
    }

    private static void OpenOverlaySettings()
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            if (context == null)
            {
                return;
            }
            var intent = new Intent(Settings.ActionManageOverlayPermission);
            var uri = Android.Net.Uri.Parse($"package:{context.PackageName}");
            intent.SetData(uri);
            intent.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(intent);
        }
        catch
        {
            OpenAppInfoSettings();
        }
#endif
    }

    private static void OpenNotificationListenerSettings()
    {
#if ANDROID
        try
        {
            var intent = new Intent(Settings.ActionNotificationListenerSettings);
            intent.AddFlags(ActivityFlags.NewTask);
            Android.App.Application.Context?.StartActivity(intent);
        }
        catch
        {
        }
#endif
    }

    private void PlayPreview(string source, double volume)
    {
        if (_previewPlayer != null && _previewPlayer.IsPlaying)
        {
            StopPreviewPlayer();
            return;
        }

        StopPreviewPlayer();

        var player = new MediaPlayer();
        try
        {
            var builder = new AudioAttributes.Builder();
            builder!.SetUsage(AudioUsageKind.Alarm);
            builder!.SetContentType(AudioContentType.Music);
            player.SetAudioAttributes(builder!.Build()!);

            if (source.StartsWith(AlertAudioPlayer.AssetPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var assetName = source.Substring(AlertAudioPlayer.AssetPrefix.Length);
                using var afd = Android.App.Application.Context.Assets?.OpenFd(assetName);
                if (afd == null)
                {
                    player.Release();
                    return;
                }
                player.SetDataSource(afd.FileDescriptor, afd.StartOffset, afd.Length);
            }
            else if (source.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
            {
                var resolver = Android.App.Application.Context.ContentResolver;
                if (resolver == null)
                {
                    player.Release();
                    return;
                }

                var uri = Android.Net.Uri.Parse(source);
                if (uri == null)
                {
                    player.Release();
                    return;
                }

                using var pfd = resolver.OpenFileDescriptor(uri, "r");
                if (pfd == null)
                {
                    player.Release();
                    return;
                }

                player.SetDataSource(pfd.FileDescriptor);
            }
            else
            {
            player.SetDataSource(source);
            }

            var vol = (float)Math.Clamp(volume / 100.0, 0, 1);
            player.SetVolume(vol, vol);
            player.Completion += (_, _) => StopPreviewPlayer();
            player.Prepare();
            player.Start();
            _previewPlayer = player;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Audio.Preview failed source={source}", ex);
            try
            {
                player.Release();
            }
            catch
            {
            }
        }
    }

    private void StopPreviewPlayer()
    {
        if (_previewPlayer == null) return;
        try
        {
            if (_previewPlayer.IsPlaying)
            {
                _previewPlayer.Stop();
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Audio.PreviewStop failed", ex);
        }
        finally
        {
            try
            {
                _previewPlayer.Release();
            }
            catch
            {
            }
            _previewPlayer = null;
        }
    }
}
