using System.Diagnostics;

internal static class Logger
{
    public static bool ShouldLog(bool enableLocalLogging) => enableLocalLogging;

    public static void Log(string message, string category = "Audio")
    {
        Debug.WriteLine($"[{category}] {message}");
    }
}
