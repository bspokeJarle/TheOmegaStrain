namespace SteamIntegration;

public static class SteamDiagnostics
{
    private const string LogFilePath = @"C:\Temp\OmegaStrainSteamDiagnostics.txt";

    public static bool Enabled { get; set; }

    public static void Clear()
    {
        if (!Enabled)
        {
            return;
        }

        try
        {
            EnsureLogDirectoryExists();
            File.WriteAllText(LogFilePath, "");
        }
        catch
        {
        }
    }

    public static void Write(string message)
    {
        if (!Enabled)
        {
            return;
        }

        try
        {
            EnsureLogDirectoryExists();
            File.AppendAllText(
                LogFilePath,
                $"{DateTime.Now:HH:mm:ss.fff} [Steam] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private static void EnsureLogDirectoryExists()
    {
        string? directory = Path.GetDirectoryName(LogFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
