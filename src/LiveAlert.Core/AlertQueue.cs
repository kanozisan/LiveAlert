using System.Linq;

namespace LiveAlert.Core;

public sealed class AlertQueue
{
    private readonly List<AlertQueueItem> _pending = new();

    public AlertQueueItem? DequeueNext()
    {
        if (_pending.Count == 0) return null;
        _pending.Sort(Compare);
        var item = _pending[0];
        _pending.RemoveAt(0);
        return item;
    }

    public void Enqueue(AlertQueueItem item)
    {
        if (_pending.Any(x => x.VideoId == item.VideoId)) return;
        _pending.Add(item);
    }

    public void RemoveByVideoId(string videoId)
    {
        _pending.RemoveAll(x => x.VideoId == videoId);
    }

    public bool Contains(string videoId)
    {
        return _pending.Any(x => x.VideoId == videoId);
    }

    private static int Compare(AlertQueueItem a, AlertQueueItem b)
    {
        var time = a.DetectedAt.CompareTo(b.DetectedAt);
        if (time != 0) return time;
        return a.Event.AlertIndex.CompareTo(b.Event.AlertIndex);
    }
}

public sealed record AlertQueueItem(AlertEvent Event, DateTimeOffset DetectedAt)
{
    public string VideoId => Event.VideoId;
    public AlertConfig Alert => Event.Alert;
}
