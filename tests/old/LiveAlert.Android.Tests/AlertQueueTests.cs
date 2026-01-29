using LiveAlert.Core;

namespace LiveAlert.Android.Tests;

public static class AlertQueueTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return new TestCase("AlertQueue priority/time ordering", Enqueue_Dequeue_OrdersByPriorityDescThenTimeAsc);
        yield return new TestCase("AlertQueue deduplicates by videoId", Enqueue_DeduplicatesByVideoId);
    }

    private static void Enqueue_Dequeue_OrdersByPriorityDescThenTimeAsc()
    {
        var queue = new AlertQueue();
        var first = new AlertQueueItem(new AlertEvent(BuildAlert(1), 0, "v1", DateTimeOffset.UtcNow), 1, DateTimeOffset.UtcNow.AddSeconds(5));
        var second = new AlertQueueItem(new AlertEvent(BuildAlert(5), 1, "v2", DateTimeOffset.UtcNow), 5, DateTimeOffset.UtcNow.AddSeconds(2));
        var third = new AlertQueueItem(new AlertEvent(BuildAlert(5), 2, "v3", DateTimeOffset.UtcNow), 5, DateTimeOffset.UtcNow.AddSeconds(1));

        queue.Enqueue(first);
        queue.Enqueue(second);
        queue.Enqueue(third);

        var next = queue.DequeueNext();
        TestAssertions.NotNull(next, "First dequeue should return an item.");
        TestAssertions.Equal("v3", next!.VideoId, "Highest priority/earliest should be v3.");

        var next2 = queue.DequeueNext();
        TestAssertions.NotNull(next2, "Second dequeue should return an item.");
        TestAssertions.Equal("v2", next2!.VideoId, "Second should be v2.");

        var next3 = queue.DequeueNext();
        TestAssertions.NotNull(next3, "Third dequeue should return an item.");
        TestAssertions.Equal("v1", next3!.VideoId, "Third should be v1.");
    }

    private static void Enqueue_DeduplicatesByVideoId()
    {
        var queue = new AlertQueue();
        var item1 = new AlertQueueItem(new AlertEvent(BuildAlert(1), 0, "v1", DateTimeOffset.UtcNow), 1, DateTimeOffset.UtcNow);
        var item2 = new AlertQueueItem(new AlertEvent(BuildAlert(2), 1, "v1", DateTimeOffset.UtcNow), 2, DateTimeOffset.UtcNow.AddSeconds(1));

        queue.Enqueue(item1);
        queue.Enqueue(item2);

        var next = queue.DequeueNext();
        TestAssertions.NotNull(next, "Dequeue should return the first item.");
        TestAssertions.Equal("v1", next!.VideoId, "VideoId should be v1.");
        TestAssertions.True(queue.DequeueNext() == null, "Second dequeue should be null.");
    }

    private static AlertConfig BuildAlert(int priority)
    {
        return new AlertConfig
        {
            Label = "TEST",
            Url = "https://www.youtube.com/channel/TEST",
            Priority = priority
        };
    }

    // Assertions moved to TestAssertions
}
