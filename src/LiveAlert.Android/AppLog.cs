using System;
using System.Diagnostics;
using System.IO;
using Android.Content;
using Android.Util;

namespace LiveAlert;

public static class AppLog
{
    private const string Tag = "LiveAlert";
    private const long MaxLogBytes = 2 * 1024 * 1024;
    private static readonly object FileLock = new();
    private static string? _logFilePath;

    public static string? LogFilePath => _logFilePath;

    public static void Init(Context? context)
    {
        if (_logFilePath != null) return;
        try
        {
            var dir = context?.FilesDir?.AbsolutePath;
            if (string.IsNullOrWhiteSpace(dir))
            {
                return;
            }
            _logFilePath = Path.Combine(dir, "livealert.log");
            File.WriteAllText(_logFilePath, string.Empty);
            Info($"Log initialized: {_logFilePath}");
        }
        catch
        {
            // ignore log init failures
        }
    }

    public static void Info(string message) => Write(LogPriority.Info, "INFO", message, null);

    public static void Warn(string message) => Write(LogPriority.Warn, "WARN", message, null);

    public static void Error(string message, Exception? ex = null) => Write(LogPriority.Error, "ERROR", message, ex);

    private static void Write(LogPriority priority, string level, string message, Exception? ex)
    {
        var line = $"{DateTimeOffset.UtcNow:O} [{level}] {message}";
        if (ex != null)
        {
            line += $" | {ex}";
        }

        try
        {
            Log.WriteLine(priority, Tag, line);
        }
        catch
        {
        }

#if DEBUG
        try
        {
            Debug.WriteLine($"{Tag}: {line}");
            Trace.WriteLine($"{Tag}: {line}");
        }
        catch
        {
        }
#endif

        if (_logFilePath == null)
        {
            return;
        }

        try
        {
            lock (FileLock)
            {
                if (File.Exists(_logFilePath))
                {
                    var info = new FileInfo(_logFilePath);
                    if (info.Length > MaxLogBytes)
                    {
                        var oldPath = _logFilePath + ".old";
                        try
                        {
                            if (File.Exists(oldPath))
                            {
                                File.Delete(oldPath);
                            }
                            File.Move(_logFilePath, oldPath, true);
                        }
                        catch
                        {
                        }
                    }
                }

                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
        }
        catch
        {
        }
    }
}
