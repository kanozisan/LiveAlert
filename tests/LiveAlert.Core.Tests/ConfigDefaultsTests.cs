using LiveAlert.Core;
using Xunit;

namespace LiveAlert.Core.Tests;

public sealed class ConfigDefaultsTests
{
    [Fact]
    public void CreateDefault_HasExpectedDefaults()
    {
        var config = ConfigDefaults.CreateDefault();

        Assert.Single(config.Alerts);
        Assert.Equal(5, config.DedupeMinutes);
        var alert = config.Alerts[0];
        Assert.Equal("SAMPLE", alert.Label);
        Assert.Equal("https://www.youtube.com/channel/XXXX", alert.Url);
        Assert.Equal(100, alert.VoiceVolume);
        Assert.Equal(50, alert.BgmVolume);
        Assert.Equal("警告　{label} がライブ開始", alert.Message);
        Assert.Equal("#FF0000", alert.Colors.Background);
        Assert.Equal("#000000", alert.Colors.Text);

        var options = config.Options;
        Assert.Equal(60, options.PollIntervalSec);
        Assert.Equal(30, options.MaxAlarmDurationSec);
        Assert.Equal("top", options.BandPosition);
        Assert.Equal(340, options.BandHeightPx);
        Assert.True(options.HotReload);
        Assert.Equal("alarm", options.NotificationMode);
        Assert.Equal("alarm", options.DisplayMode);
        Assert.Equal("alarm", options.AudioMode);
        Assert.Equal(5, options.LoopIntervalSec);
        Assert.Equal(-1, options.ExpandedAlertIndex);
        Assert.False(options.DebugMode);
    }
}
