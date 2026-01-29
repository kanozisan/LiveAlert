namespace LiveAlert.Android.Tests;

public static class TestRegistry
{
    public static IEnumerable<TestCase> All()
    {
        foreach (var test in AlertQueueTests.All())
        {
            yield return test;
        }

        foreach (var test in YouTubeDetectorTests.All())
        {
            yield return test;
        }
    }
}
