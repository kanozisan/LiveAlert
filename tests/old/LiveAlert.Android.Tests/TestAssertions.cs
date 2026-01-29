namespace LiveAlert.Android.Tests;

public static class TestAssertions
{
    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void Equal(string expected, string actual, string message)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}, Actual: {actual}");
        }
    }

    public static void Equal(bool expected, bool actual, string message)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException($"{message} Expected: {expected}, Actual: {actual}");
        }
    }

    public static void NotNull(object? value, string message)
    {
        if (value == null)
        {
            throw new InvalidOperationException(message);
        }
    }
}
