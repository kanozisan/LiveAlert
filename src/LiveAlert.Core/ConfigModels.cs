using System.Text.Json.Serialization;

namespace LiveAlert.Core;

public sealed class ConfigRoot
{
    [JsonPropertyName("dedupeMinutes")]
    public int DedupeMinutes { get; set; } = 5;

    [JsonPropertyName("alerts")]
    public List<AlertConfig> Alerts { get; set; } = new();

    [JsonPropertyName("options")]
    public AlertOptions Options { get; set; } = new();
}

public sealed class AlertConfig
{
    [JsonPropertyName("service")]
    public string? Service { get; set; } = "youtube";

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("titleContains")]
    public string TitleContains { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("voice")]
    public string Voice { get; set; } = string.Empty;

    [JsonPropertyName("voiceVolume")]
    public double VoiceVolume { get; set; } = 100;

    [JsonPropertyName("bgm")]
    public string Bgm { get; set; } = string.Empty;

    [JsonPropertyName("bgmVolume")]
    public double BgmVolume { get; set; } = 50;

    [JsonPropertyName("message")]
    public string Message { get; set; } = "警告　{label} がライブ開始";

    [JsonPropertyName("colors")]
    public AlertColors Colors { get; set; } = new();
}

public sealed class AlertColors
{
    [JsonPropertyName("background")]
    public string Background { get; set; } = "#FF0000";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "#000000";
}

public sealed class AlertOptions
{
    [JsonPropertyName("pollIntervalSec")]
    public int PollIntervalSec { get; set; } = 60;

    [JsonPropertyName("maxAlarmDurationSec")]
    public int MaxAlarmDurationSec { get; set; } = 30;

    [JsonPropertyName("bandPosition")]
    public string BandPosition { get; set; } = "top"; // top or bottom

    [JsonPropertyName("bandHeightPx")]
    public int BandHeightPx { get; set; } = 340;

    [JsonPropertyName("hotReload")]
    public bool HotReload { get; set; } = true;

    [JsonPropertyName("notificationMode")]
    public string NotificationMode { get; set; } = "alarm"; // alarm | manner | off

    [JsonPropertyName("displayMode")]
    public string DisplayMode { get; set; } = "alarm"; // alarm | manner | off

    [JsonPropertyName("audioMode")]
    public string AudioMode { get; set; } = "alarm"; // alarm | manner | off

    [JsonPropertyName("loopIntervalSec")]
    public int LoopIntervalSec { get; set; } = 5;

    [JsonPropertyName("expandedAlertIndex")]
    public int ExpandedAlertIndex { get; set; } = -1;

    [JsonPropertyName("debugMode")]
    public bool DebugMode { get; set; }
}

public static class ConfigDefaults
{
    public static ConfigRoot CreateDefault()
    {
        return new ConfigRoot
        {
            DedupeMinutes = 5,
            Alerts =
            {
                new AlertConfig
                {
                    Url = "https://www.youtube.com/channel/XXXX",
                    Label = "SAMPLE",
                    Voice = "",
                    VoiceVolume = 100,
                    Bgm = "",
                    BgmVolume = 50,
                    Message = "警告　{label} がライブ開始",
                    Colors = new AlertColors
                    {
                        Background = "#FF0000",
                        Text = "#000000"
                    },
                }
            },
            Options = new AlertOptions()
        };
    }
}
