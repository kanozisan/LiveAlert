using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LiveAlert.Core;
using Microsoft.Maui.Graphics;

namespace LiveAlert;

public sealed class AlertEditor : INotifyPropertyChanged
{
    private string _service = "youtube";
    private string _url = string.Empty;
    private string _titleContains = string.Empty;
    private string _label = string.Empty;
    private string _voice = string.Empty;
    private double _voiceVolume = 100;
    private string _bgm = string.Empty;
    private double _bgmVolume = 50;
    private const string DefaultMessageYoutube = "警告　{label} がライブ開始";
    private const string DefaultMessageSpace = "警告　{label}がスペース開始";
    private string _message = DefaultMessageYoutube;
    private string _backgroundColor = "#FF0000";
    private string _textColor = "#000000";
    private bool _isExpanded;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Service
    {
        get => _service;
        set
        {
            if (SetField(ref _service, value))
            {
                var normalized = NormalizeService(value);
                if (normalized == "x_space")
                {
                    if (string.IsNullOrWhiteSpace(Message) || Message == DefaultMessageYoutube)
                    {
                        Message = DefaultMessageSpace;
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(Message) || Message == DefaultMessageSpace)
                    {
                        Message = DefaultMessageYoutube;
                    }
                }
            }
        }
    }

    public string Url
    {
        get => _url;
        set => SetField(ref _url, value);
    }

    public string TitleContains
    {
        get => _titleContains;
        set => SetField(ref _titleContains, value);
    }

    public string Label
    {
        get => _label;
        set
        {
            if (SetField(ref _label, value))
            {
                OnPropertyChanged(nameof(DisplayLabel));
            }
        }
    }

    public string Voice
    {
        get => _voice;
        set => SetField(ref _voice, value);
    }

    public double VoiceVolume
    {
        get => _voiceVolume;
        set => SetField(ref _voiceVolume, value);
    }

    public string Bgm
    {
        get => _bgm;
        set => SetField(ref _bgm, value);
    }

    public double BgmVolume
    {
        get => _bgmVolume;
        set => SetField(ref _bgmVolume, value);
    }

    public string Message
    {
        get => _message;
        set => SetField(ref _message, value);
    }

    public string BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            if (SetField(ref _backgroundColor, value))
            {
                OnPropertyChanged(nameof(BackgroundColorValue));
            }
        }
    }

    public string TextColor
    {
        get => _textColor;
        set
        {
            if (SetField(ref _textColor, value))
            {
                OnPropertyChanged(nameof(TextColorValue));
            }
        }
    }

    public Color BackgroundColorValue
    {
        get => ParseColor(BackgroundColor, Colors.Red);
        set => BackgroundColor = ToHex(value);
    }

    public Color TextColorValue
    {
        get => ParseColor(TextColor, Colors.Black);
        set => TextColor = ToHex(value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? "(no label)" : Label;

    public static AlertEditor FromConfig(AlertConfig config)
    {
        return new AlertEditor
        {
            Service = string.IsNullOrWhiteSpace(config.Service) ? "youtube" : config.Service,
            Url = config.Url,
            TitleContains = config.TitleContains,
            Label = config.Label,
            Voice = config.Voice,
            VoiceVolume = NormalizeVolume(config.VoiceVolume),
            Bgm = config.Bgm,
            BgmVolume = NormalizeVolume(config.BgmVolume),
            Message = config.Message,
            BackgroundColor = string.IsNullOrWhiteSpace(config.Colors.Background) ? "#FF0000" : config.Colors.Background,
            TextColor = string.IsNullOrWhiteSpace(config.Colors.Text) ? "#000000" : config.Colors.Text
        };
    }

    public static AlertEditor CreateDefault(int index)
    {
        return new AlertEditor
        {
            Label = $"ALERT {index}",
            Message = DefaultMessageYoutube,
            BgmVolume = 50
        };
    }

    public AlertConfig ToConfig()
    {
        var service = NormalizeService(Service);
        var message = Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = service == "x_space" ? DefaultMessageSpace : DefaultMessageYoutube;
        }

        return new AlertConfig
        {
            Service = service,
            Url = Url.Trim(),
            TitleContains = TitleContains.Trim(),
            Label = Label.Trim(),
            Voice = Voice.Trim(),
            VoiceVolume = NormalizeVolume(VoiceVolume),
            Bgm = Bgm.Trim(),
            BgmVolume = NormalizeVolume(BgmVolume),
            Message = message,
            Colors = new AlertColors
            {
                Background = string.IsNullOrWhiteSpace(BackgroundColor) ? "#FF0000" : BackgroundColor.Trim(),
                Text = string.IsNullOrWhiteSpace(TextColor) ? "#000000" : TextColor.Trim()
            }
        };
    }

    private static string NormalizeService(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "youtube" : value.Trim();
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private static Color ParseColor(string value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        try
        {
            return Color.FromArgb(value.Trim());
        }
        catch
        {
            return fallback;
        }
    }

    private static string ToHex(Color color)
    {
        var a = (int)Math.Round(color.Alpha * 255);
        var r = (int)Math.Round(color.Red * 255);
        var g = (int)Math.Round(color.Green * 255);
        var b = (int)Math.Round(color.Blue * 255);
        if (a >= 255)
        {
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        return $"#{a:X2}{r:X2}{g:X2}{b:X2}";
    }

    private static double NormalizeVolume(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return 100;
        return Math.Clamp(value, 0, 100);
    }
}
