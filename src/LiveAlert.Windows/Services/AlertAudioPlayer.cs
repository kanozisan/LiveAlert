using System.IO;
using LiveAlert.Core;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace LiveAlert.Windows.Services;

public sealed class AlertAudioPlayer : IDisposable
{
    private readonly LoopingTrack _voiceTrack = new();
    private readonly LoopingTrack _bgmTrack = new();

    public void Start(AlertConfig alert, AlertOptions options)
    {
        Stop();

        var voiceSource = ResolveSource(alert.Voice, AppAssets.DefaultVoiceUri);
        var bgmSource = ResolveSource(alert.Bgm, AppAssets.DefaultBgmUri);

        _voiceTrack.Start(voiceSource, alert.VoiceVolume, options.LoopIntervalSec);
        _bgmTrack.Start(bgmSource, alert.BgmVolume, options.LoopIntervalSec);
    }

    public void Stop()
    {
        _voiceTrack.Stop();
        _bgmTrack.Stop();
    }

    public void Dispose()
    {
        Stop();
    }

    private static Uri ResolveSource(string? configuredPath, Uri fallbackUri)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return new Uri(configuredPath, UriKind.Absolute);
        }

        return fallbackUri;
    }

    private sealed class LoopingTrack
    {
        private IWavePlayer? _player;
        private IDisposable? _reader;
        private IDisposable? _ownedStream;
        private CancellationTokenSource? _restartCts;
        private Uri? _source;
        private float _volume;
        private int _loopIntervalSec;
        private bool _stopRequested;

        public void Start(Uri? sourceUri, double volume, int loopIntervalSec)
        {
            if (sourceUri is null)
            {
                return;
            }

            Stop();

            _source = sourceUri;
            _volume = (float)Math.Clamp(volume / 100d, 0d, 1d);
            _loopIntervalSec = Math.Max(0, loopIntervalSec);
            _stopRequested = false;
            StartPlayback();
        }

        public void Stop()
        {
            _stopRequested = true;
            _restartCts?.Cancel();
            _restartCts?.Dispose();
            _restartCts = null;
            DisposePlayback();
        }

        private void StartPlayback()
        {
            if (_source is null)
            {
                return;
            }

            try
            {
                DisposePlayback();

                var playbackResource = CreatePlaybackResource(_source, _volume);
                _reader = playbackResource.Reader;
                _ownedStream = playbackResource.OwnedStream;
                _player = new WaveOutEvent();
                _player.PlaybackStopped += HandlePlaybackStopped;
                _player.Init(playbackResource.Provider);
                _player.Play();
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Audio playback failed: {ex.Message}");
                Stop();
            }
        }

        private void HandlePlaybackStopped(object? sender, StoppedEventArgs e)
        {
            var failed = e.Exception;
            DisposePlayback();

            if (_stopRequested)
            {
                return;
            }

            if (failed is not null)
            {
                AppLog.Warn($"Audio playback failed: {failed.Message}");
                Stop();
                return;
            }

            if (_loopIntervalSec == 0)
            {
                StartPlayback();
                return;
            }

            _restartCts?.Cancel();
            _restartCts?.Dispose();
            _restartCts = new CancellationTokenSource();
            var token = _restartCts.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_loopIntervalSec), token).ConfigureAwait(false);
                    if (!token.IsCancellationRequested && !_stopRequested)
                    {
                        StartPlayback();
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }, token);
        }

        private void DisposePlayback()
        {
            if (_player is not null)
            {
                _player.PlaybackStopped -= HandlePlaybackStopped;
                _player.Stop();
                _player.Dispose();
                _player = null;
            }

            _reader?.Dispose();
            _reader = null;

            _ownedStream?.Dispose();
            _ownedStream = null;
        }

        private static PlaybackResource CreatePlaybackResource(Uri sourceUri, float volume)
        {
            if (sourceUri.IsFile)
            {
                var reader = new AudioFileReader(sourceUri.LocalPath);
                var volumeProvider = new VolumeSampleProvider(reader) { Volume = volume };
                return new PlaybackResource(reader, new SampleToWaveProvider(volumeProvider), null);
            }

            var stream = AppAssets.OpenResourceStream(sourceUri);
            try
            {
                var extension = Path.GetExtension(sourceUri.AbsolutePath).ToLowerInvariant();
                WaveStream reader = extension switch
                {
                    ".wav" => new WaveFileReader(stream),
                    ".mp3" => new StreamMediaFoundationReader(stream),
                    _ => throw new NotSupportedException($"Unsupported embedded audio format: {extension}")
                };

                var volumeProvider = new VolumeSampleProvider(reader.ToSampleProvider()) { Volume = volume };
                return new PlaybackResource(reader, new SampleToWaveProvider(volumeProvider), stream);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        private sealed record PlaybackResource(IDisposable Reader, IWaveProvider Provider, IDisposable? OwnedStream);
    }
}
