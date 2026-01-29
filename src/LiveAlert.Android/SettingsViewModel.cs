using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using LiveAlert.Core;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

namespace LiveAlert;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private const string ModeAlarmDisplay = "アラーム";
    private const string ModeMannerDisplay = "マナー";
    private const string ModeOffDisplay = "OFF";
    private const string PositionTopDisplay = "画面上部";
    private const string PositionCenterDisplay = "画面中央";
    private const string PositionBottomDisplay = "画面下部";

    private readonly ConfigManager _configManager;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private bool _suppressAutoSave;
    private bool _bandHeightDragging;
    private int _expandedAlertIndex = -1;

    public event PropertyChangedEventHandler? PropertyChanged;

    public SettingsViewModel(ConfigManager configManager)
    {
        _configManager = configManager;
        ConfigPath = _configManager.ConfigPath;
        Alerts.CollectionChanged += OnAlertsChanged;
    }

    private bool _initialized;

    public string ConfigPath { get; }

    public ObservableCollection<AlertEditor> Alerts { get; } = new();

    private string _logPath = string.Empty;
    public string LogPath
    {
        get => _logPath;
        private set => SetField(ref _logPath, value, autoSave: false);
    }

    private bool _serviceRunning;
    public bool ServiceRunning
    {
        get => _serviceRunning;
        set => SetField(ref _serviceRunning, value, autoSave: false);
    }

    private bool _warningActive;
    public bool WarningActive
    {
        get => _warningActive;
        set
        {
            if (SetField(ref _warningActive, value, autoSave: false))
            {
                OnPropertyChanged(nameof(ShowWarning));
            }
        }
    }

    private string _warningMessage = string.Empty;
    public string WarningMessage
    {
        get => _warningMessage;
        set
        {
            if (SetField(ref _warningMessage, value, autoSave: false))
            {
                OnPropertyChanged(nameof(ShowWarning));
            }
        }
    }

    private bool _permissionWarningVisible;
    public bool PermissionWarningVisible
    {
        get => _permissionWarningVisible;
        private set => SetField(ref _permissionWarningVisible, value, autoSave: false);
    }

    public string PermissionWarningMessage => "まず最初に権限設定ボタンで権限を設定してください";

    private string _notificationMode = ModeAlarmDisplay;
    public string NotificationMode
    {
        get => _notificationMode;
        set => SetField(ref _notificationMode, value, autoSave: true);
    }

    private string _displayMode = ModeAlarmDisplay;
    public string DisplayMode
    {
        get => _displayMode;
        set => SetField(ref _displayMode, value, autoSave: true);
    }

    private string _audioMode = ModeAlarmDisplay;
    public string AudioMode
    {
        get => _audioMode;
        set => SetField(ref _audioMode, value, autoSave: true);
    }

    private int _pollIntervalSec = 60;
    public int PollIntervalSec
    {
        get => _pollIntervalSec;
        set => SetField(ref _pollIntervalSec, value, autoSave: true);
    }

    private int _dedupeMinutes = 5;
    public int DedupeMinutes
    {
        get => _dedupeMinutes;
        set => SetField(ref _dedupeMinutes, value, autoSave: true);
    }

    private int _maxAlarmDurationSec = 30;
    public int MaxAlarmDurationSec
    {
        get => _maxAlarmDurationSec;
        set => SetField(ref _maxAlarmDurationSec, value, autoSave: true);
    }

    private int _loopIntervalSec = 5;
    public int LoopIntervalSec
    {
        get => _loopIntervalSec;
        set => SetField(ref _loopIntervalSec, value, autoSave: true);
    }

    private int _bandHeightPx = 340;
    public int BandHeightPx
    {
        get => _bandHeightPx;
        set => SetField(ref _bandHeightPx, value, autoSave: true);
    }

    private int _bandHeightMaxPx = 1000;
    public int BandHeightMaxPx
    {
        get => _bandHeightMaxPx;
        private set => SetField(ref _bandHeightMaxPx, value, autoSave: false);
    }

    public int BandHeightMinPx => AlertOverlay.MinBandHeightPx;

    private string _bandPosition = PositionTopDisplay;
    public string BandPosition
    {
        get => _bandPosition;
        set => SetField(ref _bandPosition, value, autoSave: true);
    }

    private bool _hotReload = true;
    public bool HotReload
    {
        get => _hotReload;
        set
        {
            if (SetField(ref _hotReload, value, autoSave: false))
            {
                OnPropertyChanged(nameof(ShowReload));
            }
        }
    }

    public bool ShowReload => HotReload;
    public bool ShowWarning => WarningActive && !string.IsNullOrWhiteSpace(WarningMessage);
    public int ExpandedAlertIndex => _expandedAlertIndex;

    private bool _debugMode;
    public bool DebugMode
    {
        get => _debugMode;
        private set => SetField(ref _debugMode, value, autoSave: false);
    }

    public void UpdatePermissionWarning(bool visible)
    {
        PermissionWarningVisible = visible;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        LogPath = AppLog.LogFilePath ?? string.Empty;
        AppLog.Info("SettingsViewModel.Initialize");
        await _configManager.LoadAsync();
        _suppressAutoSave = true;
        try
        {
            BandHeightMaxPx = GetMaxBandHeightPx();
            ApplyConfig(_configManager.Current);
        }
        finally
        {
            _suppressAutoSave = false;
        }

        ServiceRunning = ServiceController.IsRunning();
        ApplyWarning(ServiceController.WarningActive, ServiceController.WarningMessage);
        ServiceController.WarningChanged += OnWarningChanged;
        ServiceController.DebugModeChanged += OnDebugModeChanged;
    }

    public async Task SaveAsync()
    {
        await _saveGate.WaitAsync();
        var previousSuppress = _suppressAutoSave;
        _suppressAutoSave = true;
        try
        {
            var config = _configManager.Current;
            config.Options.NotificationMode = ToCanonicalMode(NotificationMode);
            config.Options.DisplayMode = ToCanonicalMode(DisplayMode);
            config.Options.AudioMode = ToCanonicalMode(AudioMode);
            config.Options.PollIntervalSec = Math.Clamp(PollIntervalSec, 60, 600);
            config.DedupeMinutes = Math.Clamp(DedupeMinutes, 1, 30);
            config.Options.MaxAlarmDurationSec = Math.Clamp(MaxAlarmDurationSec, 15, 600);
            config.Options.LoopIntervalSec = Math.Clamp(LoopIntervalSec, 0, 60);
            var maxBandHeightPx = GetMaxBandHeightPx();
            var clampedBandHeightPx = Math.Clamp(BandHeightPx, AlertOverlay.MinBandHeightPx, maxBandHeightPx);
            if (BandHeightPx != clampedBandHeightPx)
            {
                BandHeightPx = clampedBandHeightPx;
            }
            config.Options.BandHeightPx = clampedBandHeightPx;
            config.Options.BandPosition = ToCanonicalPosition(BandPosition);
            HotReload = true;
            config.Options.HotReload = true;
            config.Options.ExpandedAlertIndex = _expandedAlertIndex;
            config.Options.DebugMode = DebugMode;
            config.Alerts = Alerts.Select(alert => alert.ToConfig()).ToList();

            await _configManager.SaveAsync(config);
        }
        finally
        {
            _suppressAutoSave = previousSuppress;
            _saveGate.Release();
        }
    }

    public async Task ReloadAsync()
    {
        AppLog.Info("SettingsViewModel.Reload");
        await _configManager.LoadAsync();
        _suppressAutoSave = true;
        try
        {
            ApplyConfig(_configManager.Current);
        }
        finally
        {
            _suppressAutoSave = false;
        }
    }

    public void StartService()
    {
        AppLog.Info("SettingsViewModel.StartService");
        ServiceController.Start();
        ServiceRunning = true;
    }

    public void StopService()
    {
        AppLog.Info("SettingsViewModel.StopService");
        ServiceController.Stop();
        ServiceRunning = false;
    }

    private void ApplyConfig(ConfigRoot config)
    {
        foreach (var alert in Alerts)
        {
            alert.PropertyChanged -= OnAlertEditorChanged;
        }

        Alerts.Clear();
        foreach (var alert in config.Alerts)
        {
            Alerts.Add(AlertEditor.FromConfig(alert));
        }

        config.Options.NotificationMode = ToCanonicalMode(config.Options.NotificationMode);
        config.Options.DisplayMode = ToCanonicalMode(config.Options.DisplayMode);
        config.Options.AudioMode = ToCanonicalMode(config.Options.AudioMode);
        NotificationMode = ToDisplayMode(config.Options.NotificationMode);
        DisplayMode = ToDisplayMode(config.Options.DisplayMode);
        AudioMode = ToDisplayMode(config.Options.AudioMode);
        config.Options.PollIntervalSec = Math.Clamp(config.Options.PollIntervalSec, 60, 600);
        config.DedupeMinutes = Math.Clamp(config.DedupeMinutes <= 0 ? 5 : config.DedupeMinutes, 1, 30);
        config.Options.MaxAlarmDurationSec = Math.Clamp(config.Options.MaxAlarmDurationSec, 15, 600);
        config.Options.LoopIntervalSec = Math.Clamp(config.Options.LoopIntervalSec, 0, 60);
        PollIntervalSec = config.Options.PollIntervalSec;
        DedupeMinutes = config.DedupeMinutes;
        MaxAlarmDurationSec = config.Options.MaxAlarmDurationSec;
        LoopIntervalSec = config.Options.LoopIntervalSec;
        var maxBandHeightPx = GetMaxBandHeightPx();
        var clampedBandHeightPx = Math.Clamp(config.Options.BandHeightPx, AlertOverlay.MinBandHeightPx, maxBandHeightPx);
        config.Options.BandHeightPx = clampedBandHeightPx;
        BandHeightPx = clampedBandHeightPx;
        config.Options.BandPosition = ToCanonicalPosition(config.Options.BandPosition);
        BandPosition = ToDisplayPosition(config.Options.BandPosition);
        HotReload = true;
        config.Options.HotReload = true;
        ApplyExpandedIndex(config.Options.ExpandedAlertIndex, suppressAutoSave: true);
        DebugMode = config.Options.DebugMode;
    }

    public void BeginBandHeightDrag()
    {
        _bandHeightDragging = true;
    }

    public void EndBandHeightDrag()
    {
        _bandHeightDragging = false;
        RequestAutoSave();
    }

    public void AddAlert()
    {
        AppLog.Info("SettingsViewModel.AddAlert");
        Alerts.Add(AlertEditor.CreateDefault(Alerts.Count + 1));
    }

    public void RemoveAlert(AlertEditor alert)
    {
        var index = Alerts.IndexOf(alert);
        AppLog.Info($"SettingsViewModel.RemoveAlert index={index}");
        Alerts.Remove(alert);
        if (index < 0) return;
        if (_expandedAlertIndex == index)
        {
            ApplyExpandedIndex(-1, suppressAutoSave: true);
        }
        else if (_expandedAlertIndex > index)
        {
            ApplyExpandedIndex(_expandedAlertIndex - 1, suppressAutoSave: true);
        }
        RequestAutoSave();
    }

    public void ToggleExpanded(AlertEditor alert)
    {
        var index = Alerts.IndexOf(alert);
        if (index < 0) return;
        var next = index == _expandedAlertIndex ? -1 : index;
        ApplyExpandedIndex(next, suppressAutoSave: true);
        RequestAutoSave();
    }

    private void OnWarningChanged(bool active, string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ApplyWarning(active, message);
        });
    }

    private void OnDebugModeChanged(bool enabled)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DebugMode = enabled;
        });
    }


    private void ApplyWarning(bool active, string message)
    {
        WarningActive = active;
        WarningMessage = message;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private bool SetField<T>(ref T field, T value, bool autoSave, [CallerMemberName] string? name = null)
    {
        if (!SetField(ref field, value, name)) return false;
        if (autoSave)
        {
            RequestAutoSave();
        }
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void OnAlertsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (AlertEditor alert in e.OldItems)
            {
                alert.PropertyChanged -= OnAlertEditorChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (AlertEditor alert in e.NewItems)
            {
                alert.PropertyChanged += OnAlertEditorChanged;
            }
        }

        NormalizeExpandedIndex();
        RequestAutoSave();
    }

    private void OnAlertEditorChanged(object? sender, PropertyChangedEventArgs e)
    {
        RequestAutoSave();
    }

    private void RequestAutoSave()
    {
        if (!_initialized || _suppressAutoSave || _bandHeightDragging) return;
        _ = SaveAsync();
    }

    private void ApplyExpandedIndex(int index, bool suppressAutoSave)
    {
        var previousSuppress = _suppressAutoSave;
        if (suppressAutoSave)
        {
            _suppressAutoSave = true;
        }

        try
        {
            _expandedAlertIndex = index;
            for (var i = 0; i < Alerts.Count; i++)
            {
                Alerts[i].IsExpanded = i == _expandedAlertIndex;
            }
        }
        finally
        {
            _suppressAutoSave = previousSuppress;
        }

    }

    private void NormalizeExpandedIndex()
    {
        var index = _expandedAlertIndex;
        if (index < 0 || index >= Alerts.Count)
        {
            index = -1;
        }
        ApplyExpandedIndex(index, suppressAutoSave: true);
    }

    private static int GetMaxBandHeightPx()
    {
        var height = (int)Math.Round(DeviceDisplay.MainDisplayInfo.Height);
        if (height <= 0)
        {
            return 1000;
        }

        return Math.Max(height, AlertOverlay.MinBandHeightPx);
    }

    private static string ToCanonicalMode(string? mode)
    {
        return mode switch
        {
            "alarm" => "alarm",
            "manner" => "manner",
            "off" => "off",
            ModeAlarmDisplay => "alarm",
            ModeMannerDisplay => "manner",
            ModeOffDisplay => "off",
            _ => "alarm"
        };
    }

    private static string ToDisplayMode(string? mode)
    {
        return mode switch
        {
            "alarm" => ModeAlarmDisplay,
            "manner" => ModeMannerDisplay,
            "off" => ModeOffDisplay,
            ModeAlarmDisplay => ModeAlarmDisplay,
            ModeMannerDisplay => ModeMannerDisplay,
            ModeOffDisplay => ModeOffDisplay,
            _ => ModeAlarmDisplay
        };
    }

    private static string ToCanonicalPosition(string? position)
    {
        return position switch
        {
            "top" => "top",
            "center" => "center",
            "bottom" => "bottom",
            PositionTopDisplay => "top",
            PositionCenterDisplay => "center",
            PositionBottomDisplay => "bottom",
            _ => "top"
        };
    }

    private static string ToDisplayPosition(string? position)
    {
        return position switch
        {
            "top" => PositionTopDisplay,
            "center" => PositionCenterDisplay,
            "bottom" => PositionBottomDisplay,
            PositionTopDisplay => PositionTopDisplay,
            PositionCenterDisplay => PositionCenterDisplay,
            PositionBottomDisplay => PositionBottomDisplay,
            _ => PositionTopDisplay
        };
    }
}
