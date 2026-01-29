using System;
using LiveAlert.Core;
using Xunit;

namespace LiveAlert.Core.Tests;

public sealed class AlertQueueTests
{
    [Fact]
    public void DequeueNext_OrdersByDetectedAt()
    {
        var queue = new AlertQueue();
        var alertA = new AlertConfig { Label = "A" };
        var alertB = new AlertConfig { Label = "B" };
        var older = DateTimeOffset.UtcNow.AddSeconds(-10);
        var newer = DateTimeOffset.UtcNow;

        queue.Enqueue(new AlertQueueItem(new AlertEvent(alertA, 0, "vid-a", newer), newer));
        queue.Enqueue(new AlertQueueItem(new AlertEvent(alertB, 1, "vid-b", older), older));

        var first = queue.DequeueNext();

        Assert.NotNull(first);
        Assert.Equal("vid-b", first!.VideoId);
    }

    [Fact]
    public void DequeueNext_OrdersByAlertIndexWhenDetectedAtSame()
    {
        var queue = new AlertQueue();
        var alertA = new AlertConfig { Label = "A" };
        var alertB = new AlertConfig { Label = "B" };
        var now = DateTimeOffset.UtcNow;

        queue.Enqueue(new AlertQueueItem(new AlertEvent(alertA, 0, "vid-a", now), now));
        queue.Enqueue(new AlertQueueItem(new AlertEvent(alertB, 1, "vid-b", now), now));

        var first = queue.DequeueNext();

        Assert.NotNull(first);
        Assert.Equal("vid-a", first!.VideoId);
    }

    [Fact]
    public void Enqueue_DeduplicatesByVideoId()
    {
        var queue = new AlertQueue();
        var alert = new AlertConfig { Label = "A" };
        var now = DateTimeOffset.UtcNow;

        queue.Enqueue(new AlertQueueItem(new AlertEvent(alert, 0, "vid-a", now), now));
        queue.Enqueue(new AlertQueueItem(new AlertEvent(alert, 0, "vid-a", now.AddSeconds(1)), now.AddSeconds(1)));

        Assert.True(queue.Contains("vid-a"));
        Assert.NotNull(queue.DequeueNext());
        Assert.Null(queue.DequeueNext());
    }

    [Fact]
    public void RemoveByVideoId_RemovesMatchingItems()
    {
        var queue = new AlertQueue();
        var alert = new AlertConfig { Label = "A" };
        var now = DateTimeOffset.UtcNow;

        queue.Enqueue(new AlertQueueItem(new AlertEvent(alert, 0, "vid-a", now), now));
        queue.Enqueue(new AlertQueueItem(new AlertEvent(alert, 0, "vid-b", now), now));

        queue.RemoveByVideoId("vid-a");

        Assert.False(queue.Contains("vid-a"));
        Assert.True(queue.Contains("vid-b"));
    }
}
