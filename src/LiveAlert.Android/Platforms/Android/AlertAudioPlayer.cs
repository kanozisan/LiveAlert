using Android.Content;
using Android.Media;
using Android.OS;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LiveAlert;

public sealed class AlertAudioPlayer
{
    public const string AssetPrefix = "asset://";
    public const string DefaultVoiceLiveAsset = "voice_live.wav";
    public const string DefaultVoiceSpaceAsset = "voice_space.wav";
    public const string DefaultVoiceAsset = DefaultVoiceLiveAsset;
    public const string DefaultBgmAsset = "bgm.mp3";

    private readonly Context _context;
    private readonly Handler _handler = new(Looper.MainLooper!);
    private MediaPlayer? _voicePlayer;
    private MediaPlayer? _bgmPlayer;
    private CancellationTokenSource? _durationCts;

    public AlertAudioPlayer(Context context)
    {
        _context = context;
    }

    public void Start(string voicePath, double voiceVolume, string bgmPath, double bgmVolume, int loopIntervalSec, int maxDurationSec, AudioUsageKind usage, bool enabled)
    {
        if (!enabled)
        {
            Stop();
            return;
        }

        var voiceSource = ResolveSource(voicePath, DefaultVoiceAsset);
        var bgmSource = ResolveSource(bgmPath, DefaultBgmAsset);
        AppLog.Info($"Audio.Start voice={(!string.IsNullOrWhiteSpace(voiceSource))} bgm={(!string.IsNullOrWhiteSpace(bgmSource))} loop={loopIntervalSec}s max={maxDurationSec}s voiceVol={voiceVolume} bgmVol={bgmVolume}");
        Stop();

        _durationCts = new CancellationTokenSource();
        if (maxDurationSec > 0)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(maxDurationSec), _durationCts.Token).ConfigureAwait(false);
                    Stop();
                }
                catch (System.OperationCanceledException)
                {
                }
            });
        }

        _voicePlayer = CreatePlayer(voiceSource, voiceVolume, usage);
        _bgmPlayer = CreatePlayer(bgmSource, bgmVolume, usage);

        StartLoop(_voicePlayer, loopIntervalSec, _durationCts.Token);
        StartLoop(_bgmPlayer, loopIntervalSec, _durationCts.Token);
    }

    public void Stop()
    {
        AppLog.Info("Audio.Stop");
        _durationCts?.Cancel();
        _durationCts = null;

        StopPlayer(ref _voicePlayer);
        StopPlayer(ref _bgmPlayer);
    }

    public static string BuildAssetSource(string assetName) => $"{AssetPrefix}{assetName}";

    private static string ResolveSource(string path, string defaultAsset)
    {
        if (!string.IsNullOrWhiteSpace(path)) return path;
        return BuildAssetSource(defaultAsset);
    }

    private MediaPlayer? CreatePlayer(string source, double volume, AudioUsageKind usage)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;

        try
        {
            var player = new MediaPlayer();
            var builder = new AudioAttributes.Builder();
            builder!.SetUsage(usage);
            builder!.SetContentType(AudioContentType.Music);
            var attributes = builder!.Build()!;
            player.SetAudioAttributes(attributes!);

            if (source.StartsWith(AssetPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var assetName = source.Substring(AssetPrefix.Length);
                using var afd = _context.Assets?.OpenFd(assetName);
                if (afd == null)
                {
                    player.Release();
                    return null;
                }
                player.SetDataSource(afd.FileDescriptor, afd.StartOffset, afd.Length);
            }
            else if (source.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
            {
                var resolver = _context.ContentResolver;
                if (resolver == null)
                {
                    player.Release();
                    return null;
                }
                var uri = Android.Net.Uri.Parse(source);
                if (uri == null)
                {
                    player.Release();
                    return null;
                }
                using var pfd = resolver.OpenFileDescriptor(uri, "r");
                if (pfd == null)
                {
                    player.Release();
                    return null;
                }
                player.SetDataSource(pfd.FileDescriptor);
            }
            else
            {
                player.SetDataSource(source);
            }

            var vol = ToVolume(volume);
            player.SetVolume(vol, vol);
            player.Prepare();
            return player;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Audio.CreatePlayer failed source={source}", ex);
            return null;
        }
    }

    private static float ToVolume(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return 1f;
        return (float)Math.Clamp(value / 100.0, 0, 1);
    }

    private void StartLoop(MediaPlayer? player, int loopIntervalSec, CancellationToken token)
    {
        if (player == null) return;
        MediaPlayer localPlayer = player;
        localPlayer.Completion += (_, _) =>
        {
            if (token.IsCancellationRequested) return;
            _handler.PostDelayed(() =>
            {
                if (token.IsCancellationRequested) return;
                try
                {
                    localPlayer.SeekTo(0);
                    localPlayer.Start();
                }
                catch (Exception ex)
                {
                    AppLog.Error("Audio.LoopStart failed", ex);
                }
            }, Math.Max(0, loopIntervalSec) * 1000);
        };

        try
        {
            localPlayer.Start();
        }
        catch (Exception ex)
        {
            AppLog.Error("Audio.Start failed", ex);
        }
    }

    private static void StopPlayer(ref MediaPlayer? player)
    {
        if (player == null) return;
        try
        {
            if (player.IsPlaying)
            {
                player.Stop();
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Audio.StopPlayer failed", ex);
        }
        finally
        {
            try
            {
                player.Release();
            }
            catch (Exception ex)
            {
                AppLog.Error("Audio.Release failed", ex);
            }
            player = null;
        }
    }
}
