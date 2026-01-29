using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using LiveAlert.Core;
using Xunit;

namespace LiveAlert.Core.Tests;

public sealed class ConfigManagerTests
{
    [Fact]
    public async Task LoadAsync_CreatesDefaultWhenMissing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LiveAlertTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.json");
        var manager = new ConfigManager(configPath);

        await manager.LoadAsync();

        Assert.True(File.Exists(configPath));
        Assert.NotNull(manager.Current);
        Assert.NotEmpty(manager.Current.Alerts);
    }

    [Fact]
    public async Task SaveAsync_RoundTripsValues()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LiveAlertTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.json");
        var manager = new ConfigManager(configPath);

        var config = new ConfigRoot
        {
            DedupeMinutes = 7,
            Alerts =
            {
                new AlertConfig
                {
                    Service = "x_space",
                    Url = "https://www.youtube.com/channel/TEST",
                    TitleContains = "yasagure",
                    Label = "ALPHA"
                }
            },
            Options = new AlertOptions
            {
                PollIntervalSec = 120,
                MaxAlarmDurationSec = 45,
                BandPosition = "bottom",
                BandHeightPx = 72,
                HotReload = false,
                NotificationMode = "manner",
                DisplayMode = "alarm",
                AudioMode = "manner",
                LoopIntervalSec = 12
            }
        };

        await manager.SaveAsync(config);

        var reloaded = new ConfigManager(configPath);
        await reloaded.LoadAsync();

        Assert.Equal("ALPHA", reloaded.Current.Alerts[0].Label);
        Assert.Equal("yasagure", reloaded.Current.Alerts[0].TitleContains);
        Assert.Equal(7, reloaded.Current.DedupeMinutes);
        Assert.Equal(120, reloaded.Current.Options.PollIntervalSec);
        Assert.Equal("bottom", reloaded.Current.Options.BandPosition);
        Assert.Equal("manner", reloaded.Current.Options.NotificationMode);
        Assert.Equal(12, reloaded.Current.Options.LoopIntervalSec);
    }

    [Fact]
    public async Task LoadAsync_ParsesCommentsAndTrailingCommas()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LiveAlertTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.json");
        var json = """
        {
          // comment
          "alerts": [
            {
              "url": "https://www.youtube.com/channel/XXXX",
              "label": "NIKO",
            },
          ],
          "options": {
            "pollIntervalSec": 60,
          },
        }
        """;
        await File.WriteAllTextAsync(configPath, json, Encoding.UTF8);
        var manager = new ConfigManager(configPath);

        await manager.LoadAsync();

        Assert.Single(manager.Current.Alerts);
        Assert.Equal("NIKO", manager.Current.Alerts[0].Label);
        Assert.Equal(60, manager.Current.Options.PollIntervalSec);
    }
}
